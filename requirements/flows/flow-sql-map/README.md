# 流程可执行映射（flow-sql-map）

本目录与 [`diagrams/`](../diagrams/) 下的 Mermaid 流程图一一对应，描述每个流程节点的类型、分支路由、入参来源，以及通过 `sql_ids` 引用 [`data-access-sql/sql-catalog`](../data-access-sql/sql-catalog/) 中的 SQL。

每个业务流程放在本目录的 `flows/` 下。简单流程可以继续用单个 `flows/<flow_id>.yaml`；节点较多的流程使用 `flows/<flow_id>/index.yaml` + 节点 YAML 拆分。

节点目录与 Mermaid 流程图 `subgraph` 对齐，详见 **[SUBGRAPH-NODES.md](SUBGRAPH-NODES.md)**（Cursor 规则：`.cursor/rules/flow-sql-map-subgraph-nodes.mdc`）。

```text
flows/<flow_id>/
  index.yaml
  nodes/<subgraph_id>/<node_id>.yaml   # subgraph_id 与 diagrams 中 subgraph id 一致；嵌套时取叶子 subgraph
```

## 当前流程

| 文件 | flow_id | 流程图 |
|------|---------|--------|
| [flows/operator_return_wire_and_issue_available_wire/index.yaml](flows/operator_return_wire_and_issue_available_wire/index.yaml) | `operator_return_wire_and_issue_available_wire` | 操作员存入归还焊丝并取出可用焊丝 |
| [flows/material_handler_load_available_wire/index.yaml](flows/material_handler_load_available_wire/index.yaml) | `material_handler_load_available_wire` | 物料员存入可用焊丝 |
| [flows/material_handler_open_all_returned_wire_slots/index.yaml](flows/material_handler_open_all_returned_wire_slots/index.yaml) | `material_handler_open_all_returned_wire_slots` | 物料员打开所有含有归还焊丝的格口 |
| [flows/material_handler_open_all_available_wire_slots/index.yaml](flows/material_handler_open_all_available_wire_slots/index.yaml) | `material_handler_open_all_available_wire_slots` | 物料员打开所有含有可用焊丝的格口 |
| [flows/material_handler_open_all_slots/index.yaml](flows/material_handler_open_all_slots/index.yaml) | `material_handler_open_all_slots` | 物料员打开所有格口 |
| [flows/material_handler_open_single_slot/index.yaml](flows/material_handler_open_single_slot/index.yaml) | `material_handler_open_single_slot` | 物料员打开单个格口 |

索引见 [index.yaml](index.yaml)。`sql_ids` 已与 [`../data-access-sql/sql-catalog/items/`](../data-access-sql/sql-catalog/items/) 中的 `id` 对齐。

维护时直接编辑对应流程文件；如果流程已拆分，则编辑 `flows/<flow_id>/nodes/<subgraph_id>/<node_id>.yaml`（无 subgraph 时用 `nodes/<node_id>.yaml`），并在新增或改名节点时同步维护该流程的 `index.yaml`（含 `sections` 与 `node_files`）。新增或改名流程时，同步维护本目录的 [index.yaml](index.yaml)。

拆分流程约定：

- `flows/<flow_id>/index.yaml`：流程级元数据、`sections`（可选）、`node_order` 和 `node_files`。
- `flows/<flow_id>/nodes/<subgraph_id>/<node_id>.yaml`：单个流程节点映射；`subgraph_id` 与 Mermaid 流程图 subgraph ID 对齐。

## 节点 I/O 约定

每个节点维护两类信息（不必对称命名为 input/output mappings）：

| 字段 | 作用 |
|---|---|
| `input_mappings` | 本节点执行**需要**哪些入参、从哪来（`source_node` + `source_field` 或 `source_type`） |
| `output_fields` | 本节点执行后流程上下文**会出现**哪些逻辑字段，供下游引用；并标明字段**来源**（catalog / 界面 / 应用库 / 运行时） |

下游在 `input_mappings` 中引用上游时，`source_field` 必须能在上游节点的 `output_fields.name` 中找到（SQL 节点则与 catalog `outputs.name` 一致）。

### output_fields 子字段

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | string | 是 | 流程上下文逻辑字段名，下游 `source_field` 引用此名。 |
| `source_type` | string | 是 | `sql_catalog` / `ui_input` / `application_db` / `slot_control` / `runtime` / `context` |
| `source_ref` | string | 条件 | `source_type: sql_catalog` 时填 catalog `id`（与节点 `sql_ids` 一致）；正式目录未入库时可配合节点级 `catalog_status: pending_catalog`。 |
| `catalog_field` | string | 条件 | 对应 catalog `outputs.name`；不重复抄写 type/meaning。 |
| `source_text` | string | 推荐 | 人能读懂的来源与业务含义。 |

示例（MES 查询节点）：

```yaml
output_fields:
  - name: lot
    source_type: sql_catalog
    source_ref: mes.product.get_latest_product_lot_by_eqp
    catalog_field: lot
    source_text: 机台最近生产产品批号；无数据时 SQL NVL 为 N/A。
```

