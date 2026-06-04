# 仓位与 IO 配置（现场真表）

格口**业务需求**以 `requirements/baseline/格口控制层.md`、`格口硬件控制层.md` 为准。  
本目录两张 Excel 是**现场映射数据**，实现时据此生成配置/数据库，**不要**以 `仓位数据结构.md` 为准（该文件仅为早期草稿，可忽略）。

## 文件说明

| 文件 | 作用 |
|------|------|
| [仓位编号.xlsx](仓位编号.xlsx) | **仓门位置 → 格口编号**：`front(row,col)` / `rear(row,col)` 对应 `F-A01`、`前-A01` 等 |
| [IO仓位对应表.xlsx](IO仓位对应表.xlsx) | **IO 点位 → 仓门位置**：各 IO 模块（sheet 名 = 模块 MAC 末两位）上 DO/DI 与仓门位置一一对应 |

### 仓门位置约定

- 格式：`front(行,列)` / `rear(行,列)`，行、列为柜体网格坐标（与编号表一致）。
- **格口业务主键（SlotCode）**：`仓位编号.xlsx` 中的 **英文编号**（如 `F-A01`）。`F`/`R`=前/后柜，字母=行，数字=列；写入应用库 `slot_no`、日志、流程与 `UnlockAsync`。说明见 [0.1 格口硬件概要设计 §3.1](../requirements/versions/0.1/docs/格口硬件控制层-概要设计.md)。

### IO 表约定

- 每个 sheet（`C4`、`24`、`67`、`BE`）对应一块 IO 模块。
- 每行：`DOx` 开锁输出 + `DIx` 状态输入，**指向同一仓门位置**（符合硬件层「每格独立 DO/DI」）。
- 实现时：按 sheet 配置 Modbus 连接；本柜模块 **0-based**，**DO 起始 100、DI 起始 10200**，每模块各 **16** 点（`DOn`→`100+n-1`，`DIn`→`10200+n-1`）。详见 [0.1 格口硬件详细设计 §4.1](../requirements/versions/0.1/docs/格口硬件控制层-详细设计.md)。

## 与 baseline 的衔接

```text
焊丝流程 / 界面
    → 格口控制层（baseline）：用 SlotCode 开锁、业务状态、联锁
    → 格口硬件控制层（baseline）：按 IO 映射发 DO、读 DI
    → 本目录 Excel：SlotCode ↔ 仓门位置 ↔ 模块+DO/DI
```

- **开锁**：对 `SlotCode` 查编号表得 `front(1,1)`，再查 IO 表得模块 `67` 的 `DO1` 等。
- **状态**：读同格 `DI`；本柜 **DI=1 锁闭合、DI=0 锁释放**，释锁后门自动弹开（见 [0.1 硬件详细设计 §5.1](../requirements/versions/0.1/docs/格口硬件控制层-详细设计.md)）。
- **门全关联锁（0.1）**：对「已启用且已接线」格口读 DI，由格口控制层聚合为 `DoorStateInput`（**不用** 旧版 `closeFlag` 位串，见 [0.1 格口控制层详细设计](../requirements/versions/0.1/docs/格口控制层-详细设计.md) §4）。

## 数据核对状态（最近复核）

| 项 | 状态 |
|----|------|
| 格口总数 | **54**（`仓位编号.xlsx`） |
| IO 覆盖 | **54** 个仓门位置均有 DO/DI，与编号表 **一一对应** |
| 同位置多模块 | **已消除**（此前 `front(1,3)` 双绑已修复） |
| 有编号无 IO / 有 IO 无编号 | **无** |
| `F-B01` 重复 | **已修复**（英文编号已唯一化） |
| `R-B01` 重复 | **已修复** → `R-B01` / `R-B02` / `R-B03` |
| BE 模块 `undefined` | `DO7`～`DO16` 未接线，**保留即可**；有效映射 **6** 对（`DO1`～`DO6`） |
| RCS 门联锁 | 已接线且启用 **54** 格均关好 → `AllDoorsClosed`；任一格开 → `AnyDoorOpen` |

**复核结论（最近一次）**：54 格编号唯一、54 路 IO 一一对应、无跨模块重复绑定，**可导入配置**。

## 0.1 验收

- 格口验收以 baseline + [验收清单](../requirements/versions/0.1/验收清单.md) §3 为准。
- IO 配置来源为本目录两张 xlsx（或其导出的 yaml/json），不再单独维护一份与草稿 C# 模型不一致的「假 IO 表」。
- **已导出配置**：[requirements/versions/0.1/wire-cabinet/slot-config/](../requirements/versions/0.1/wire-cabinet/slot-config/)（`python wire-cabinet/slot-config/_tools/export_slot_config.py` 可重新生成）。
- **0.1 设计文档**：[requirements/versions/0.1/docs/](../requirements/versions/0.1/docs/)
