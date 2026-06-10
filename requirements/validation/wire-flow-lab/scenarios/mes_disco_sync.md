# DISCOEQPNO 同步场景（wire-flow-lab / mock）

## 前置

- 使用 SQLite mock MES（`v_fw_material_indetail.DISCOEQPNO` 列）。
- 小车名与 `Mes:MatTransWriter` 一致。

## 场景 1：MH 存丝 SET 成功

1. 执行 MH 存料流程至 `bind_wire` 成功。
2. 验证 mock 表对应 `sublot` 的 `DISCOEQPNO` 为车名。

## 场景 2：MES 瞬态失败 → pending → 后台重试

1. 人为使 MES UPDATE 失败（如断开 mock 表或改错 sublot）。
2. 流程应失败并提示「系统将自动重试…」。
3. 检查 `wire_mes_disco_sync` 有 `pending` + `failure_kind=inline`。
4. 恢复 MES 后等待重试或手动触发 `RetryPendingBatch`，应变为 `success`。

## 场景 3：OP 领用 CLEAR

1. 柜内有 `available_wire`，走 OP 领用至 `complete_issue_pickup`。
2. 验证 `DISCOEQPNO` 为 NULL。

## 场景 4：崩溃快照不自动修复

1. 领用开门后（`op_interrupted_issue` 存在）Kill 进程。
2. 重启应弹 OP 提醒，**不**自动清库 / CLEAR。
3. 后台重试应跳过该批号。

## 场景 5：Maint 试开隔离

1. Maint 试开格口，关门 `assume_empty`（MH door only）若原为 `available_wire` 才 CLEAR。
2. `maint_trial` 路径不触发 MES（Maint 页面试开不走 bind/assume_empty 业务链）。

## 场景 6：非中断漂移对账

1. 手工改 mock 表 DISCOEQPNO 与柜内库存不一致（无中断快照）。
2. MES 对账「刷新对账」应显示差集；「非中断项入队重试」应写入 pending 并由后台修复。

## 场景 7：中断相关漂移仅展示

1. 存在 MH/OP 中断快照且库存/MES 不一致。
2. 对账差集标注「仅人工」；后台 **不** 自动 SET/CLEAR。
