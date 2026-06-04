# MH 存入可用焊丝界面 — 分步交互规范（0.1）

## 1. 布局参考

- 视觉与区块划分以 [`WireDispenser.Demo/Views/MhView.xaml`](../../../../WireDispenser.Demo/Views/MhView.xaml) 为准（左栏存料 + 右栏格口网格；1024×768 无滚动，见 [HMI界面约束](HMI界面约束.md)）。
- 业务顺序以流程 `material_handler_load_available_wire` 为准，与 flow-sql-map 段落见 §3。
- MH **格口维护**（单开/批量开）为独立流程，本规范仅覆盖 **存可用焊丝**；维护页可简化交互，但须遵守 [需求范围 §2.6](../需求范围.md) 会话互斥。

## 2. 核心规则（必须实现）

### 2.1 分步点亮

| 规则 | 说明 |
|------|------|
| 初始 | 仅**当前步骤**可交互；后续步骤置灰（建议 `Opacity≈0.45`）且 `IsEnabled=false`。 |
| 前进 | 当前步 **查询/开锁/确认** 成功后锁定该步，再点亮下一步。 |
| 锁定 | 已完成步的主输入与动作按钮不可再改；只读结果区（规格、分配格口）可保留。 |

### 2.2 级联清空

在可编辑步将**焊丝批号**清空时：

1. 清空该步及之后所有步的输入、查询结果、格口分配展示；
2. 流程回退到该步为当前可编辑步；
3. 已锁定更早步不变（存料链仅一步输入时通常无更早锁定项）。

须提供 **「重新开始」**，一次性复位存料流程。

### 2.3 与到站门禁的关系

- **存可用焊丝**适用 [需求范围 §2.2](../需求范围.md)：须车在 **MH 作业站**（`allowed_roles` 含 MH）且顶栏为物料员；否则禁止开锁与写库。
- **MH 格口维护**（§3.3）**不要求到站**（与 OP/MH 存取区分）。

### 2.4 状态提示

当前步骤、校验失败、开锁结果走主窗口底部状态栏（或等价绑定），中文、可恢复。

## 3. 步骤与流程节点映射

| UI 步骤 | 界面区块 | 完成条件（通过后锁定） | flow-sql-map 段落 |
|---------|----------|------------------------|-------------------|
| ① | 焊丝批号输入 +「查询」 | `checkAvailableSlot` → `queryWireSpecByLotNo` → `checkWireLotNoExists` → `checkWireSpecExists` 均通过；已展示规格与分配格口 | `inputWireLotNo` 子图（`inputAvailableWireLotNo` 等） |
| ② | 「打开格口并存放」 | `openAvailableSlot` 成功；提示放入并等待关门 | `openSlotLoadWire` |
| ③ | 关门确认 + 写库 | `closeAvailableSlotDoor` → `updateDB`（`app.slot.bind_wire`）成功；格口 `available_wire` | `openSlotLoadWire` / `updateDB` |

实现时步骤①可在一次「查询」中链式调用多条 SQL，通过后整步锁定。

## 4. 实现建议

- 复用与 OP 相同的 `CompletedThrough` / `ApplyStepGating()` 模式（可参考 Demo `OpStepFlow.cs`）。
- 批号 `TextChanged`：空白时 `ResetFromStep(1)`。
- 异步开锁/写库期间禁用相关按钮，避免触屏连点。

## 5. 验收要点

- [ ] 冷启动仅步骤①（批号+查询）可输入。
- [ ] ①查询通过后②「打开格口」可点，批号不可改。
- [ ] 清空批号后②③复位且禁用。
- [ ] 「重新开始」后回到仅①可输入。
- [ ] 未到站时不可开锁、不可完成写库。
