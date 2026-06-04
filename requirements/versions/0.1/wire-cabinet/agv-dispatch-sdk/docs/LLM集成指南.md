# LLM 集成指南（AgvDispatch.Sdk）

> **0.1 焊丝柜生产**：门状态由 [格口控制层-详细设计.md](../../docs/格口控制层-详细设计.md) 的 `GetDoorStateForAgvAsync` 聚合 Modbus DI + `app_slot`，**不要** 使用下文 `FromCloseFlag` / `closeFlag`（仅兼容旧上位机或 Sample）。

## 标准轮询伪代码

```csharp
var thresholds = options.StateThresholds;
var pollMs = options.RecommendedPollIntervalMs;
bool pausedForDoor = false;
bool callInProgress = false;

while (cancellationToken.IsCancellationRequested == false)
{
    var snapshot = await client.GetVehicleSnapshotAsync(ct);
    var doors = DoorStateInput.FromCloseFlag(yourCloseFlagFromPlc, thresholds);
    var eval = AgvOperationEvaluator.Evaluate(
        snapshot.Vehicle, snapshot.Order, doors, thresholds,
        previouslyPausedForDoor: pausedForDoor,
        callInProgress: callInProgress);

    if (eval.RecommendsCancelOrder && snapshot.Vehicle.OrderTaskId != null)
    {
        await client.CancelOrderAsync(snapshot.Vehicle.OrderTaskId, ct);
        callInProgress = false;
    }
    else if (eval.ShouldPauseForDoor)
    {
        await client.PauseMovementAsync(ct: ct);
        pausedForDoor = true;
    }
    else if (eval.ShouldContinueAfterDoorClosed)
    {
        await client.ContinueMovementAsync(ct: ct);
        pausedForDoor = false;
    }
    else if (businessWantsDispatch && eval.CanAcceptOrder)
    {
        await client.CreateMoveOrderAsync(destinationStationId, ct: ct);
        callInProgress = true;
    }

    await Task.Delay(pollMs, ct);
}
```

## 决策矩阵

| 有 OrderTaskId | order_state | sys_state | 门 | callInProgress | 调用 |
|----------------|-------------|-----------|-----|----------------|------|
| 是 | 9 | * | * | * | CancelOrderAsync |
| 是 | 1 | * | * | * | 仅轮询 |
| 是 | 其他 | * | * | * | 仅轮询 |
| 否 | - | ERROR | * | * | CancelEmergencyAsync（按需） |
| 否 | - | CHARGING | * | * | 禁止下单 |
| 否 | - | 非 IDLE | 有门开 | * | PauseMovementAsync |
| 否 | - | * | 全关且曾暂停 | * | ContinueMovementAsync |
| 否 | - | IDLE + move/proc 就绪 | 全关 | false | CreateMoveOrderAsync |

阈值字符串见 `AgvStateThresholds`（可配置）。

## 方法契约表

| 方法 | 前置条件 | 后置 | 幂等 | REST |
|------|----------|------|------|------|
| LoginAsync | BaseUrl 可达 | Bearer 缓存 | 可重复 | POST /api/auth/v1/admin/login |
| GetVehicleInfoAsync | 已登录 | 单车 DTO | 是 | GET .../getVehicleInfoByDeviceKey |
| GetOrderDetailAsync | orderId 非空 | 订单 DTO | 是 | GET .../detailByOrderId/{id} |
| CreateMoveOrderAsync | destination≠0 | 可能有 orderId | 否 | POST .../add/byDefaultMissions |
| CancelOrderAsync | orderId 有效 | 订单取消中 | 谨慎重试 | POST .../order/command/{id} |
| PauseMovementAsync | 车在执行 | 运动暂停 | 可重复 | POST .../pauseMovement |
| ContinueMovementAsync | 曾暂停 | 继续 | 可重复 | POST .../continueMovement |
| CancelEmergencyAsync | 急停态 | 解除流程 | 可重复 | POST .../batchServiceSet |
| GetVehiclesAsync | deviceIds 已配置 | 列表 | 是 | GET .../vehicles |
| CreateChargeOrderAsync | 充电参数已配置 | 充电单 | 否 | 同 add 接口 |

## 反模式

1. 未读车状态直接 `CreateMoveOrderAsync`。
2. 门打开时只下单不 `PauseMovementAsync`。
3. `order_state=9` 不取消再次下单。
4. 每次循环 `new HttpClient`（应 DI 或单例）。
5. 把仓位名当 `destination` 传入。
6. 在代码写死 IP / mapId / deviceKey。

## 术语

- **destination**：地图站点数字 ID。
- **deviceKey**：调度车辆唯一键，不是 IP。
- **OrderTaskId**：调度订单 ID，不等于本地 SQL Job 表主键。
- **closeFlag**（遗留）：位串，`1`=关 `0`=开；0.1 业务改用 `DoorStateInput { AllDoorsClosed, AnyDoorOpen }` 直接赋值。

## 不要自行推断

- 站点号来源（ini/SQL/配置中心）。
- 调度是否需代理/证书。
- 多车时选哪台 `deviceKey`。
