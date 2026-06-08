namespace WireCabinet.Core.Config;

public sealed class ApplicationDatabaseOptions
{
    public string Path { get; set; } = "";
}

public sealed class MesOptions
{
    public string ConnectionString { get; set; } = "";
    public string MatTransWriter { get; set; } = "新厂前线物料多仓位2";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
}

public sealed class AgvThresholdOptions
{
    public int LowBatteryPercent { get; set; } = 20;
    public int FullBatteryPercent { get; set; } = 95;
    public int ArrivalTimeoutSeconds { get; set; } = 600;
}

public sealed class SlotHardwareOptions
{
    public bool Enabled { get; set; } = true;
    public int UnlockPulseMs { get; set; } = 200;
    public int BatchIntervalMs { get; set; } = 400;
}

public sealed class FlowTraceWindowOptions
{
    public bool Enabled { get; set; }
    public int MaxEntriesPerFlow { get; set; } = 1000;
}
