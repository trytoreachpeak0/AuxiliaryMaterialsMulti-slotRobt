using WireFlowLab.Data;
using WireFlowLab.SlotControl;

namespace WireFlowLab.Flows;

/// <summary>流程引擎依赖的服务集合。Mes 可在运行时随模式切换被替换。</summary>
public sealed class EngineServices
{
    public required SqlCatalog Catalog { get; init; }
    public required IAppDb AppDb { get; init; }
    public required IMesGateway Mes { get; set; }
    public required ISlotController Slot { get; init; }
    public string SystemAgvNo { get; set; } = "AGV-01";
    public string SystemMesWriter { get; set; } = "新厂前线物料多仓位2";
}
