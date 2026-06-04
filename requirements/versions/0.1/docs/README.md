# 0.1 设计文档

本目录为 **实现级设计**（概要 + 详细）；版本范围与验收见上级目录。

| 文档 | 说明 |
|------|------|
| [HMI界面约束.md](HMI界面约束.md) | 一体机 1024×768、触控、主题、角色壳 |
| [OP界面-分步交互规范.md](OP界面-分步交互规范.md) | OP 存废取新分步 UI |
| [MH界面-分步交互规范.md](MH界面-分步交互规范.md) | MH 存料分步 UI |
| [格口控制层-概要设计.md](格口控制层-概要设计.md) | 业务层：`app_slot`、会话互斥、联锁、与 flow 对接 |
| [格口控制层-详细设计.md](格口控制层-详细设计.md) | 接口、状态机、SQL 映射、`GetDoorStateForAgvAsync` |
| [格口硬件控制层-概要设计.md](格口硬件控制层-概要设计.md) | Modbus、SlotCode、DI/DO 语义 |
| [格口硬件控制层-详细设计.md](格口硬件控制层-详细设计.md) | `ISlotHardwareService`、寻址 100/10200、开锁时序 |
| [RCS协同层-0.1.md](RCS协同层-0.1.md) | 0.1 控车/回充范围；实现见 `../wire-cabinet/agv-dispatch-sdk/` |
| [开发待补齐信息.md](开发待补齐信息.md) | **验收前须现场提供 / 开发仍缺项**（清单） |
| [../wire-cabinet/station-config/README.md](../wire-cabinet/station-config/README.md) | 作业站/充电点、到站门禁、与 flow 绑定 |

## 阅读顺序（开发）

1. [../需求范围.md](../需求范围.md) — 做什么、不做什么  
2. [HMI界面约束.md](HMI界面约束.md) — 界面尺寸与触控（实现 UI 前必读）  
3. [格口硬件控制层-概要设计.md](格口硬件控制层-概要设计.md) → [详细设计](格口硬件控制层-详细设计.md)  
4. [格口控制层-概要设计.md](格口控制层-概要设计.md) → [详细设计](格口控制层-详细设计.md)  
5. [OP界面-分步交互规范.md](OP界面-分步交互规范.md) / [MH界面-分步交互规范.md](MH界面-分步交互规范.md) — 焊丝页交互  
6. [RCS协同层-0.1.md](RCS协同层-0.1.md) + [../wire-cabinet/agv-dispatch-sdk/README.md](../wire-cabinet/agv-dispatch-sdk/README.md)  
7. [../wire-cabinet/slot-config/README.md](../wire-cabinet/slot-config/README.md)  
8. [开发待补齐信息.md](开发待补齐信息.md) — 联调前核对  

## 仓库内关联（不在本目录）

| 路径 | 用途 |
|------|------|
| `../../flows/flow-sql-map/` | 6 条流程节点定义 |
| `../../flows/diagrams/` | 流程图 |
| `../../data-access-sql/` | MES SQL（正式 catalog） |
| `../../validation/wire-flow-lab/` | SQLite 验证、mock MES、`app.*` SQL 草案 |
| `../../../仓位信息/` | 现场 xlsx → 导出 `../wire-cabinet/slot-config/` |
| `../../baseline/` | 母版需求（0.1 取子集） |
