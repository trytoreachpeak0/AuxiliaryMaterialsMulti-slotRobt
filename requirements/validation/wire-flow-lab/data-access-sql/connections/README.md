# connections (验证版)

本目录说明验证工具用到的连接，仅用于本地调试，不要提交真实账号 / 密码 / IP / 端口。

## 应用数据库 (SQLite)

- 始终为真实执行。验证工具首次启动会在 `wire-flow-lab/db/app.db` 自动建库并灌入种子数据。
- 建库脚本：`../../db/schema.sqlite.sql`、`../../db/seed.sqlite.sql`。

## MES 数据源（两种模式，可在界面切换）

| 模式 | 连接 | SQL 来源 |
|---|---|---|
| `SqliteMock`（默认） | 与应用库同一个 SQLite 文件中的 `mv_fw_username` 等 mock 表 | `sql-catalog/items/mes_mock.*.yaml` 的 `sql` 字段（SQLite） |
| `Oracle` | 界面填入 Oracle 连接串（如 `User Id=...;Password=...;Data Source=host:1521/svc`） | 正式 `data-access-sql/sql-catalog` 的 Oracle 原生 SQL（镜像里以 `oracle_sql` 字段保留） |

存储函数 `Get_Mat_QuotaCheck` / `FWMES.FUN_MAT_TRANS_NEW` 在 `SqliteMock` 模式下由界面「MES 函数 mock 结果」面板配置（配额差值、提交成功/失败）。
