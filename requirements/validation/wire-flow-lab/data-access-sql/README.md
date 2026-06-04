# data-access-sql (SQLite 验证版)

本目录是正式 `requirements/data-access-sql/` 的「SQLite 验证镜像」，供 `WireFlowLab` 验证工具在离线 / mock 模式下端到端跑通 6 个焊丝发放流程。

与正式目录的对应关系：

| 正式目录 | 本镜像 | 说明 |
|---|---|---|
| `sql-catalog/items/mes.*.yaml` | `sql-catalog/items/mes_mock.*.yaml` | 把 MES 的 Oracle 原生 SQL / 存储函数改写为 SQLite 等价语句（mock 表）。原生 Oracle SQL 以 `oracle_sql` 字段保留备查。 |
| （此前缺失的应用库 SQL） | `sql-catalog/items/app.*.yaml` | 补齐 `flow-sql-map` 中 `catalog_status: pending_catalog` / `sql_ids: []` 的应用数据库与格口控制节点的 SQLite SQL。 |
| `schema/tables/*.yaml` | `schema/tables/*.yaml` | MES mock 表 + 新增应用库表（`app_slot` / `app_returned_weight` / `app_operation_log`）。 |
| 正式 `../../flows/flow-sql-map/flows/<id>/nodes/*.yaml`（每节点一文件） | 本目录 `flow-sql-map/flows/<id>/flow.yaml`（每流程一文件） | 为便于流程引擎单步执行，按流程聚合为单个 `flow.yaml`，字段语义与正式 map 一致（`node_order` / `input_mappings` / `outcome_routing` / `sql_ids`），并把缺 SQL 的节点补上 `sql_id`。 |

## 与正式 catalog 的同步记录（findings C-1～C-4）

| 编号 | 本目录修正 |
|---|---|
| C-1 | 无 `status` 字段；拼写问题已在正式 `mes.wire_quota.check_quota` 修正。 |
| C-2 | `schema/tables/v_fw_wip_sublot.yaml`：`name` 与索引一致。 |
| C-3 | `schema/tables/fw_wip_trans.yaml` + `db/schema.sqlite.sql`：含 `dates` 列。 |
| C-4 | `sql-catalog/items/mes_mock.operator.get_by_id.yaml`：输出字段统一为 `operator_name`。 |

详见上级目录 [`findings.md`](../findings.md) 第三节。

## 设计取舍

- **聚合 flow.yaml**：正式 map 把每个节点拆成独立 yaml，验证工具改为「一个流程一个 `flow.yaml`」以方便 `FlowEngine` 解析与单步执行，字段含义保持一致。
- **MES 模式**：`mes_mock.*` 给出 SQLite 等价 SQL；存储函数（`Get_Mat_QuotaCheck` / `FUN_MAT_TRANS_NEW`）在 mock 模式下由界面可配置结果驱动，原生 PL/SQL 保留在 `oracle_sql` 字段，Oracle 模式下直接执行。
- **格口门状态**：门开关由 `MockSlotController` 模拟，并同步写回 `app_slot.door_state`，因此 `app.slot.are_all_doors_closed` 这类 SQL 仍能读到真实门状态。

## 流程清单

1. `operator_return_wire_and_issue_available_wire` — 操作员存入归还焊丝并取出可用焊丝
2. `material_handler_load_available_wire` — 物料员存入可用焊丝
3. `material_handler_open_single_slot` — 物料员打开单个格口
4. `material_handler_open_all_slots` — 物料员打开所有格口
5. `material_handler_open_all_available_wire_slots` — 物料员打开所有含可用焊丝的格口
6. `material_handler_open_all_returned_wire_slots` — 物料员打开所有含归还焊丝的格口