示例（界面输入，已有写法保持不变）：

```yaml
output_fields:
  - name: operator_id
    source_type: ui_input
    source_text: 操作员输入工号。
```

**纯路由型 `decision` 节点**（不产生新字段、只读上游做分支）可不写 `output_fields`。**读库并产出字段的 decision**（如 `checkAvailableSlot` 选出格口）应写出产出字段。

SQL 正文与 `outputs` 类型定义仍以 [`../data-access-sql/sql-catalog/`](../data-access-sql/sql-catalog/) 为权威；`output_fields` 只做流程内索引与溯源，不复制 SQL。

`source_node` 只填写流程图里的真实节点或上游访问节点；固定值、系统参数、系统上下文、应用数据库上下文分别用 `source_type: constant`、`source_type: system_parameter`、`source_type: system`、`source_type: application_db` 等表达。

- `access_type: user_input`：必须写 `output_fields`，`source_type: ui_input`。
- `access_type: mes_sql` / `mes_function`：必须写 `output_fields`（`source_type: sql_catalog` + `source_ref` + `catalog_field`），并维护 `sql_ids`。
- `access_type: application_db` / `application_db_and_slot_control`：必须写 `output_fields`；catalog 未入正式目录时 `source_ref` 写计划中的 `app.*` id，节点级可保留 `catalog_status: pending_catalog`。

常见 `source_type`：

- `system_parameter`：系统配置或运行参数，例如当前发货柜/AGV/设备编号。
- `system`：系统运行上下文，例如会话、登录态、当前服务实例上下文。

## 引用关系

```text
flow-sql-map/flows/<flow_id>/nodes/**/*.yaml ──(sql_ids)──> data-access-sql/sql-catalog/items/*.yaml
                                                                  └──(tables)──> data-access-sql/schema/tables/*.yaml
```

## 字段说明（拆分目录）

适用于 `flows/<flow_id>/index.yaml` 与 `flows/<flow_id>/nodes/<subgraph_id>/<node_id>.yaml`。

### 流程 index（`flows/<flow_id>/index.yaml`）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `flow_id` | string | 是 | 流程唯一 ID。 | `operator_return_wire_and_issue_available_wire` |
| `title` | string | 是 | 流程标题。 | `操作员存入归还焊丝并取出可用焊丝` |
| `file` | string | 是 | 流程图路径，相对 `requirements/flows/`。 | `diagrams/操作员存入归还焊丝取出可用焊丝.md` |
| `description` | string | 否 | 流程说明。 | - |
| `sections` | list | 推荐 | 与流程图 subgraph 对齐；含 `subgraph_id`、`title`、可选 `parent_subgraph_id`、`node_ids`。 | - |
| `node_order` | list | 是 | 主路径节点顺序。 | - |
| `node_files` | map | 是 | 节点 ID → `nodes/<subgraph_id>/<node_id>.yaml`。 | - |

### 节点（`nodes/<subgraph_id>/<node_id>.yaml`）

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `subgraph_id` | string | 是 | 与 Mermaid subgraph id、物理目录名一致（取叶子 subgraph）。 |
| `node_id` | string | 是 | 流程图 Mermaid 节点 ID。 |
| `node_text` | string | 否 | 节点文字。 |
| `access_type` | string | 是 | `user_input` / `mes_sql` / `mes_function` / `decision` / `application_db` 等。 |
| `sql_ids` | list | 是 | 引用的 catalog `id`；非 SQL 节点可为 `[]`。 |
| `input_mappings` | list | 推荐 | 入参来源（见下）。 |
| `output_fields` | list | 条件必填 | 本节点产出字段（见上文）；`user_input` / SQL / 应用库访问节点必填。 |
| `outcome_routing` | map | 推荐 | 分支 → `next` 节点 ID。 |

### input_mappings

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | string | 是 | 与 catalog `inputs.name` 一致。 |
| `source_node` | string | 条件必填 | 上游流程节点 ID。 |
| `source_type` | string | 条件必填 | `constant` / `ui_input` / `system` 等。 |
| `source_field` | string | 否 | 来源字段名。 |
| `value` | string/number/boolean | 否 | `source_type: constant` 时的固定值。 |

### outcome_routing

键可与 catalog `outcomes` 或判定结果（如 `yes` / `no`）对应；每项含 `next`（必填）与可选 `note`。

**应用库查询节点（约定）**

| 引擎 outcome | 流程图路由 | 说明 |
|--------------|------------|------|
| `single` / `empty` | 配置 `next`（常共用一个判定节点） | 查询成功；无行由下游判定（如 `field_not_empty`）区分业务无数据 |
| `error` | **不配置** | SQL/catalog/连接失败 → 应用层统一异常提示并中止，**不新增** diagram 终止节点 |

示例：`queryMatchedAvailableWire` 仅 `single`/`empty` → `checkMatchedWireExists`（findings F-2 已关闭）。
