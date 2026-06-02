# MES SQL 清单（拆分目录）

每条 SQL 一个文件，按 `id` 命名，便于检索与单独维护。

## 快速查找

| 文件 | 说明 |
|------|------|
| [index.yaml](index.yaml) | 总览与全部 `sql_id` 索引 |
| [items/mes.operator.get_by_id.yaml](items/mes.operator.get_by_id.yaml) | 查询操作员 |
| [items/mes.wire_lot.get_by_lot_no.yaml](items/mes.wire_lot.get_by_lot_no.yaml) | 按批号查焊丝 |
| [items/mes.eqp.get_by_wire_bonding_eqp_no.yaml](items/mes.eqp.get_by_wire_bonding_eqp_no.yaml) | 校验焊线机台 |
| [items/mes.product.get_latest_product_lot_by_eqp.yaml](items/mes.product.get_latest_product_lot_by_eqp.yaml) | 机台最近产品批号 |
| [items/mes.wire_usage.check_quota.yaml](items/mes.wire_usage.check_quota.yaml) | 焊线用量配额核对（draft） |
| [items/mes.mat_trans.submit_return.yaml](items/mes.mat_trans.submit_return.yaml) | 提交退料（draft） |

## 维护约定

- **编辑**：只改 `items/<id>.yaml`。
- **合并**：在 `../_tools` 执行 `npm run merge`，会更新上级目录的 `mes-sql-catalog.yaml`（合并视图）。
- **从旧单文件重新拆分**：`npm run split`（会覆盖 `items/`）。

字段说明见上级 [README.md](../README.md)。
