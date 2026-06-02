# 流程文件与流程 ID

| 文件 | 中文标题 | 建议流程 ID |
| --- | --- | --- |
| `物料员存入可用焊丝.md` | 物料员存入可用焊丝 | `materialHandlerLoadAvailableWire` |
| `操作员存入归还焊丝取出可用焊丝.md` | 操作员存入归还焊丝并取出可用焊丝 | `operatorReturnWireAndIssueWire` |
| `物料员打开单个格口.md` | 物料员打开单个格口 | `materialHandlerOpenSingleSlot` |
| `物料员打开所有格口.md` | 物料员打开所有格口 | `materialHandlerOpenAllSlots` |
| `物料员打开所有含有可用焊丝的格口.md` | 物料员打开所有含有可用焊丝的格口 | `materialHandlerOpenAvailableWireSlots` |
| `物料员打开所有含有归还焊丝的格口.md` | 物料员打开所有含有归还焊丝的格口 | `materialHandlerOpenReturnedWireSlots` |

## 命名建议

- 文件名和标题建议统一使用“格口”，仅在明确指门体时使用“格口门”。
- 英文标题建议统一使用 Title Case。
- 流程 ID 建议使用小驼峰，并保留角色、动作、对象三个层次。
