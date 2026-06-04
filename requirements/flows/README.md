# 焊丝发放流程资料（flows）

本目录集中维护**业务流程**相关资产：流程图、术语、以及可执行节点映射。

## 目录结构

| 目录 / 文件 | 作用 |
|---|---|
| [diagrams/](diagrams/) | Mermaid 流程图（业务主视图） |
| [glossary/](glossary/) | 流程 ID、节点 ID、术语对照 |
| [flow-sql-map/](flow-sql-map/) | 流程可执行映射：节点顺序、分支、`sql_ids`、入参来源；引用 [data-access-sql/sql-catalog](../data-access-sql/sql-catalog/)（subgraph 目录约定见 [flow-sql-map/SUBGRAPH-NODES.md](flow-sql-map/SUBGRAPH-NODES.md)） |

## 引用关系

```text
diagrams/*.md  ←──(file)──  flow-sql-map/flows/<flow_id>/index.yaml
                              │
                              └──(sql_ids)──> data-access-sql/sql-catalog/items/*.yaml
```

日常改流程节点或分支时，优先改 `flow-sql-map/` 并与 `diagrams/` 对照；改 SQL 正文时改 `data-access-sql/sql-catalog/`。

验证工具 `validation/wire-flow-lab/` 在本地保留聚合版 `flow.yaml` 镜像，正式维护仍以本目录 `flow-sql-map`（每节点一文件）为准。
