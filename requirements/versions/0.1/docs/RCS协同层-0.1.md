# 0.1 RCS 协同层（简化版）

## 1. 定位

本文件描述 0.1 **RCS 协同层** 的范围（简化实现，非完整一期任务层）。不引用尚未确定的 baseline 任务/RCS 规格文档。

- **实现与接口说明**：一律以 [../wire-cabinet/agv-dispatch-sdk/](../wire-cabinet/agv-dispatch-sdk/) 为准（`AgvDispatch.Sdk` + `docs/`），不在此重复 API 表。
- **业务职责**：发货柜应用负责站点配置、到站门禁、回充策略、与格口/MES 联锁；SDK 只封装调度 REST 与可复用评估逻辑。

```text
站点作业 / 焊丝流程（业务）
        ↕ 门禁、会话、日志
RCS 协同层 0.1（本版本）
        ↕ AgvDispatch.Sdk
调度系统 RCS（HTTP 轮询）
```

## 2. 0.1 包含

| 能力 | 说明 | SDK / 文档 |
|------|------|------------|
| 登录与轮询 | Bearer、单车/订单状态 | [AGV调度SDK使用说明](../wire-cabinet/agv-dispatch-sdk/docs/AGV调度SDK使用说明.md) |
| 手动移动 | 选作业站 → `CreateMoveOrderAsync` | 场景 B，[场景与状态机](../wire-cabinet/agv-dispatch-sdk/docs/AGV调度场景与状态机.md) |
| 到站判定 | `currentPosition == destination` | 场景 D |
| 门开暂停/门关继续 | `Pause` / `Continue` + `IDoorStateProvider` | 场景 C，`AgvControlLoop` |
| 低电量回充 | `CreateChargeOrderAsync` + 充完回驻点 | 场景 F，Sample Scenario05 |
| 急停恢复 | `CancelEmergencyAsync` | 场景 E |
| 移动/回充日志 | 建议落应用库 | 业务实现 |

**必须真实联调 RCS**，见 [需求范围.md](../需求范围.md) §7、[验收清单.md](../验收清单.md) 脚本 3、4。

## 3. 0.1 不包含（详见 [需求范围.md](../需求范围.md) §8）

- 任务池、自动派单、与 MES 任务绑定派车  
- 多点配送、多车调度策略  
- 完整机器人任务层状态机（仅保留控车会话 + 回充状态）

## 4. 业务侧须在应用里实现（SDK 无）

以下不重复 [../wire-cabinet/agv-dispatch-sdk/docs/](../wire-cabinet/agv-dispatch-sdk/docs/) 已有内容，仅列 0.1 验收相关项：

| 项 | 说明 |
|----|------|
| 站点表 | 作业站 + 充电点（[station-config](../wire-cabinet/station-config/)）；充后回上一作业站；`CreateMoveOrderAsync` 的 destination 来源 |
| UI 移动状态机 | `Idle → Sending → Moving → Arrived / Failed / Timeout`（需求范围 §7.1） |
| 到站门禁 | Arrived 且站点匹配才允许 MH/OP 焊丝界面（需求范围 §2.2） |
| 回充策略 | 低电量%、充满%、充后 `CreateMoveOrderAsync(上一作业站)`；充电中**仅禁开格口**，可控车离站/移站 |
| 焊丝/格口联锁 | 出发前门全关 + 开锁中/存取中禁下单；移动中 **每 Tick** `CabinetDoorStateProvider` → 门开 Pause（§4.3，**不用 closeFlag**） |
| 配置 | 复制 [appsettings.example.json](../wire-cabinet/agv-dispatch-sdk/AgvDispatch.Sdk/appsettings.example.json) → 业务项目 |

集成时序与 Agent 说明：[LLM集成指南](../wire-cabinet/agv-dispatch-sdk/docs/LLM集成指南.md)、[AGENTS.md](../wire-cabinet/agv-dispatch-sdk/AGENTS.md)。

### 4.1 仓门联锁与 closeFlag（遗留说明）

| 项 | 0.1 业务 | SDK Sample / 单测 |
|----|----------|-------------------|
| 门状态来源 | Modbus DI + `app_slot`，聚合为 `AllDoorsClosed` / `AnyDoorOpen` | 可用 `DoorStateInput.FromCloseFlag` 模拟 |
| `closeFlag` 位串 | **不使用** | 可选，兼容旧 MainFrm |
| `ExpectedDoorCount` | **不配置**；以 enabled∧wired 格口集合为准 | Sample 默认 24，仅演示 |

`AgvOperationEvaluator` 只消费 `DoorStateInput` 两个布尔量，**不** 向 RCS 上传 closeFlag。

## 5. 验收前须冻结的配置

与 [验收清单.md](../验收清单.md) §7 一致，验收前由现场填写并冻结：`BaseUrl`、`DefaultDeviceKey`、`DefaultMapId`、`ChargeDestination`、`ChargeActionId`、站点表、回充与到站超时阈值。

## 6. 相关文档

| 文档 | 用途 |
|------|------|
| [../wire-cabinet/agv-dispatch-sdk/README.md](../wire-cabinet/agv-dispatch-sdk/README.md) | SDK 入口与工程结构 |
| [../wire-cabinet/agv-dispatch-sdk/docs/AGV调度SDK使用说明.md](../wire-cabinet/agv-dispatch-sdk/docs/AGV调度SDK使用说明.md) | API、配置、REST |
| [../wire-cabinet/agv-dispatch-sdk/docs/AGV调度场景与状态机.md](../wire-cabinet/agv-dispatch-sdk/docs/AGV调度场景与状态机.md) | 场景 A–F |
| [需求范围.md](../需求范围.md) §7 | 0.1 功能要求 |
| [验收清单.md](../验收清单.md) | 演示脚本 3、4 |
| [格口控制层-详细设计.md](格口控制层-详细设计.md) | 0.1 — `GetDoorStateForAgvAsync`、`CabinetDoorStateProvider` |
| [格口硬件控制层-详细设计.md](格口硬件控制层-详细设计.md) | 0.1 — Modbus DI、`ReadInterlockLockStatesAsync` |
| [slot-config/README.md](../wire-cabinet/slot-config/README.md) | 联锁格口集合（enabled∧wired） |
