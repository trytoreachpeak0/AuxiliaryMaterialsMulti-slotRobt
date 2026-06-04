using AgvDispatch.Sdk;
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
    public string LastSysState { get; private set; } = "—";
    public string LastMoveState { get; private set; } = "—";
    public int? LastWorkStationBeforeCharge { get; set; }

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
            Move = new ManualMoveService(_client, _options, slots, doors);
            Charge = new LowBatteryChargeService(_client, _thresholds);
            ControlLoop = new AgvControlLoop(_client, _options, doors);
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

    public async Task RefreshStatusAsync(CancellationToken ct = default)
    {
        if (_client is null || !IsConnected) return;
        try
        {
            var snap = await _client.GetVehicleSnapshotAsync(cancellationToken: ct).ConfigureAwait(false);
            LastBatteryPercent = snap.Vehicle.Battery;
            LastPosition = snap.Vehicle.CurrentPosition;
            LastSysState = snap.Vehicle.SysState ?? "—";
            LastMoveState = snap.Vehicle.EffectiveMoveState ?? "—";
            if (Move is not null)
                await Move.RefreshAsync(ct).ConfigureAwait(false);
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
