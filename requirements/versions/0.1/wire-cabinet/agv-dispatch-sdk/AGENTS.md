# AGV 调度 SDK — Agent 阅读指引

面向 **LLM / Cursor Agent**：按下列顺序阅读，再生成集成代码。

## 阅读顺序

1. [docs/LLM集成指南.md](docs/LLM集成指南.md) — 决策表、标准轮询伪代码、反模式
2. [docs/AGV调度场景与状态机.md](docs/AGV调度场景与状态机.md) — 场景 A～G、字段百科
3. [docs/AGV调度SDK使用说明.md](docs/AGV调度SDK使用说明.md) — 配置项、API、REST 对照
4. [docs/schemas/AgvDispatchOptions.schema.json](docs/schemas/AgvDispatchOptions.schema.json)
5. [docs/fixtures/](docs/fixtures/) — JSON 样例
6. [AgvDispatch.Sdk/appsettings.example.json](AgvDispatch.Sdk/appsettings.example.json)

## 硬性约束

- 目标框架：**net8.0+** 引用 `AgvDispatch.Sdk`；**不要**假设 net48 可引用。
- **禁止**在业务代码写死 `BaseUrl`、站点号、`deviceKey`、地图 ID；一律 `appsettings` → `AgvDispatchOptions`。
- **仓门状态**不由 SDK 采集；业务传入 `DoorStateInput`，再调用 `AgvOperationEvaluator`。
- `destination` 是地图站点 **数字 ID**，不是仓位名称；名称解析由业务/SQL 完成。

## 代码入口

- 客户端：`IAgvDispatchClient` / `AgvDispatchClient`
- 规则：`AgvOperationEvaluator.Evaluate`
- DI：`services.AddAgvDispatchClient(o => configuration.Bind(o))`

## 示例项目

`AgvDispatch.Sample` — 场景 A 轮询（不自动下单）。
