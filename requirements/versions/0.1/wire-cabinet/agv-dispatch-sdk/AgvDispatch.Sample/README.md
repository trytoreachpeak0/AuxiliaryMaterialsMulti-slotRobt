# AgvDispatch.Sample

给 **其他 .NET 8 项目** 参考的示例：如何注册 SDK、按场景调 API、如何复制主循环。

## 快速运行

```bash
cd AgvDispatch.Sample
# 改 appsettings.json 里的 AgvDispatch
dotnet run -- poll
dotnet run -- list
dotnet run -- loop 5          # 5 次 tick，不下单
dotnet run -- loop 519 10 --execute   # 10 次 tick，可接单时下单到 519
dotnet run -- order 519 --execute
```

## 复制到你自己的项目

### 1. 引用 SDK

```xml
<ProjectReference Include="..\AgvDispatch.Sdk\AgvDispatch.Sdk.csproj" />
```

```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.1" />
```

### 2. 注册（见 `SampleRuntime.cs`）

```csharp
services.AddAgvDispatchClient(o =>
    configuration.GetSection(AgvDispatchOptions.SectionName).Bind(o));
```

### 3. 推荐：复制主循环

将以下文件复制到你的业务工程（命名空间可改）：

- `Integration/AgvControlLoop.cs`
- 实现 `IDoorStateProvider`（0.1 生产：聚合 DI+DB 为 `DoorStateInput`，见 `格口控制层-详细设计.md`；Sample 仍可用 `FromCloseFlag` 演示）

在 `BackgroundService` 中：

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    var loop = new AgvControlLoop(_client, _options, _doorProvider, _sink);
    while (!stoppingToken.IsCancellationRequested)
    {
        await loop.TickAsync(dispatchDestinationIfAllowed: null, stoppingToken);
        await Task.Delay(_options.RecommendedPollIntervalMs, stoppingToken);
    }
}
```

需要业务触发下单时，在 `TickAsync` 传入 `destination`（地图站点 ID，来自你们的配置/SQL）。

### 4. 场景示例代码

| 文件 | 说明 |
|------|------|
| `Scenarios/Scenario01_PollStatus.cs` | 查状态 + Evaluator |
| `Scenarios/Scenario02_CreateMoveOrder.cs` | 下单 |
| `Scenarios/Scenario03_DoorPauseContinue.cs` | 门控暂停逻辑 |
| `Scenarios/Scenario07_ControlLoop.cs` | 完整 tick 循环 |

## 安全说明

默认 **干跑**（`Sample:AllowExecute=false`）。只有加 `--execute` 才会调用写调度接口。
