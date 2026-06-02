# 流程节点 ↔ SQL 映射（拆分目录）

每个业务流程一个文件，放在 `flows/` 下。

## 当前流程

| 文件 | flow_id | 流程图 |
|------|---------|--------|
| [flows/op_return_old_wire_take_new_wire.yaml](flows/op_return_old_wire_take_new_wire.yaml) | `op_return_old_wire_take_new_wire` | OP 存废取新 |
| [flows/mh_store_wire.yaml](flows/mh_store_wire.yaml) | `mh_store_wire` | 物料员存焊丝 |

索引见 [index.yaml](index.yaml)。`sql_ids` 已与 `mes-sql-catalog/items/` 中的 `id` 对齐。

维护与合并方式同 [mes-sql-catalog/README.md](../mes-sql-catalog/README.md)。
