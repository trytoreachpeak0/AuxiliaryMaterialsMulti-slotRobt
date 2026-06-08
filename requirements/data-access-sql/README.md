# Data Access SQL 记录说明

本目录用于集中记录焊丝发放流程中访问 MES、应用数据库及后续其他数据源的 SQL，方便 AI、开发、测试和接口人员统一理解。
本文件是字段说明手册：第一次接触本目录的人，先读这里就能看懂每个文件、每个字段是什么。

## 1. 文件总览

### 1.1 目录结构（推荐查阅与编辑）

内容已按「一条目一文件」拆到子目录；**日常请改子目录里的文件**。

| 目录 / 文件 | 作用 | 说明 |
|---|---|---|
| **`sql-catalog/`** | 数据访问 SQL 清单 | [index.yaml](sql-catalog/index.yaml) + [items/&lt;id&gt;.yaml](sql-catalog/items/)，见 [sql-catalog/README.md](sql-catalog/README.md) |
| **`schema/`** | 表结构与数据字典 | [tables/](schema/tables/)、[enums/](schema/enums/)，见 [schema/README.md](schema/README.md) |
| `connections/` | 数据源连接资料 | 可存放本地连接说明或模板；含敏感信息的真实连接不应公开 |
| `../flows/flow-sql-map/` | 流程可执行映射 | 与流程图、SQL 清单关联，见 [../flows/flow-sql-map/README.md](../flows/flow-sql-map/README.md) |
| `../flows/glossary/` | 术语对照 | 按主题分文件 |

### 1.2 维护方式

本目录统一维护拆分后的文件，不再维护根目录合并视图，也不再依赖 Node.js 拆分/合并脚本。
日常修改入口：

- SQL 清单：`sql-catalog/items/<id>.yaml`
- 表结构与枚举：`schema/tables/*.yaml`、`schema/enums/*.yaml`
- 流程可执行映射：[`../flows/flow-sql-map/`](../flows/flow-sql-map/)（见该目录 README）

### 1.3 引用关系

```text
flows/flow-sql-map/flows/*.yaml ──(sql_ids)──> data-access-sql/sql-catalog/items/*.yaml ──(datasource)──> 逻辑数据源 ID
                                                      │
                                                      └──(tables)──> data-access-sql/schema/tables/*.yaml
```

---

## 2. sql-catalog 字段说明

以下字段适用于 **`sql-catalog/items/<id>.yaml`** 中的单条 SQL。

### 2.1 顶层字段

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `version` | integer | 是 | 文件结构版本号，便于以后升级格式。 |
| `owner` | string | 否 | 负责人或归口部门。 |
| `description` | string | 否 | 本文件整体说明。 |
| `sql_items` | list | 是 | SQL 条目列表，每个元素是一条 SQL 的完整定义（见下）。 |

### 2.2 sql_items 每条 SQL 的字段

