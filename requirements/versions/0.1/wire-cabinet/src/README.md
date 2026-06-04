# WireCabinet 0.1 解决方案

| 项目 | 说明 |
|------|------|
| WireCabinet.Hmi | WPF 触控宿主（1024×768） |
| WireCabinet.Flow | YAML 流程引擎（flow-sql-map） |
| WireCabinet.Data | SQLite 应用库 + Oracle MES |
| WireCabinet.Slots | 格口业务层 |
| WireCabinet.Slots.Hardware | Modbus TCP 开锁 |
| WireCabinet.Rcs | 到站门禁、控车、回充 |
| WireCabinet.Core | 路径解析、配置模型 |

```powershell
dotnet build ../WireCabinet.sln
dotnet run --project WireCabinet.Hmi/WireCabinet.Hmi.csproj
```
