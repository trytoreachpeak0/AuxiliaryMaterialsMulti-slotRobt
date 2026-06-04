using AgvDispatch.Sdk.Operations;

namespace WireCabinet.Slots;

public interface IDoorStateProvider
{
    Task<DoorStateInput> GetDoorStateAsync(CancellationToken cancellationToken = default);
}

public sealed class CabinetDoorStateProvider : IDoorStateProvider
{
    private readonly ISlotControlService _slots;

    public CabinetDoorStateProvider(ISlotControlService slots) => _slots = slots;

    public Task<DoorStateInput> GetDoorStateAsync(CancellationToken cancellationToken = default) =>
        _slots.GetDoorStateForAgvAsync(cancellationToken);
}
