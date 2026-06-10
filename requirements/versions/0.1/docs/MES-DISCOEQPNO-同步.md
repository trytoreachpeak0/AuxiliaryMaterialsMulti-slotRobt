# DISCOEQPNO MES 同步

## 业务规则

- **SET**：可用焊丝 `bind_wire` 成功后，更新 Oracle `fw_material_indetail.DISCOEQPNO` 为配置项 `Mes:MatTransWriter`（默认「新厂前线物料多仓位2」）。
- **CLEAR**：`available_wire` 清格成功后（领用 `complete_issue_pickup` 或 MH 纯开门 `assume_empty`），将 `DISCOEQPNO` 置 `NULL`。
- **不触发**：归还焊丝路径、`maint_trial` 试开。
- **时序**：本地 SQLite 写库成功后再调 MES；MES 失败写入 `wire_mes_disco_sync` pending，**不回滚**本地库。

## 挂接点（代码集中）

| 事件 | 挂接位置 |
|------|----------|
| SET | `FlowEngine.ExecWrite` 在 `app.slot.bind_wire` 成功后 |
| CLEAR（领用） | `FlowEngine.ExecWrite` 在 `app.slot.complete_issue_pickup` 前读库存、成功后 CLEAR |
| CLEAR（MH 开门） | `MhDoorOnlyService.AssumeEmptyAsync` 清格前读 `app.slot.get_inventory` |

## 后台重试

- 启动时 `RetryPendingBatch()` 一次；运行中每 2 分钟扫描 pending。
- 存在 `mh_interrupted_load` / `op_interrupted_issue` 的批号 **禁止** 自动 SET/CLEAR。
- 指数退避：30s → 2min → 5min…，最多 5 次后标 `failed`。

## 崩溃 / 中断（仅提醒，不自动恢复）

- **MH**：`mh_interrupted_load` 扩展 `bind_done` / `mes_disco_done`；门已关重启后弹窗 → 清除快照 → **必须重新扫码**。
- **OP**：`op_interrupted_issue` 在领用开门后写入；重启弹窗提醒，**不**自动 `complete_issue_pickup` / CLEAR。

## MES 对账界面

顶栏第 5 标签「MES 对账」，与 MAINT 同密码。功能：

1. Pending / failed 列表：手动重试、标记已人工处理
2. 库存 vs MES 漂移：刷新、非中断项入队重试
3. MH/OP 中断快照只读 + 人工清除
4. 按批号手工 SET / NULL + `mes_recon_audit_log`

Maint 界面不涉及 MES / 对账。

## SQL Catalog

- `mes.wire.set_disco_eqp_no` / `mes.wire.clear_disco_eqp_no`
- `mes.wire.query_disco_by_lot` / `mes.wire.list_by_disco_eqp_no`
- `app.slot.get_inventory`
- Lab mock：`mes_mock.wire.*` 对应条目
