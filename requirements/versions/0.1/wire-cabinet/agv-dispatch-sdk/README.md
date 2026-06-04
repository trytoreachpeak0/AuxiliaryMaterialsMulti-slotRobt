# AgvDispatch SDK（0.1 RCS 实现包）

本目录是 0.1 **RCS 协同层** 的 .NET 8 实现与文档，对接调度 Web API（HTTP + Bearer，主动轮询）。

**0.1 要做什么、不做什么**：见上级目录 [RCS协同层-0.1.md](../docs/RCS协同层-0.1.md)（仅范围裁剪，不重复下文 API）。

## 文档（以此为准）

| 文档 | 说明 |
|------|------|
| [docs/AGV调度SDK使用说明.md](docs/AGV调度SDK使用说明.md) | API、配置项、DI、REST 对照 |
| [docs/AGV调度场景与状态机.md](docs/AGV调度场景与状态机.md) | 轮询、下单、门暂停、到站、充电、急停 |
| [docs/LLM集成指南.md](docs/LLM集成指南.md) | 后台服务 / Agent 集成时序 |
| [docs/迁移到其他项目.md](docs/迁移到其他项目.md) | ProjectReference 与配置迁移 |
| [AGENTS.md](AGENTS.md) | 编码 Agent 索引 |
| [AgvDispatch.Sdk/README.md](AgvDispatch.Sdk/README.md) | 类库说明 |

配置模板：[AgvDispatch.Sdk/appsettings.example.json](AgvDispatch.Sdk/appsettings.example.json)

## 工程结构

```text
agv-dispatch-sdk/
├── AgvDispatch.Sdk/          # IAgvDispatchClient、AgvOperationEvaluator
├── AgvDispatch.Sample/       # Scenario00–07
├── AgvDispatch.Sdk.Tests/
└── docs/
```

## 本地验证

```bash
cd AgvDispatch.Sample
# 复制 AgvDispatch.Sdk/appsettings.example.json → appsettings.json 并填写
dotnet run
```

需网络可达调度 `BaseUrl`。

## 与业务系统的边界

- **SDK**：调度 REST、订单/车辆状态、评估与 Sample 循环（`AgvControlLoop`）。
- **业务**：站点表、到站门禁、焊丝/MES/格口联锁、回充阈值、日志落库；格口门通过 `IDoorStateProvider` 注入。