| 字段 | 类型 | 必填 | 说明 | 取值 / 示例 |
|---|---|---|---|---|
| `id` | string | 是 | 稳定唯一 ID，作为被引用锚点。 | `mes.operator.get_by_id` |
| `title` | string | 是 | 人能看懂的标题。 | `查询 MES 操作员` |
| `system` | string | 是 | 所属系统。 | `MES` / `APP_DB` |
| `operation` | string | 是 | 访问类型。 | `read` / `write` / `update` |
| `dialect` | string | 是 | 数据库方言。 | `oracle` / `sqlserver` / `mysql` |
| `risk_level` | string | 是 | 操作风险等级，取值见 [6.1](#61-risk_level操作风险等级)。 | `low` / `medium` / `high` |
| `status` | string | 是 | SQL 成熟度，取值见 [6.2](#62-status该条-sql-的成熟度)。 | `draft` / `verified` / `deprecated` |
| `datasource` | string | 推荐 | 该 SQL 使用的逻辑数据源 ID。 | `mes.oracle.main` |
| `purpose` | string | 是 | 业务目的，说明“为什么查”，不要只描述 SQL 动作。 | - |
| `inputs` | list | 是 | 输入参数列表（见 2.4）。 | - |
| `outputs` | list | 是 | 输出字段列表（见 2.5）。 | - |
| `sql` | string(多行) | 是 | 参数化 SQL 模板，参数用 `:name`。 | - |
| `result_cardinality` | string | 推荐 | 期望返回行数，取值见 [6.3](#63-result_cardinality期望返回行数)。 | `1` / `0..1` / `0..n` |
| `outcomes` | map | 推荐 | 各种结果的处理（见 2.6），推荐用它替代下面两个旧字段。 | - |
| `tables` | list | 推荐 | 该 SQL 涉及的表/视图清单（见 2.7），详细列结构放 `schema/tables/`。 | - |
| `interface` | map | 否 | 建议的调用契约/函数签名（见 2.8），方便 AI 生成代码。 | - |
| `sample_result` | list | 否 | 脱敏的样例返回结果，便于理解数据形状、写测试。 | - |
| `transaction` | map | 否 | 写操作的事务契约（见 2.9），仅 `operation` 为 `write`/`update` 时需要。 | - |
| `meta` | map | 否 | 元信息（见 2.10），如 `last_updated`、`author`。 | - |
| `business_rule` | string | 否 | 不完全由 SQL 决定的业务规则。 | `成功返回数值即允许提交；ORA-20007 须重输` |
| `notes` | string | 否 | 补充说明、JOIN 口径、取舍等。 | - |
| `empty_result_means` | string | 否(旧) | 旧字段：查询为空代表什么。推荐改用 `outcomes.empty`。 | - |
| `error_handling` | string | 否(旧) | 旧字段：异常/空结果怎么处理。推荐改用 `outcomes`。 | - |

> 说明：`empty_result_means` 和 `error_handling` 是早期字段，部分条目仍在用。新增或重构条目时，建议统一改为结构化的 `result_cardinality` + `outcomes`。

### 2.3 流程关联来源

SQL 条目只描述 SQL 自身的用途、参数、结果和调用契约；流程节点与 SQL 的关联统一维护在 [`../flows/flow-sql-map/flows/`](../flows/flow-sql-map/flows/) 各节点 YAML 的 `sql_ids` 与 `output_fields`（引用 catalog，不重复抄写 SQL）中，下游 `input_mappings` 通过 `source_node` + `source_field` 引用上游产出字段。

### 2.4 inputs 子字段（输入参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `name` | string | 是 | 参数名，应与 SQL 里的 `:name` 一致。 | `user_code` |
| `type` | string | 是 | 数据类型。 | `string` / `integer` / `datetime` / `boolean` |
| `required` | boolean | 是 | 是否必填。 | `true` / `false` |
| `source` | string | 是 | 参数来源（哪个流程步骤/输入提供）。 | `OP输入操作人员ID` |
| `example` | string | 否 | 示例值。 | `S0017655` |

### 2.5 outputs 子字段（输出字段）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `name` | string | 是 | 输出的**逻辑字段名**（给代码/流程判断用的契约名），不是表名。建议在 SQL 用别名对齐。 | `operator_name` |
| `type` | string | 是 | 数据类型。 | `string` / `integer` / `datetime` / `boolean` |
| `meaning` | string | 是 | 业务含义。 | `操作人员姓名` |
| `column` | string | 否(建议) | 数据库真实列名，当 `name` 与列名不一致时记录映射。 | `USERNAME` |

### 2.6 outcomes 子字段（结果处理）

`outcomes` 是一个 map，键是结果分支，值描述该结果如何处理。可按需出现以下分支：

| 分支键 | 含义 |
|---|---|
| `empty` | 返回 0 行。 |
| `single` | 返回 1 行。 |
| `multiple` | 返回多行（唯一性/身份校验场景通常是数据异常，应禁止放行）。 |
| `error` | 数据库异常或查询超时。 |

每个分支下的字段：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `means` | string | 是 | 这种结果代表什么（结果语义，与具体流程无关）。 |
| `action` | string | 是 | 业务上怎么处理。 |
| `severity` | string | 否 | 严重度，例如 `error`。 |

> 职责边界：`outcomes` 只描述“这条 SQL 的某种结果代表什么、严重度如何”，**不记录跳转到哪个流程节点**。跳转是与流程绑定的编排行为（同一条 SQL 被多个流程复用时跳转目标不同），统一放在 [`../flows/flow-sql-map/`](../flows/flow-sql-map/) 节点 YAML 的 `outcome_routing.<分支>.next`（见 [flow-sql-map README](../flows/flow-sql-map/README.md)）。
>
> 原则：唯一性/身份校验类查询不要在 SQL 层用 `ROWNUM`/`TOP` 掩盖多行，应通过 `outcomes.multiple` 暴露成异常，以便发现数据问题。完整示例见 `mes.operator.get_by_id`。

### 2.7 tables 子字段（涉及的表/视图）

记录该 SQL 用到哪些表/视图；详细的列结构、类型、是否可空放在 `schema/tables/`，这里只做引用。

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `name` | string | 是 | 表或视图名，与 `schema/tables/<name>.yaml` 的 `name` 对应。 | `mv_fw_username` |
| `type` | string | 否 | 对象类型。 | `table` / `view` / `materialized_view` |
| `usage` | string | 否 | 在本 SQL 中的用途。 | `操作员工号与姓名映射` |

### 2.8 interface 子字段（调用契约/函数签名）

给 AI 一个明确的“函数长什么样”，便于生成代码。

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `function_name` | string | 是 | 建议的函数/方法名。 | `getOperatorNameByUserCode` |
| `params` | list | 是 | 入参列表，每项含 `name`、`type`。 | `- name: user_code` `  type: string` |
| `returns` | string | 是 | 返回类型描述。 | `operator_name (string) \| null` |

### 2.9 transaction 子字段（写操作事务契约）

仅当 `operation` 为 `write` / `update` 时需要。只读查询不用写。

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `required` | boolean | 是 | 是否需要事务。 | `true` |
| `idempotent` | boolean | 否 | 重复执行是否安全（幂等）。 | `false` |
| `affected_rows` | string | 否 | 预期影响行数。 | `1` / `0..n` |
| `rollback_on` | string | 否 | 什么情况回滚。 | `任一步骤失败` |

### 2.10 meta 子字段（元信息）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `last_updated` | date | 否 | 最近更新日期。 | `2026-06-01` |
| `author` | string | 否 | 维护人。 | `张三` |

---

## 3. flow-sql-map（已迁至 flows）

流程可执行映射（节点顺序、分支、`sql_ids`、入参来源）已迁至 [`../flows/flow-sql-map/`](../flows/flow-sql-map/)。字段说明与维护约定见该目录 [README.md](../flows/flow-sql-map/README.md)。

---

## 4. schema 字段说明

表定义见 **`schema/tables/<表名>.yaml`**，枚举见 **`schema/enums/<字段名>.yaml`**。

记录表/视图的真实结构和数据字典，让 AI 不用靠 SQL 反推字段。

### 5.0 顶层字段

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `version` | integer | 是 | 文件结构版本号。 |
| `description` | string | 否 | 文件说明。 |
| `datasources` | list | 是 | 数据源引用列表，`id` 为逻辑数据源 ID。 |
| `tables` | list | 是 | 表/视图结构列表（见 5.1）。 |
| `enums` | list | 否 | 数据字典/枚举值（见 5.2）。 |

### 5.1 tables 子字段（表/视图结构）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `name` | string | 是 | 表/视图名，与 catalog 的 `tables.name` 对应。 | `mv_fw_username` |
| `datasource` | string | 是 | 所属数据源 ID。 | `mes.oracle.main` |
| `type` | string | 是 | 对象类型。 | `table` / `view` / `materialized_view` |
| `comment` | string | 否 | 表的业务说明。 | `操作员工号与姓名映射` |
| `columns` | list | 是 | 列定义列表（见下）。 | - |
| `notes` | string | 否 | 补充说明。 | - |

列（`columns`）的字段：

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `name` | string | 是 | 列名（数据库真实列名）。 | `usercode` |
| `type` | string | 是 | 数据库数据类型。 | `VARCHAR2` / `NUMBER` / `DATE` |
| `length` | integer | 否 | 长度/精度。 | `50` |
| `nullable` | boolean | 是 | 是否可空。 | `true` / `false` |
| `is_key` | boolean | 否 | 是否主键/唯一键。 | `true` / `false` |
| `comment` | string | 否 | 列的业务含义。 | `操作人员工号` |

### 5.2 enums 子字段（数据字典/枚举）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `field` | string | 是 | 枚举字段名。 | `enabled` |
| `applies_to` | list | 否 | 应用到哪些 `表.列`。 | `mes_machine.enabled` |
| `db_type` | string | 否 | 数据库类型。 | `NUMBER(1)` |
| `values` | list | 是 | 取值列表，每项含 `value` 和 `meaning`。 | `value: 1` `meaning: 启用` |

---

## 5. 字段取值规范（枚举）

### 5.1 `risk_level`（操作风险等级）

- `low`：只读、单表、无副作用的查询。
- `medium`：只读，但涉及多表/聚合，或结果直接影响后续业务判断。
- `high`：写入、更新或删除数据，有副作用，AI 处理时需谨慎并关注事务。

### 5.2 `status`（该条 SQL 的成熟度）

- `draft`：草稿，尚未在真实库验证，字段名和结果可能不准确，不要盲信。
- `verified`：已在真实库验证过字段和返回结果，可放心使用。
- `deprecated`：已废弃，不要再使用。

### 5.3 `result_cardinality`（期望返回行数）

- `1`：必须且只返回一行。
- `0..1`：最多一行，可能为空。
- `0..n`：可能返回多行。

---

## 6. SQL ID 命名规则

推荐格式：

```text
mes.<业务对象>.<动作>
```

示例：

```text
mes.operator.get_by_id
mes.wire.get_by_lot_no
mes.eqp.get_by_wire_bonding_eqp_no
mes.product.get_latest_product_lot_by_eqp
mes.wire_quota.check_quota
```

不要使用下面这种不清晰的名称：

```text
SQL1
查询1
焊丝SQL
MES查询
```

---

## 7. 流程图关联规则

流程图中的 Mermaid 节点 ID 可以直接作为关联锚点。

例如流程图中有：

```mermaid
queryOPById[("查询 MES 数据库该操作人员")]
```

则在对应的 [`../flows/flow-sql-map/flows/<flow_id>/nodes/<subgraph_id>/<node_id>.yaml`](../flows/flow-sql-map/) 中写：

```yaml
- node_id: queryOPById
  node_text: 查询 MES 数据库该操作人员
  sql_ids:
    - mes.operator.get_by_id
  input_mappings:
    - name: user_id
      source_node: inputOperatorWorkInfo
      source_field: operator_id
```

`sql-catalog/items/<id>.yaml` 中不再重复维护流程节点反向引用。

---

## 8. 注意事项

1. SQL 必须参数化，不要把真实操作员、批号、机台号直接写死在 SQL 中。
2. 不要在 catalog/map/schema 里记录数据库账号、密码、IP、端口、连接串；这里只保留逻辑数据源 ID。
3. 如果 SQL 会写入数据库，必须把 `operation` 标记为 `write` 或 `update`，并补充写入影响范围。
4. 如果同一条 SQL 被多个流程使用，只维护一条 SQL 清单，并在 [`../flows/flow-sql-map/`](../flows/flow-sql-map/) 对应节点中引用同一个 `sql_id`。
5. 如果字段来自多个表，建议在 `notes` 中说明 JOIN 关系和业务口径。
6. 如果业务判断不完全由 SQL 决定，例如“ORA-20007 时须重输待焊芯片数”，请写在 `business_rule` 中。
7. `outputs.name` 是逻辑字段名（契约），建议用 SQL 别名与真实列名对齐，或用 `column` 记录真实列名。
8. 新增或重构条目时，结果处理优先用 `result_cardinality` + `outcomes`，逐步替代 `empty_result_means` / `error_handling` / `on_success_next` / `on_empty_or_error` 等旧字段。

---

## 9. 给 AI 编程还需要补充的信息

为让 AI 能据本目录直接编程，除了原有的业务字段外，还应补充以下信息。下面说明“是什么、为什么需要、放在哪个文件”。

| 信息 | 是什么 | 为什么 AI 需要 | 放在哪里 | 当前状态 |
|---|---|---|---|---|
| 表/视图结构 | 字段名、类型、长度、是否可空、主键 | 生成实体类/DTO/ORM，避免靠 SQL 猜字段 | `schema/tables/` | 已建文件，`mv_fw_username` 已填，其余为模板待补 |
| 数据字典/枚举 | 离散字段的取值含义（如 `enabled` 1/0） | 正确写状态判断分支 | `schema/enums/` | 已给 `enabled` 示例，其余待补 |
| 数据源归属 | 每条 SQL 连哪个库 | 区分 MES 库与应用库，避免连错 | catalog 每条的 `datasource` | 5 条已全部标注 |
| 调用契约 | 函数名、入参、返回类型 | 直接生成函数签名 | catalog 每条的 `interface` | operator 已示例，其余可按需补 |
| 样例数据 | 脱敏的返回行 | 理解数据形状、写测试和 mock | catalog 每条的 `sample_result` | operator 已示例，其余可按需补 |
| 结果基数与处理 | 期望几行、空/多行/异常怎么办 | 决定返回单对象还是列表、异常分支 | catalog 的 `result_cardinality` + `outcomes` | operator 已用新结构，其余仍用旧字段 |
| 写操作事务契约 | 是否事务、幂等、影响行数 | 安全地生成写入代码 | catalog 的 `transaction` | 字段已定义，当前无写操作 SQL |
| 元信息 | 更新时间、维护人 | 追溯与可信度判断 | catalog 的 `meta` + `status` | operator 已示例 |

### 仍需你补充的内容

1. `schema/tables/` 里除 `mv_fw_username` 外的表结构和字段类型（目前是模板）。
2. `enums` 里 `wire_type` 等业务枚举的真实取值。
3. 其余 4 条 SQL 的 `interface` / `sample_result`，以及统一迁移到 `result_cardinality` + `outcomes`。
4. 流程中的“应用数据库”查询（如归还重量、提交退还）尚未进 catalog，需要时新增连接和 SQL 条目。
