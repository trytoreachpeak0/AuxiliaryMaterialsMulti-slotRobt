using AgvDispatch.Sdk;
using AgvDispatch.Sdk.Models;
using Microsoft.Extensions.Configuration;
using WireCabinet.Core.Config;
using WireCabinet.Rcs;
using WireCabinet.Slots;

namespace WireCabinet.Hmi.Services;

public sealed class AgvServices : IDisposable
{
    private readonly AgvDispatchOptions _options;
    private readonly AgvThresholdOptions _thresholds;
    private IAgvDispatchClient? _client;

    public StationConfiguration Stations { get; }
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
    public bool CanPlaceMoveOrChargeOrder { get; private set; }
    public bool CanCancelActiveOrder { get; private set; }
    public bool CanReleaseEmergency { get; private set; }
    public int? LastWorkStationBeforeCharge { get; set; }

    public int LowBatteryPercent => _thresholds.LowBatteryPercent;
    public int FullBatteryPercent => _thresholds.FullBatteryPercent;

    public AgvServices(IConfiguration config, ISlotControlService slots, IDoorStateProvider doors)
    {
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

        IsConfigured = IsAgvConfigComplete(config);
        if (!IsConfigured)
        {
            ConfigHint = "请在 appsettings.json 填写 AgvDispatch（BaseUrl、Username、Password、DefaultDeviceKey）。";
            return;
        }

        try
        {
            _client = AgvDispatchClient.Create(_options);
            ControlLoop = new AgvControlLoop(_client, _options, doors);
            Move = new ManualMoveService(_client, _options, slots, ControlLoop);
            Charge = new LowBatteryChargeService(_client, _thresholds, () => HasActiveOrder);
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

        if (HasActiveOrder)
            return (false, "已有进行中订单，请先取消或等待完成。");

        if (!CanPlaceMoveOrChargeOrder)
            return (false, "仅系统 IDLE 且无进行中订单时可下发移动单。");

        var ws = Stations.EnabledWorkStations.FirstOrDefault(s => s.RcsDestination == destination);
        LastWorkStationBeforeCharge = destination;
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

        if (HasActiveOrder)
            return (false, "已有进行中订单，请先取消或等待完成。");

        if (!CanPlaceMoveOrChargeOrder)
            return (false, "仅系统 IDLE 且无进行中订单时可下充电单。");

        if (LastPosition > 0 && Stations.EnabledWorkStations.Any(s => s.RcsDestination == LastPosition))
            LastWorkStationBeforeCharge = LastPosition;

        try
        {
            var order = await _client!.CreateChargeOrderAsync(ct).ConfigureAwait(false);
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
        if (_client is null || string.IsNullOrWhiteSpace(LastOrderTaskId))
            return (false, "当前无进行中订单。");
        if (!CanCancelActiveOrder)
            return (false, "仅暂停或挂起中的任务可取消。");

        try
        {
            await _client.CancelOrderAsync(LastOrderTaskId, ct).ConfigureAwait(false);
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

        await RefreshStatusAsync(ct).ConfigureAwait(false);
        var policyMsg = await RunAutoChargePolicyAsync(ct).ConfigureAwait(false);

        if (navigator is not null && onArrivedAtStation is not null)
        {
            navigator.ResetIfLeftStation(LastPosition);
            var station = navigator.TryGetArrivedStation(this);
            if (station is not null)
                onArrivedAtStation(station);
        }

        return policyMsg;
    }

    public async Task<string?> RunAutoChargePolicyAsync(CancellationToken ct = default)
    {
        if (Charge is null || !IsConnected) return null;

        if (LastWorkStationBeforeCharge is null && LastPosition > 0)
            LastWorkStationBeforeCharge = LastPosition;

        var low = await Charge.TryAutoChargeAsync(LastBatteryPercent, ct).ConfigureAwait(false);
        if (low is not null) return low;

        if (LastWorkStationBeforeCharge is int dest)
            return await Charge.TryReturnToWorkStationAsync(LastBatteryPercent, ct).ConfigureAwait(false);

        return null;
    }

    public void Dispose()
    {
        if (_client is IDisposable d) d.Dispose();
        _client = null;
    }

    private void ApplySnapshot(VehicleSnapshot snap)
    {
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
        LastTaskTargetDisplay = FormatTaskTarget(v, snap.Order);
        HasActiveOrder = !string.IsNullOrWhiteSpace(v.OrderTaskId);
        EvaluateUiGates(snap);
        LastEmergencyDisplay = FormatEmergency(v);
    }

    private void EvaluateUiGates(VehicleSnapshot snap)
    {
        var v = snap.Vehicle;
        var thresholds = _options.StateThresholds;

        CanPlaceMoveOrChargeOrder = IsConnected
                                    && string.Equals(v.SysState, "IDLE", StringComparison.OrdinalIgnoreCase)
                                    && !HasActiveOrder;

        CanCancelActiveOrder = HasActiveOrder && (
            (v.EffectiveMoveState?.Contains("PAUSED", StringComparison.OrdinalIgnoreCase) ?? false)
            || string.Equals(v.SysState, "PAUSE", StringComparison.OrdinalIgnoreCase)
            || (snap.Order is not null && snap.Order.EffectiveOrderState == thresholds.OrderStatePending));

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

    private string FormatTaskTarget(VehicleInfoDto v, OrderDetailDto? order)
    {
        if (string.IsNullOrWhiteSpace(v.OrderTaskId))
            return "无进行中任务";

        var dest = order?.Mission?.FirstOrDefault(m =>
            string.Equals(m.Type, "move", StringComparison.OrdinalIgnoreCase))?.Destination;
        if (dest is null or 0)
            dest = v.EndStationNo > 0 ? v.EndStationNo : null;

        var name = !string.IsNullOrWhiteSpace(v.EndStationName)
            ? v.EndStationName
            : dest is int d ? FormatPosition(d) : "—";

        var orderLabel = string.IsNullOrWhiteSpace(v.OrderName) ? v.OrderTaskId : $"{v.OrderName}（{v.OrderTaskId}）";
        return dest is int station
            ? $"{orderLabel} → {name}（站点 {station}）"
            : $"{orderLabel} → {name}";
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
