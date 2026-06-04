# Requirements

本目录按“版本范围、需求基线、流程资料、数据访问、验证工具”拆分。开发和验收时优先看 `versions/`，缺少细节时再回到 `baseline/` 和 `flows/` 查完整资料。

## Directory Map

| 目录 | 作用 |
|---|---|
| [versions/0.1](versions/0.1/) | 当前调试演示版本：需求/设计文档；可运行工程在 [versions/0.1/wire-cabinet](versions/0.1/wire-cabinet/) |
| [versions/0.2](versions/0.2/) | 预留给下一阶段增量需求 |
| [versions/final](versions/final/) | 最终生产版本的完整建设方向 |
| [baseline](baseline/) | 完整分层需求资料来源，不直接代表当前版本范围 |
| [flows](flows/) | 流程图、术语、可执行映射（`diagrams/`、`glossary/`、`flow-sql-map/`） |
| [data-access-sql](data-access-sql/) | MES、应用数据库等数据访问 SQL 与 schema（`sql-catalog/`、`schema/`） |
| [validation/wire-flow-lab](validation/wire-flow-lab/) | 焊丝发放流程验证工具资料，非最终交付品 |

## Version Priority

当前优先按 [versions/0.1](versions/0.1/) 执行。0.1 只覆盖焊丝存放、OP 存废取新、最小应用数据库库存记录、最小格口控制、手动控车，以及必要的 MES 查询或可标识 Mock。

`versions/0.2` 用于承接 0.1 之后但尚未进入最终版本基线的增量能力；`versions/final` 用于描述完整生产系统目标。

## Working Rules

- 版本目录只记录该版本要实现、测试和验收的内容。
- `baseline/` 保留完整需求源文档，作为版本拆分和详细设计依据。
- `flows/flow-sql-map/` 描述流程节点如何引用 `data-access-sql/sql-catalog/` 中的 SQL；流程图在 `flows/diagrams/`。
- `data-access-sql/` 只维护 SQL 与表结构，通过 `system`、`datasource` 区分 MES 与应用库。
- `validation/` 下的内容只服务流程跑通、逻辑校验、SQL 校验和缺口发现，不作为最终交付物。
