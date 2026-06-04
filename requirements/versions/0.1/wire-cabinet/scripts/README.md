# 0.1 应用库 SQLite 脚本

0.1 触控屏/业务宿主使用 **SQLite** 应用库。本目录提供建表与种子数据。

## 焊丝归还重量（WeldingWireMaterials）

| 文件 | 说明 |
|------|------|
| `create-welding_wire_materials.sqlite.sql` | 建表 `welding_wire_materials` |
| `seed-welding_wire_materials.sqlite.sql` | 53 条规格数据（由现场导出生成） |
| `_tools/gen_welding_wire_seed.py` | 从仓库根目录 `WeldingWireMaterials.sql` 重新生成 seed |
| `../data-access-sql` | 正式 catalog：`app.returned_weight.get_by_spec` |

**字段与流程映射**

| 表列 | 流程字段 |
|------|----------|
| `specification_model` | `queryWireSpecByLotNo.spec` → 查询入参 |
| `empty_spool_weight` | `queryWireReturnedWeightBySpec.return_weight` |

**初始化顺序（宿主 app.db）**

```sql
-- 1. 建表
.read create-welding_wire_materials.sqlite.sql
-- 2. 灌数
.read seed-welding_wire_materials.sqlite.sql
```

现场 SQL Server 源表变更时：更新根目录 `WeldingWireMaterials.sql` → 运行 `python _tools/gen_welding_wire_seed.py` → 重新 `.read` seed。
