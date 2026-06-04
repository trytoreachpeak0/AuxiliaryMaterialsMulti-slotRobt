# 0.1 格口配置（slot-config）

现场格口编号与 IO 映射的**机器可读配置**，由 [仓位信息/仓位编号.xlsx](../../../仓位信息/仓位编号.xlsx) 与 [IO仓位对应表.xlsx](../../../仓位信息/IO仓位对应表.xlsx) 导出。

## SlotCode（`slot_code`）是什么？

- **含义**：一格焊丝柜仓口的**业务主键**（英文短码），例如 `F-A01`、`R-B02`。
- **规则**：`F`/`R` = 前/后柜；中间字母 = 行（A、B、C…）；数字 = 列。
- **用途**：`UnlockAsync(slotCode)`、流程格口字段、HMI、操作日志；**不要**用裸 `A01`（无法区分前后柜）。
- **配置字段**：`slots.generated.yaml` 里为 `slot_code`；`slot-io-mapping.generated.yaml` 用其关联 `module_key`、`do_index`、`di_index`。
- **对照**：同表还有 `door_position`（如 `front(1,1)`）用于对 IO 表；`slot_index` 仅排序，不是主键。

- **主键**：库表用 `slot_id`；SlotCode 为唯一业务名（存 `slot_no`），见 [§3.2](../格口硬件控制层-概要设计.md)。

## IO 模块标识与 IP

- xlsx 的 sheet 名仍是 **MAC 末两位**（`C4` / `24` / `67` / `BE`）。
- 配置里的 `module_key` 使用 **完整 MAC**，由 [module-key-map.yaml](module-key-map.yaml) 把 sheet 名映射过去；`io-modules.yaml` 的 `modules[].key` 须与之一致。
- xlsx **不含 IP**。现场提供每块模块的 Modbus 地址后：

1. 在 `module-key-map.yaml` 填写四块模块的真实 MAC
2. 复制 `io-modules.template.yaml` → `io-modules.yaml`，`modules[].key` 与映射表一致
3. 填写各 `modules[].host`（及必要时 `port`）
4. 修改 xlsx 或映射表后执行 `export_slot_config.py` 重新生成 `slot-io-mapping.generated.yaml`

见 [格口硬件控制层-概要设计 §3.3](../格口硬件控制层-概要设计.md)。

完整 SlotCode 说明：[§3.1](../格口硬件控制层-概要设计.md)。

## 文件

| 文件 | 说明 |
|------|------|
| [slots.generated.yaml](slots.generated.yaml) | 54 格：`slot_code`、`door_position`、`slot_index`、显示名 |
| [slot-io-mapping.generated.yaml](slot-io-mapping.generated.yaml) | 每格 `module_key`、`do_index`、`di_index`、`wired` |
| [module-key-map.yaml](module-key-map.yaml) | xlsx sheet（MAC 末两位）→ 完整 MAC `module_key` |
| [io-modules.template.yaml](io-modules.template.yaml) | Modbus 连接模板；`modules[].key` 为完整 MAC；复制为 `io-modules.yaml` 并填 IP |
| [VALIDATION.md](VALIDATION.md) | 最近一次导出校验摘要 |

## 重新生成

```bash
python requirements/versions/0.1/wire-cabinet/slot-config/_tools/export_slot_config.py
```

修改 xlsx 后应重新执行，并提交更新后的 `*.generated.yaml` 与 `VALIDATION.md`。

## 与 RCS 门联锁（0.1，不用 closeFlag）

0.1 **不** 使用旧上位机的 `closeFlag` 位串。联锁逻辑为：

1. **集合 S**：`is_enabled ∧ io_wired` 的格口（全柜接线且全启用时 **S 共 54 格**）。
2. 读取 S 内每格 DI（`ReadInterlockLockStatesAsync`）并查询 `app_slot.door_state`。
3. 聚合为 `DoorStateInput`：
   - `AllDoorsClosed`：S 中全部锁止且 DB 无 `open`
   - `AnyDoorOpen`：任一格 **DI=0**（锁释放，门自动开）或 DB 为 `open`

`slot_index` 仅用于 **批量开锁顺序** 与界面排序，与 RCS 无关。

实现见 [格口控制层-详细设计.md](../docs/格口控制层-详细设计.md) §4。

## 设计文档

见 [docs/](../docs/)（格口概要/详细设计、RCS、[开发待补齐信息.md](../docs/开发待补齐信息.md)）。

母版需求：[baseline/格口硬件控制层.md](../../baseline/格口硬件控制层.md)、[baseline/格口控制层.md](../../baseline/格口控制层.md)。
