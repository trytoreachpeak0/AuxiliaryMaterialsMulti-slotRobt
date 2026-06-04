namespace WireCabinet.Slots;

/// <summary>全 HMI 同一时间仅允许一个开门动作（单格或批量）。</summary>
public sealed class DoorOperationGate
{
    private readonly object _lock = new();
    private string? _activeLabel;

    public bool IsBusy
    {
        get { lock (_lock) return _activeLabel is not null; }
    }

    public string? ActiveLabel
    {
        get { lock (_lock) return _activeLabel; }
    }

    public event EventHandler? BusyChanged;

    public bool TryEnter(string label, out string busyMessage)
    {
        lock (_lock)
        {
            if (_activeLabel is not null)
            {
                busyMessage = $"当前正在「{_activeLabel}」，请等待完成。";
                return false;
            }

            _activeLabel = label;
        }

        busyMessage = "";
        BusyChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Exit()
    {
        lock (_lock)
        {
            if (_activeLabel is null)
                return;
            _activeLabel = null;
        }

        BusyChanged?.Invoke(this, EventArgs.Empty);
    }
}
