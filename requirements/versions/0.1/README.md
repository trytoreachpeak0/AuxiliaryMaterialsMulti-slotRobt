# 0.1 版本需求



## 版本定位



0.1 是 **3 天内可调试、可演示** 的最小闭环，不是完整一期系统。目标是在现场证明：



```text

真实格口（可 30 格以上）+ MES 焊丝业务

    ↕

应用库库存 / 格口状态 / 操作日志

    ↕

手动控车到站 → 允许 MH / OP 作业

    ↕

低电量自动回充 → 充满后回充点前作业站

```



优先：**主流程能跑通、异常有中文提示、MES / RCS / 格口均真实联调**，不追求任务池、自动派单、多点配送等平台能力。



## 目录结构



```text

0.1/                          ← 本目录：需求、验收、设计（无工程源码）

├── README.md                   ← 版本入口

├── 需求范围.md

├── 验收清单.md

├── docs/                       ← HMI/UI、格口、RCS 设计、开发待补齐

└── wire-cabinet/               ← **可运行交付**（与需求文档分离）

    ├── WireCabinet.sln

    ├── src/                    ← WPF 宿主 WireCabinet.Hmi

    ├── slot-config/            ← 54 格 IO 映射 + io-modules

    ├── station-config/         ← 作业站 / 充电点

    ├── scripts/                ← SQLite 建表与种子

    └── agv-dispatch-sdk/       ← RCS .NET SDK

```



## 版本目标（必须达成）



| # | 目标 |

|---|------|

| 1 | **6 条焊丝相关流程**均可操作（见 [需求范围.md](需求范围.md) §3），与 MES / 应用库按 `flow-sql-map` 对接 |

| 2 | **真实格口**：IO 映射可配置全柜；已启用格口可开锁；批量开格口流程可演示 |

| 3 | **手动控车**：界面选站 → 下发 RCS → 轮询到站；**未到站或不在作业站时禁止**焊丝存取 |

| 4 | **低电量回充**：低于阈值自动下充电单；充电中**仅禁开格口**（可控车离站）；充满后自动回 **充点前作业站** |

| 5 | **安全联锁**：移动前无开锁中 / 无进行中存取 / 门全关；**移动中每 Tick 监测门态**，突开 Pause AGV |

| 6 | **无 Mock**：MES（含配额、退料）、RCS、已启用格口均接真实环境，不提供模拟/假数据模式 |



## 快速导航



| 你要… | 打开 |

|--------|------|

| 知道 0.1 做啥、不做啥 | [需求范围.md](需求范围.md) |

| 验收怎么演、配置检查表 | [验收清单.md](验收清单.md) |

| **开发还缺什么、现场要提供啥** | [docs/开发待补齐信息.md](docs/开发待补齐信息.md) |

| 实现格口业务层 | [docs/格口控制层-概要设计.md](docs/格口控制层-概要设计.md) |

| 实现 Modbus / 开锁 | [docs/格口硬件控制层-概要设计.md](docs/格口硬件控制层-概要设计.md) |

| 实现控车 / 回充 | [docs/RCS协同层-0.1.md](docs/RCS协同层-0.1.md) + [wire-cabinet/agv-dispatch-sdk/](wire-cabinet/agv-dispatch-sdk/) |

| 格口编号与 IO | [wire-cabinet/slot-config/](wire-cabinet/slot-config/) |

| 编译运行 WireCabinet | [wire-cabinet/README.md](wire-cabinet/README.md) |

| HMI 约束 / OP&MH 分步 UI | [docs/HMI界面约束.md](docs/HMI界面约束.md)、[OP](docs/OP界面-分步交互规范.md)、[MH](docs/MH界面-分步交互规范.md) |

| UI 布局原型 | [WireDispenser.Demo](../../WireDispenser.Demo/)（已被 WireCabinet.Hmi 取代） |

## 构建与运行（WireCabinet）

```powershell
cd requirements/versions/0.1/wire-cabinet
dotnet build WireCabinet.sln
dotnet run --project src/WireCabinet.Hmi/WireCabinet.Hmi.csproj
```

联调前配置（复制模板后填写）：

| 文件 | 说明 |
|------|------|
| `src/WireCabinet.Hmi/appsettings.json` | MES 连接串、应用库路径、RCS BaseUrl |
| `wire-cabinet/slot-config/io-modules.yaml` | 由 `io-modules.template.yaml` 复制，填写 4 模块 IP |
| `wire-cabinet/station-config/stations.yaml` | 由 `stations.template.yaml` 复制，填写作业站/充电点 |

首次运行会在 `requirements/versions/0.1/wire-cabinet/data/app.db` 自动建库并导入 54 格种子（见 `scripts/`）。

**控车界面**：顶栏第三个标签 **「控车 AGV」** → 选择作业站 / 下充电单。RCS 须在 `appsettings.json` 填写账号，站点号来自 `wire-cabinet/station-config/stations.yaml`（可复制 `stations.template.yaml`）。



## 0.1 边界（摘要）



**包含**：6 条焊丝流程、真实 MES/RCS/格口、应用库三表、手动控车与回充、格口单开/批量开。  

**不包含**：任务池、Mock、物体检测强制、完整权限与审计。详见 [需求范围.md](需求范围.md) §8。



## 参考资料（仓库内）



| 路径 | 用途 |

|------|------|

| `../../flows/diagrams/` | 焊丝流程图 |

| `../../flows/flow-sql-map/` | 流程节点与 SQL 映射 |

| `../../data-access-sql/` | MES SQL（正式 catalog） |

| `../../validation/wire-flow-lab/` | SQLite 验证、`app.*` SQL 草案 |

| `../../baseline/` | 母版需求（0.1 取子集） |

| `../../仓位信息/` | 现场 xlsx → 导出 [wire-cabinet/slot-config/](wire-cabinet/slot-config/) |



## 实施前待现场补齐（摘要）



完整清单见 **[docs/开发待补齐信息.md](docs/开发待补齐信息.md)**。验收前须冻结：



- RCS：`appsettings`（BaseUrl、DeviceKey、地图、充电站点）

- **站点表**：作业站 + 充电点（[wire-cabinet/station-config/stations.yaml](wire-cabinet/station-config/stations.template.yaml)）

- **格口**：`wire-cabinet/slot-config/` + `io-modules.yaml`（4 模块 IP）；联锁 enabled∧wired（54 格）

- **回充阈值**、MES 测试库与演示批号/OP/机台


