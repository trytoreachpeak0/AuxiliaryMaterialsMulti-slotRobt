using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Models;
using Microsoft.Extensions.Configuration;
using WireCabinet.Core.Config;
using WireCabinet.Rcs;
using WireCabinet.Slots;

namespace WireCabinet.Hmi.Services;

public sealed class AgvServices : IDisposable
{
    public const string AlreadyAtStationMessage = "发货柜已经在站点";
    public const string AlreadyAtChargeStationMessage = "发货柜已在充电点";

    private readonly AgvDispatchOptions _options;
    private readonly AgvThresholdOptions _thresholds;
    private readonly AgvOrderGate _orderGate;
    private readonly ISlotControlService _slots;
    private IAgvDispatchClient? _client;
    private VehicleSnapshot _lastSnapshot = new();
    private bool _startupCleanupDone;

    public StationConfiguration Stations { get; }
    public AgvOrderGate OrderGate => _orderGate;
    public bool IsConfigured { get; }
    public string ConfigHint { get; } = "";
    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }

    public ManualMoveService? Move { get; private set; }
    public LowBatteryChargeService? Charge { get; private set; }
    public AgvControlLoop? ControlLoop { get; private set; }

    public double LastBatteryPercent { get; private set; }
    public int LastPosition { get; private set; }
    public string LastPositionDisplay { get; private set; } = "—";
    public string LastSysState { get; private set; } = "—";
    public string LastMoveState { get; private set; } = "—";
    public int LastProgress { get; private set; }
    public string? LastOrderTaskId { get; private set; }
    public string? LastOrderName { get; private set; }
    public string LastTaskTargetDisplay { get; private set; } = "—";
    public string? LastEmergencyState { get; private set; }
    public string LastEmergencyDisplay { get; private set; } = "—";
    public string? LastLocationState { get; private set; }
    public string LastLocationDisplay { get; private set; } = "—";
    public bool HasActiveOrder { get; private set; }
    public bool HasBlockingOrder { get; private set; }
    public string LastOrderStateDisplay { get; private set; } = "—";
    public bool CanPlaceMoveOrChargeOrder { get; private set; }
    public bool CanCancelActiveOrder { get; private set; }
    public bool CanReleaseEmergency { get; private set; }
    public int? LastWorkStationBeforeCharge { get; set; }

    public int LowBatteryPercent => _thresholds.LowBatteryPercent;
    public int FullBatteryPercent => _thresholds.FullBatteryPercent;
    public int RecommendedPollIntervalMs => _options.RecommendedPollIntervalMs;
    public DoorInterlockTickResult? LastDoorInterlockTick { get; private set; }

    public AgvServices(IConfiguration config, ISlotControlService slots, IDoorStateProvider doors)
    {
        _slots = slots;
        Stations = StationConfiguration.Load();
        _thresholds = new AgvThresholdOptions
        {
            LowBatteryPercent = config.GetValue("AgvThresholds:LowBatteryPercent", Stations.LowBatteryPercent),
            FullBatteryPercent = config.GetValue("AgvThresholds:FullBatteryPercent", Stations.FullBatteryPercent)
        };

        _options = new AgvDispatchOptions();
        config.GetSection(AgvDispatchOptions.SectionName).Bind(_options);

        if (_options.ChargeDestination <= 0 && Stations.ChargeStation is { RcsDestination: > 0 } chg)
            _options.ChargeDestination = chg.RcsDestination;

        if (_options.VehicleQueryDeviceIds.Length == 0)
            _options.VehicleQueryDeviceIds = [0];

        _orderGate = new AgvOrderGate(_options.StateThresholds, () => ControlLoop?.CallInProgress ?? false);

        IsConfigured = IsAgvConfigComplete(config);
        if (!IsConfigured)
        {
            ConfigHint = "请在 appsettings.json 填写 AgvDispatch（BaseUrl、Username、Password、DefaultDeviceKey）。";
            return;
        }

        try
        {
            _client = AgvDispatchClient.Create(_options);
            ControlLoop = new AgvControlLoop(_client, _options, doors, _orderGate);
            Move = new ManualMoveService(_client, _options, slots, ControlLoop);
            Charge = new LowBatteryChargeService(_client, _options, _thresholds, _orderGate, Stations);
        }
        catch (Exception ex)
        {
            ConfigHint = $"RCS 初始化失败：{ex.Message}";
            IsConfigured = false;
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        if (_client is null) return false;
        try
        {
            await _client.LoginAsync(ct).ConfigureAwait(false);
            IsConnected = true;
            LastError = null;
            await RefreshStatusAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
            return false;
        }
    }

    public async Task EnsureTokenFreshAsync(CancellationToken ct = default)
    {
        if (_client is null || !IsConnected) return;

        var interval = TimeSpan.FromHours(_options.TokenRefreshIntervalHours);
        var obtained = _client.TokenObtainedAtUtc;
        if (obtained is null || DateTime.UtcNow - obtained.Value >= interval)
        {
            await _client.ForceReLoginAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task RefreshStatusAsync(CancellationToken ct = default)
    {
        if (_client is null || !IsConnected) return;
        try
        {
            await EnsureTokenFreshAsync(ct).ConfigureAwait(false);
            var snap = await _client.GetVehicleSnapshotAsync(cancellationToken: ct).ConfigureAwait(false);
            ApplySnapshot(snap);
            await _orderGate.ReconcilePendingAsync(_client, snap, ct).ConfigureAwait(false);
            ApplySnapshot(snap);
            Move?.RefreshFromSnapshot(snap);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public async Task<(bool Ok, string Message)> MoveToStationAsync(int destination, CancellationToken ct = default)
    {
        if (Move is null) return (false, "RCS 未就绪");
        if (!IsConnected && !await ConnectAsync(ct).ConfigureAwait(false))
            return (false, LastError ?? "RCS 登录失败");

        if (HasBlockingOrder)
            return (false, "已有进行中或队列中的订单，请先取消或等待完成。");

        if (!CanPlaceMoveOrChargeOrder)
            return (false, "仅系统 IDLE 且无进行中订单时可下发移动单。");

        if (_orderGate.BlocksNewOrder(_lastSnapshot, AgvPendingOrderKind.Move))
            return (false, "当前不可下发移动单，请等待队列中的订单执行。");

        if (destination > 0 && LastPosition == destination)
            return (false, AlreadyAtStationMessage);

        var doorBlock = await TryBlockIfDoorsOpenForDispatchAsync(ct).ConfigureAwait(false);
        if (doorBlock is not null)
            return doorBlock.Value;

        var ws = Stations.EnabledWorkStations.FirstOrDefault(s => s.RcsDestination == destination);
        SetLastWorkStationBeforeCharge(destination);
        var result = await Move.RequestMoveAsync(destination, ct).ConfigureAwait(false);
        await RefreshStatusAsync(ct).ConfigureAwait(false);
        var label = ws?.Name ?? destination.ToString();
        return result.Ok
            ? (true, $"{result.Message} → {label}（站点 {destination}）")
            : (false, result.Message);
    }

    public async Task<(bool Ok, string Message)> GoChargeAsync(CancellationToken ct = default)
    {
        if (Charge is null || _options.ChargeDestination <= 0)
            return (false, "未配置充电站点号（stations.yaml / AgvDispatch:ChargeDestination）。");
        if (!IsConnected && !await ConnectAsync(ct).ConfigureAwait(false))
            return (false, LastError ?? "RCS 登录失败");

        if (HasBlockingOrder)
            return (false, "已有进行中或队列中的订单，请先取消或等待完成。");

        if (!CanPlaceMoveOrChargeOrder)
            return (false, "仅系统 IDLE 且无进行中订单时可下充电单。");

        if (_orderGate.BlocksNewOrder(_lastSnapshot, AgvPendingOrderKind.Charge))
            return (false, "当前不可下充电单，请等待队列中的订单执行。");

        if (_options.ChargeDestination > 0
            && LastPosition == _options.ChargeDestination
            && !_options.StateThresholds.IdleSysStates.Contains(LastSysState, StringComparer.OrdinalIgnoreCase))
            return (false, AlreadyAtChargeStationMessage);

        var doorBlock = await TryBlockIfDoorsOpenForDispatchAsync(ct).ConfigureAwait(false);
        if (doorBlock is not null)
            return doorBlock.Value;

        try
        {
            var order = await _client!.CreateChargeOrderAsync(ct).ConfigureAwait(false);
            _orderGate.RecordPlacedOrder(order.OrderId, AgvPendingOrderKind.Charge);
            Charge.RememberReturnDestinationOnChargeDispatch(LastPosition);
            await RefreshStatusAsync(ct).ConfigureAwait(false);
            var name = Stations.ChargeStation?.Name ?? "充电点";
            return (true, $"已下充电单 → {name}（{order.OrderId}）");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, string Message)> CancelActiveOrderAsync(CancellationToken ct = default)
    {
        if (_client is null)
            return (false, "RCS 未就绪");

        var orderId = _orderGate.ResolveCancelOrderId(_lastSnapshot);
        if (string.IsNullOrWhiteSpace(orderId))
            return (false, "当前无进行中订单。");
        if (!CanCancelActiveOrder)
            return (false, "仅暂停、挂起或队列中的任务可取消。");

        try
        {
            await _client.CancelOrderAsync(orderId, ct).ConfigureAwait(false);
            _orderGate.ClearPending();
            ControlLoop?.ResetCallState();
            Move?.ResetSession();
            await RefreshStatusAsync(ct).ConfigureAwait(false);
            return (true, "已取消当前订单。");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, string Message)> ReleaseEmergencyAsync(CancellationToken ct = default)
    {
        if (_client is null) return (false, "RCS 未就绪");
        if (!CanReleaseEmergency)
            return (false, "当前无需解除急停。");

        try
        {
            await _client.CancelEmergencyAsync(cancellationToken: ct).ConfigureAwait(false);
            await RefreshStatusAsync(ct).ConfigureAwait(false);
            return (true, "已下发解除急停。");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>后台轮询：刷新状态、自动回充；到站时回调。</summary>
    public async Task<string?> PollCycleAsync(
        AgvArrivalNavigator? navigator,
        Action<WorkStationConfig>? onArrivedAtStation,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        if (!IsConnected)
            await ConnectAsync(ct).ConfigureAwait(false);

        var doorTick = await RunDoorInterlockTickAsync(ct).ConfigureAwait(false);
        LastDoorInterlockTick = doorTick;

        string? startupMsg = null;
        if (IsConnected)
            startupMsg = await EnsureStartupOrderCleanupAsync(ct).ConfigureAwait(false);

        var policyMsg = doorTick?.StatusMessage ?? await RunAutoChargePolicyAsync(ct).ConfigureAwait(false);
        if (startupMsg is not null && policyMsg is null)
            policyMsg = startupMsg;

        if (navigator is not null && onArrivedAtStation is not null)
        {
            navigator.ResetIfLeftStation(LastPosition);
            var station = navigator.TryGetArrivedStation(this);
            if (station is not null)
                onArrivedAtStation(station);
        }

        return policyMsg;
    }

    /// <summary>后台轮询：格口联锁 — 移动/充电执行中若格口打开则暂停，关闭后继续。</summary>
    public async Task<DoorInterlockTickResult?> RunDoorInterlockTickAsync(CancellationToken ct = default)
    {
        if (ControlLoop is null || !IsConnected)
        {
            await RefreshStatusAsync(ct).ConfigureAwait(false);
            return null;
        }

        var tick = await ControlLoop.TickAsync(ct: ct).ConfigureAwait(false);
        ApplySnapshot(tick.Snapshot);
        Move?.RefreshFromSnapshot(tick.Snapshot);

        var openSlots = await _slots.ListOpenInterlockSlotNosAsync(ct).ConfigureAwait(false);
        var statusMessage = tick.ExecutedAction switch
        {
            "PauseMovement" => "检测到格口打开，已暂停 AGV 任务。",
            "ContinueMovement" => "格口已关闭，AGV 任务已继续。",
            _ => null
        };

        if (statusMessage is null && tick.ExecutedAction is null)
            return null;

        return new DoorInterlockTickResult(statusMessage, tick.ExecutedAction, openSlots);
    }

    private async Task<(bool Ok, string Message)?> TryBlockIfDoorsOpenForDispatchAsync(CancellationToken ct)
    {
        if (_slots.HasUnlockInProgress())
            return (false, "存在开锁中，禁止下发移动/充电单。");

        var open = await _slots.ListOpenInterlockSlotNosAsync(ct).ConfigureAwait(false);
        if (open.Count == 0)
            return null;

        var labels = AgvDoorInterlockAlert.FormatSlotLabels(open);
        return (false, AgvDoorInterlockAlert.BuildDispatchBlockedMessage(labels));
    }

    public async Task<string?> RunAutoChargePolicyAsync(CancellationToken ct = default)
    {
        if (Charge is null || !IsConnected || _client is null) return null;

        var snap = await _client.GetVehicleSnapshotAsync(cancellationToken: ct).ConfigureAwait(false);
        ApplySnapshot(snap);
        await _orderGate.ReconcilePendingAsync(_client, snap, ct).ConfigureAwait(false);
        ApplySnapshot(snap);

        var low = await Charge.TryAutoChargeAsync(snap, ct).ConfigureAwait(false);
        if (low is not null)
        {
            ApplySnapshot(snap);
            return low;
        }

        if (Charge.PostChargeReturnPending)
        {
            var ret = await Charge.TryReturnToWorkStationAsync(snap, ct).ConfigureAwait(false);
            if (ret is not null)
                ApplySnapshot(snap);
            return ret;
        }

        return null;
    }

    private async Task<string?> EnsureStartupOrderCleanupAsync(CancellationToken ct)
    {
        if (_startupCleanupDone || _client is null)
            return null;

        _startupCleanupDone = true;
        var snap = await _client.GetVehicleSnapshotAsync(cancellationToken: ct).ConfigureAwait(false);
        var thresholds = _options.StateThresholds;
        var canceled = new List<string>();

        var isIdle = thresholds.IdleSysStates.Contains(snap.Vehicle.SysState ?? "", StringComparer.OrdinalIgnoreCase);
        var orderId = snap.Vehicle.OrderTaskId;

        if (isIdle)
        {
            if (!string.IsNullOrWhiteSpace(orderId))
            {
                try
                {
                    await _client.CancelOrderAsync(orderId, ct).ConfigureAwait(false);
                    canceled.Add(orderId);
                }
                catch (Exception ex)
                {
                    return $"启动清理订单失败：{ex.Message}";
                }
            }
            else if (snap.Order is not null
                     && snap.Order.EffectiveOrderState == thresholds.OrderStateCanceled
                     && !string.IsNullOrWhiteSpace(snap.Order.UpperId))
            {
                try
                {
                    await _client.CancelOrderAsync(snap.Order.UpperId, ct).ConfigureAwait(false);
                    canceled.Add(snap.Order.UpperId);
                }
                catch
                {
                    // 无有效 orderId 时忽略
                }
            }

            _orderGate.ClearPending();
            ControlLoop?.ResetCallState();
            Move?.ResetSession();
        }

        Charge?.BootstrapExternalChargeSession(snap);
        await RefreshStatusAsync(ct).ConfigureAwait(false);

        if (!isIdle && !string.IsNullOrWhiteSpace(orderId))
            return "车辆任务执行中，保留当前订单。";

        if (canceled.Count > 0)
            return $"启动已清除历史订单：{string.Join(", ", canceled)}";

        if (Charge?.PostChargeReturnPending == true)
        {
            var mh = Stations.ResolveDefaultReturnStation();
            if (mh is not null)
                return $"检测到已在充电，充满后将返航 {mh.Name}。";
        }

        return null;
    }

    private void SetLastWorkStationBeforeCharge(int destination)
    {
        LastWorkStationBeforeCharge = destination;
        Charge?.RememberWorkStation(destination);
    }

    public void Dispose()
    {
        if (_client is IDisposable d) d.Dispose();
        _client = null;
    }

    private void ApplySnapshot(VehicleSnapshot snap)
    {
        _lastSnapshot = snap;
        _orderGate.ApplySnapshot(snap);

        var v = snap.Vehicle;
        LastBatteryPercent = v.Battery;
        LastPosition = v.CurrentPosition;
        LastPositionDisplay = FormatPosition(v.CurrentPosition);
        LastSysState = v.SysState ?? "—";
        LastMoveState = v.EffectiveMoveState ?? "—";
        LastProgress = v.Progress;
        LastOrderTaskId = v.OrderTaskId;
        LastOrderName = v.OrderName;
        LastEmergencyState = v.EmergencyState;
        LastLocationState = v.LocationState;
        LastLocationDisplay = LocationStateDisplay.Format(v.LocationState, _options.StateThresholds);
        LastTaskTargetDisplay = _orderGate.FormatTaskSummary(snap, FormatPosition);
        LastOrderStateDisplay = _orderGate.FormatOrderStateDisplay(snap);
        HasActiveOrder = !string.IsNullOrWhiteSpace(v.OrderTaskId);
        HasBlockingOrder = _orderGate.HasBlockingOrder(snap);
        EvaluateUiGates(snap);
        LastEmergencyDisplay = FormatEmergency(v);
        LastWorkStationBeforeCharge = Charge?.ReturnDestination;
    }

    private void EvaluateUiGates(VehicleSnapshot snap)
    {
        var v = snap.Vehicle;
        var thresholds = _options.StateThresholds;

        CanPlaceMoveOrChargeOrder = IsConnected
                                    && string.Equals(v.SysState, "IDLE", StringComparison.OrdinalIgnoreCase)
                                    && !HasBlockingOrder;

        CanCancelActiveOrder = _orderGate.CanCancelOrder(snap);

        var sysError = thresholds.ErrorSysStates.Contains(v.SysState ?? "", StringComparer.OrdinalIgnoreCase);
        var emgActive = !string.IsNullOrWhiteSpace(v.EmergencyState)
                        && thresholds.EmergencyActiveContains.Any(e =>
                            v.EmergencyState!.Contains(e, StringComparison.OrdinalIgnoreCase));
        CanReleaseEmergency = sysError || emgActive;
    }

    private string FormatPosition(int position)
    {
        if (position <= 0) return "—";
        var ws = Stations.EnabledWorkStations.FirstOrDefault(s => s.RcsDestination == position);
        if (ws is not null) return $"{ws.Name}（{position}）";
        if (Stations.ChargeStation is { RcsDestination: var chg } && chg == position)
            return $"{Stations.ChargeStation.Name}（{position}）";
        return position.ToString();
    }

    private string FormatEmergency(VehicleInfoDto v)
    {
        if (!CanReleaseEmergency && string.IsNullOrWhiteSpace(v.EmergencyState))
            return "正常";
        if (!string.IsNullOrWhiteSpace(v.EmergencyState))
            return v.EmergencyState;
        if (_options.StateThresholds.ErrorSysStates.Contains(v.SysState ?? "", StringComparer.OrdinalIgnoreCase))
            return "系统 ERROR";
        return "—";
    }

    private static bool IsAgvConfigComplete(IConfiguration config)
    {
        var section = config.GetSection(AgvDispatchOptions.SectionName);
        var url = section["BaseUrl"];
        var user = section["Username"];
        var pass = section["Password"];
        var key = section["DefaultDeviceKey"];
        return !string.IsNullOrWhiteSpace(url)
               && !string.IsNullOrWhiteSpace(user)
               && !string.IsNullOrWhiteSpace(pass)
               && !string.IsNullOrWhiteSpace(key);
    }
}
