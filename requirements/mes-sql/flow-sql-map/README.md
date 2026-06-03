# 流程节点 ↔ SQL 映射（拆分目录）

每个业务流程放在 `flows/` 下。简单流程可以继续用单个 `flows/<flow_id>.yaml`；节点较多的流程使用 `flows/<flow_id>/index.yaml` + `flows/<flow_id>/nodes/<node_id>.yaml` 拆分。

## 当前流程

| 文件 | flow_id | 流程图 |
|------|---------|--------|
| [flows/operator_return_wire_and_issue_available_wire/index.yaml](flows/operator_return_wire_and_issue_available_wire/index.yaml) | `operator_return_wire_and_issue_available_wire` | 操作员存入归还焊丝并取出可用焊丝 |
| [flows/material_handler_load_available_wire/index.yaml](flows/material_handler_load_available_wire/index.yaml) | `material_handler_load_available_wire` | 物料员存入可用焊丝 |
| [flows/material_handler_open_all_returned_wire_slots/index.yaml](flows/material_handler_open_all_returned_wire_slots/index.yaml) | `material_handler_open_all_returned_wire_slots` | 物料员打开所有含有归还焊丝的格口 |
| [flows/material_handler_open_all_available_wire_slots/index.yaml](flows/material_handler_open_all_available_wire_slots/index.yaml) | `material_handler_open_all_available_wire_slots` | 物料员打开所有含有可用焊丝的格口 |
| [flows/material_handler_open_all_slots/index.yaml](flows/material_handler_open_all_slots/index.yaml) | `material_handler_open_all_slots` | 物料员打开所有格口 |
| [flows/material_handler_open_single_slot/index.yaml](flows/material_handler_open_single_slot/index.yaml) | `material_handler_open_single_slot` | 物料员打开单个格口 |

索引见 [index.yaml](index.yaml)。`sql_ids` 已与 `mes-sql-catalog/items/` 中的 `id` 对齐。

维护时直接编辑对应流程文件；如果流程已拆分，则编辑 `flows/<flow_id>/nodes/<node_id>.yaml`，并在新增或改名节点时同步维护该流程的 `index.yaml`。新增或改名流程时，同步维护本目录的 [index.yaml](index.yaml)。

拆分流程约定：

- `flows/<flow_id>/index.yaml`：流程级元数据、`node_order` 和 `node_files`。
- `flows/<flow_id>/nodes/<node_id>.yaml`：单个流程节点的 SQL/function/input/decision 映射。

## 节点 I/O 约定

每个访问数据库或调用函数的节点，只用 `input_mappings` 说明入参来自哪个流程节点/上下文字段。
SQL/function 的输出字段以 `mes-sql-catalog/items/` 中的 `outputs` 为准；后续节点如果要使用上游输出，在自己的 `input_mappings.source_node/source_field` 中引用即可，避免双向重复维护。

界面输入、扫码输入、按钮选择等用户输入也要建成独立节点，使用 `access_type: user_input`，并通过 `output_fields` 说明这些字段来自 `source_type: ui_input`。后续 SQL/function 入参再通过 `source_node/source_field` 引用该输入节点。

`source_node` 只填写流程图里的真实节点或上游访问节点；固定值、系统参数、系统上下文、应用数据库上下文分别用 `source_type: constant`、`source_type: system_parameter`、`source_type: system`、`source_type: application_db` 等表达。

- `access_type: user_input`：界面录入、扫码录入、用户选择或用户确认事件；必须写 `output_fields`，字段的 `source_type` 统一为 `ui_input`。
- `access_type: mes_sql`：引用 `mes-sql-catalog/items/` 中的查询 SQL。
- `access_type: mes_function`：引用 `mes-sql-catalog/items/` 中的 PL/SQL/function 调用。
- `access_type: application_db` 或 `application_db_and_slot_control`：应用数据库或格口控制侧访问；如果还没有 catalog 条目，先写 `catalog_status: pending_catalog` 和空 `sql_ids: []`。

常见 `source_type`：

- `system_parameter`：系统配置或运行参数，例如当前发货柜/AGV/设备编号。
- `system`：系统运行上下文，例如会话、登录态、当前服务实例上下文。
