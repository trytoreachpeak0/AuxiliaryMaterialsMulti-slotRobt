# Wire Flow Lab

本目录用于开发和记录焊丝发放流程验证工具，目标是帮助团队验证流程是否能跑通、缺少哪些步骤、逻辑是否有误，以及 SQL 查询或返回结果是否符合预期。

## Boundary

这里的内容是内部验证资产，不属于最终交付品。验证工具可以快速迭代，也可以使用 mock、样例数据和临时脚本，但发现的问题应回写到正式需求、流程图或 `data-access-sql/` 中。

## Contents（已交付）

| 目录 / 文件 | 用途 |
|---|---|
| `README.md` | 本说明文件 |
| `data-access-sql/` | 镜像正式目录的 SQLite 验证版：`sql-catalog`(app.* 应用库 + mes_mock.* MES)、`schema`、`flow-sql-map`(6 个聚合 `flow.yaml`，补齐缺 SQL 节点)、`connections` |
| `db/` | `schema.sqlite.sql` 建表 + `seed.sqlite.sql` 种子；运行期生成 `app.db` 与 `selftest-output.txt` |
| `src/` | `WireFlowLab` C#/.NET 8 WPF 验证工具（数据层 / 格口 mock / YAML 流程引擎 / 静态校验 / MVVM 界面） |
| `scenarios/` | 各流程正常路径 + 关键异常分支的输入与期望 |
| `findings.md` | 流程缺口、逻辑问题、SQL/schema 一致性问题汇总 |

## 运行方式

前置：已安装 .NET 8 SDK（`net8.0-windows`，需 Windows 桌面运行 WPF）。

```powershell
cd requirements/validation/wire-flow-lab/src
dotnet restore
dotnet build WireFlowLab.sln          # 编译
dotnet run --project WireFlowLab/WireFlowLab.csproj          # 启动 WPF 界面
dotnet run --project WireFlowLab/WireFlowLab.csproj -- selftest   # 无界面自测，输出 db/selftest-output.txt
```

- **首次启动自动建库**：工具会在 `db/app.db` 执行 `schema.sqlite.sql` + `seed.sqlite.sql`（应用库与 MES mock 表共用同一个 SQLite 文件）。
- **应用数据库**：始终真实 SQLite 执行。
- **切换 MES 模式**：界面右上「MES 设置」。默认 `SQLite mock`（MES 只读查询跑 mock 表，存储函数用可配置的配额差值/提交结果）；勾选「使用真实 Oracle MES」并填连接串后点「应用 MES 设置」即用 `mes_mock.*` 中保留的原生 Oracle SQL/存储函数对接真库。
- **格口仓门**：右下「格口仓库」面板可视化每个格口的门开/关、锁、业务状态、绑定焊丝；「开门/关门」按钮手动驱动「等待门关闭」分支。
- **验证**：「校验全部流程」做静态校验（断链/缺 SQL/入参来源/不可达/分支不全）；「单步/连续执行」产生 trace；「导出 findings」写出结论。

## Validation Focus

- 流程节点是否完整，是否存在无法进入或无法退出的分支。
- UI 输入、系统上下文、应用数据库和 MES SQL 的参数是否能正确传递。
- SQL 的空结果、多结果、异常结果是否能映射到明确流程分支。
- 应用数据库状态更新、格口控制动作和 MES 查询之间的顺序是否合理。
- 0.1 验收脚本是否能用可重复方式跑通。

## References

- `../../versions/0.1/`：当前调试演示版本范围和验收清单。
- `../../flows/diagrams/`：焊丝发放流程图。
- `../../flows/flow-sql-map/`：正式流程节点到 SQL/function/input/decision 的映射（每节点一文件）；本工具镜像见 `data-access-sql/flow-sql-map/`（聚合 `flow.yaml`）。
- `../../data-access-sql/sql-catalog/`：数据访问 SQL 清单。
