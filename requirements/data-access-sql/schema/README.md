# Schema 与数据字典（拆分目录）

## 目录结构

| 路径 | 内容 |
|------|------|
| [index.yaml](index.yaml) | 版本与索引 |
| [datasources.yaml](datasources.yaml) | 数据源列表 |
| [tables/](tables/) | 每张表/视图一个文件 |
| [enums/](enums/) | 每个枚举字段一个文件 |

## 当前表

| 文件 | 对象 |
|------|------|
| [tables/mv_fw_username.yaml](tables/mv_fw_username.yaml) | 操作员视图 |
| [tables/v_fw_material_indetail.yaml](tables/v_fw_material_indetail.yaml) | 物料批号明细 |
| [tables/welding_wire_materials.yaml](tables/welding_wire_materials.yaml) | 焊丝规格与空盘重量（0.1 SQLite，源 WeldingWireMaterials） |
| [tables/fw_eqpres_eqpinformation.yaml](tables/fw_eqpres_eqpinformation.yaml) | 机台信息 |

## 枚举

| 文件 | 字段 |
|------|------|
| [enums/state.yaml](enums/state.yaml) | `v_fw_material_indetail.state` |

## 维护方式

表/视图结构直接维护在 `tables/*.yaml`，枚举直接维护在 `enums/*.yaml`。
新增或改名文件时，同步维护 `index.yaml`。
