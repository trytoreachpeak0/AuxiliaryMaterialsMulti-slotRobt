# WireCabinet（0.1 可运行交付）

本目录为 **0.1 焊丝柜触控屏程序** 与现场配置，与上一级 [需求与设计文档](../README.md) 分离。

## 目录

```text
wire-cabinet/
├── WireCabinet.sln
├── src/                 ← WPF 宿主与各层类库
├── scripts/             ← SQLite 建表/种子
├── slot-config/         ← 54 格 IO 映射 + io-modules（现场填 IP）
├── station-config/      ← 作业站/充电点
├── agv-dispatch-sdk/    ← RCS .NET SDK
└── data/                ← 运行时 app.db（首次启动自动创建）
```

## 构建与运行

```powershell
cd requirements/versions/0.1/wire-cabinet
dotnet build WireCabinet.sln
dotnet run --project src/WireCabinet.Hmi/WireCabinet.Hmi.csproj
```

## 联调前配置

| 文件 | 说明 |
|------|------|
| `src/WireCabinet.Hmi/appsettings.json` | MES、RCS、应用库路径 |
| `wire-cabinet/slot-config/io-modules.yaml` | 由 `io-modules.template.yaml` 复制，填写 4 模块 IP |
| `wire-cabinet/station-config/stations.yaml` | 由 `stations.template.yaml` 复制，填写站点号 |

需求范围、验收清单、HMI/格口/RCS 设计见 [../docs/](../docs/) 与 [../需求范围.md](../需求范围.md)。
