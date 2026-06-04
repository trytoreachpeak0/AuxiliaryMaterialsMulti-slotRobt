namespace WireCabinet.Hmi.Services;

/// <summary>维护 MAINT 界面密码门禁；配置密码为空时不启用。</summary>
public sealed class MaintAccessGate
{
    private readonly string _password;

    public MaintAccessGate(string? configuredPassword)
    {
        _password = configuredPassword ?? "";
    }

    public bool IsGateEnabled => !string.IsNullOrEmpty(_password);

    public bool IsUnlocked { get; private set; }

    public bool TryUnlock(string? input)
    {
        if (!IsGateEnabled)
        {
            IsUnlocked = true;
            return true;
        }

        if (string.Equals(input, _password, StringComparison.Ordinal))
        {
            IsUnlocked = true;
            return true;
        }

        return false;
    }

    public void Lock() => IsUnlocked = false;
}
