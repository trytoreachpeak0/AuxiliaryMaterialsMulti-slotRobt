# AGV 调度场景与状态机

本文从 [`StandAGVClient/MainFrm.cs`](../StandAGVClient/MainFrm.cs) 提取 **调度侧** 行为，供其他 .NET 8+ 项目复现。

## 角色边界

| 组件 | 职责 |
|------|------|
| AgvDispatch.Sdk | 调度 REST + `AgvOperationEvaluator` |
| 业务项目 | 仓门 Modbus、SQL Job、MES、UI |

## 字段百科（常用）

### sys_state（系统）

| 值 | 含义 | 下单 |
|----|------|------|
| IDLE | 空闲 | 可能可下单 |
| EXECUTING | 忙碌 | 通常不可；门开需暂停 |
| ERROR | 异常 | 先急停解除 |
| CHARGING | 充电 | Sample 默认不可下单；**0.1 业务允许充电中控车/离站**，仅禁开格口（见 [需求范围.md](../../需求范围.md) §7.2） |
| PAUSE | 避障 | 不可下单 |

### actionState / movementState（移动）

| 值 | 含义 |
|----|------|
| AT_FINISHED | 动作已结束 |
| AT_NA | 空闲 |
| AT_RUNNING | 执行中 |
| AT_PAUSED | 暂停中 |

### proc_state（进程）

| 值 | 可接单辅助 |
|----|------------|
| IDLE | 是 |
| INNER_FORCE_IDLE | 是 |

### order_state

| 值 | 动作 |
|----|------|
| 1 | 已下待执行，禁止新单 |
| 9 | 应 CancelOrderAsync |

## 场景 A — 轮询 / 是否可接单

对应 `getVehstatus()` + `GetVehicleSnapshotAsync`。

1. 拉单车状态；有 `OrderTaskId` 则拉订单。
2. `order_state == 9` → 取消。
3. 有单 → 不可接单。
4. 无单：在线 + IDLE + move/proc 就绪 + **门全关** → 可接单。

## 场景 B — 下新单

对应 `GetTask` + `ADDORDER` 前置。

- `AgvOperationEvaluator.CanAcceptOrder == true`
- `callInProgress == false`
- `CreateMoveOrderAsync(destination)`，`destination` 来自业务配置（原 QXPoint 等 **不在 SDK 默认值中**）。

## 场景 C — 门开暂停 / 门关继续

对应 `AGVWork` 循环 252–261 行。

- 车 **非 IDLE** 且门含 `0` → `PauseMovementAsync`
- 门全 `1` 且曾暂停 → `ContinueMovementAsync`

## 场景 D — 到站

对应 `AGVARRIVALTODO` 调度部分。

- `currentPosition ==` 下单时锁定站点
- 开门/MES 为业务逻辑

## 场景 E — 急停

- `sys_state == ERROR` → `CancelEmergencyAsync`（可配置 messageId）

## 场景 F — 充电

1. `GetVehiclesAsync` + `IsChargeStationOccupied`
2. 未占用且可接单 → `CreateChargeOrderAsync`
3. 参数均来自 `AgvDispatchOptions`

## 场景 G — 推荐时序

见 [LLM集成指南.md](LLM集成指南.md) 伪代码。

## 与原方法对照

| 原方法 | 场景 |
|--------|------|
| getVehstatus | A |
| GetTask / ADDORDER | B |
| AGVWork 暂停/继续 | C |
| AGVARRIVALTODO | D |
| CancelEMENGENCY | E |
| AGVDockPro | F |
