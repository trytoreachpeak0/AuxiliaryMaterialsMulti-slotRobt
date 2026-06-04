# AGV 调度 SDK 使用说明

## 环境

- **.NET 8.0+**
- 网络可达调度 `BaseUrl`（默认端口 8888）
- 复制 [appsettings.example.json](../AgvDispatch.Sdk/appsettings.example.json) 为 `appsettings.json` 并填写

## 引用

```xml
<ProjectReference Include="..\AgvDispatch.Sdk\AgvDispatch.Sdk.csproj" />
```

## 配置项（AgvDispatch 节）

| 键 | 说明 |
|----|------|
| BaseUrl | 调度根 URL |
| Username / Password | 登录 |
| DefaultDeviceKey | 车辆 key |
| DefaultMapId | 地图 ID |
| DefaultOrderName | 下单名称 |
| VehicleQueryDeviceIds | 多车查询 ID 数组 |
| ChargeDestination / ChargeActionId | 充电模板 |
| ChargeOccupiedStationNames / ChargeOccupiedPositions | 充电占用判断 |
| PauseMovementMessageId / ContinueMovementMessageId | 暂停/继续 body |
| CancelEmergencyMessageId | 急停解除 |
| StateThresholds | 状态判断字符串/订单 state |

校验：构造客户端前调用 `options.Validate()`。

## DI 注册（.NET 8 推荐）

```csharp
services.AddAgvDispatchClient(o =>
    configuration.GetSection(AgvDispatchOptions.SectionName).Bind(o));
// 注入 IAgvDispatchClient
```

无 DI 时：`using var client = AgvDispatchClient.Create(options);`

## API 与 REST

| SDK | HTTP |
|-----|------|
| LoginAsync | POST /api/auth/v1/admin/login |
| GetVehicleInfoAsync | GET /api/task/vehicles/getVehicleInfoByDeviceKey |
| GetVehiclesAsync | GET /api/task/vehicles |
| GetOrderDetailAsync | GET /api/order/v1/orderRecord/detailByOrderId/{id} |
| CreateMoveOrderAsync | POST /api/order/v1/add/byDefaultMissions |
| CreateChargeOrderAsync | 同上（move+act） |
| CancelOrderAsync | POST /api/task/v1/order/command/{id} |
| PauseMovementAsync | POST .../pauseMovement |
| ContinueMovementAsync | POST .../continueMovement |
| CancelEmergencyAsync | POST /api/device/v1/command/batchServiceSet |

## 示例：查询 + 评估

```csharp
var snapshot = await client.GetVehicleSnapshotAsync();
var doors = DoorStateInput.FromCloseFlag(closeFlagFromPlc, options.StateThresholds);
var eval = AgvOperationEvaluator.Evaluate(
    snapshot.Vehicle, snapshot.Order, doors, options.StateThresholds);
```

## DTO 说明

- `VehicleInfoDto`：单车；`OrderTaskId` 有值表示有单。
- `EffectiveMoveState`：`actionState` 优先于 `movementState`。
- `OrderDetailDto.EffectiveOrderState`：兼容 camelCase / snake_case。
- 调度若返回更多 JSON 字段，保留在 `ExtensionData` 或完整属性上。

## 场景文档

完整控车流程见 [AGV调度场景与状态机.md](AGV调度场景与状态机.md)。  
LLM/Agent 见 [LLM集成指南.md](LLM集成指南.md) 与根目录 [AGENTS.md](../AGENTS.md)。

## Sample

```bash
cd AgvDispatch.Sample
dotnet run
```

需可访问调度服务，否则仅演示异常提示。
