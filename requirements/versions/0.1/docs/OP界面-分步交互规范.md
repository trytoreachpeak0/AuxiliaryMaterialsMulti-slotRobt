# OP 存废取新界面 — 分步交互规范（0.1）

## 1. 布局参考

- 视觉与区块划分以 [`WireDispenser.Demo/Views/OpView.xaml`](../../../../WireDispenser.Demo/Views/OpView.xaml) 为准（步骤徽章 ①～⑥、1024×768 无滚动、工业主题资源）。
- 业务顺序以流程 `operator_return_wire_and_issue_available_wire` 为准，与 Demo 步骤对应关系见 §3。

## 2. 核心规则（必须实现）

### 2.1 分步点亮

| 规则 | 说明 |
|------|------|
| 初始 | 仅**当前步骤**的输入与动作按钮可交互；后续步骤整块置灰（建议 `Opacity≈0.45`），输入 `IsEnabled=false`。 |
| 前进 | 当前步骤**校验通过**后，将该步骤标记为**已完成并锁定**，再点亮下一步。 |
| 锁定 | 已完成步骤的主输入、下拉、校验/查询按钮均不可再改；只读结果区可保留展示。 |
| 动作段 | 步骤⑤「提交归还」、⑥「提交领用」同样遵守：未到达该步前按钮禁用。 |

### 2.2 级联清空

在**可编辑**的某步主输入被**清空**（`Text` 变为空或仅空白）时：

1. 清空该步及**之后所有步骤**的输入、查询结果、格口展示、按钮可用状态；
2. 将流程回退到该步为「当前可编辑步」；
3. **之前已锁定**的步骤保持锁定与已填内容（不回退到更早步骤）。

若需从步骤①重新来过，须提供单独的「重新开始」入口，一次性复位全流程（0.1 建议放在状态栏旁或 OP 页顶）。

### 2.3 与到站门禁的关系

- 未到 **OP 作业站** 或顶栏非操作员：存取禁用，并提示移动至对应站（见 [需求范围 §2.2](../需求范围.md)、[station-config](../wire-cabinet/station-config/README.md)）。
- 已到站：再应用本节分步规则。

### 2.4 状态提示

- 当前应做什么、校验失败原因，走主窗口底部 `StatusText`（或等价绑定），中文、可恢复。

## 3. 步骤与流程节点映射

| UI 步骤 | 界面区块 | 完成条件（校验通过后锁定） | flow-sql-map 段落 |
|---------|----------|---------------------------|-------------------|
| ① | 操作员 ID / 班组 / 班次 | `queryOPById` + `checkOPIdExists` 通过，姓名已展示 | `inputOperatorInfo` |
| ② | 归还焊丝批号 + 结果区 | 批号、规格、匹配可用、归还重量均通过 | `inputReturnedWireLotNoSection` + `queryReturnedWeightSection` |
| ③ | 机台号 + 结果区 | 机台存在且最近产品批存在 | `inputEqpNoSection` + `queryLastProductLotNoSection` |
| ④ | 剩余芯片 / 用量差 | `queryWireQuotaCheck` 成功（返回差值，米） | `inputRemainingQtySection` |
| ⑤ | 提交归还 + 开门放料 | `submitWireReturn` 成功；含 `findReturnSlot`→`openReturnSlot`→关门→`bind_returned_wire` | `submitWireReturnSection` + `loadReturnedWireSection` |
| ⑥ | 提交领用 + 开门取料 | `submitWireIssue` 成功；含 `openIssueSlot`→关门→`complete_issue_pickup` | `issueWire` 子图（`submitWireIssueSection` 等） |

步骤②结果区在 Demo 中合并展示；实现时可在一次「查询」链式调用后锁定整步。  
步骤⑤⑥在 Demo 上各有一个主按钮，**正式实现须按** [需求范围 §3.2](../需求范围.md) 执行完整开门/关门/格口 SQL 子步骤，不得仅 UI 占位成功。  
`submitWireReturn` / `submitWireIssue` 失败时，界面须展示 MES 存储函数返回的具体错误文本或 Oracle 异常信息（`SQLERRM`），不得仅显示笼统的「提交失败」。

## 4. 实现建议

- ViewModel 维护 `CompletedThrough`（已完成并锁定的最大步骤索引，初始 `-1`）与 `ApplyStepGating()` 统一刷新各步 `IsEnabled` / 透明度。
- 主输入挂 `TextChanged`：若 `string.IsNullOrWhiteSpace` 则 `ResetFromStep(stepIndex)`。
- 校验/查询/提交用 `async` + 进行中禁用按钮，避免触屏连点。
- Demo 参考实现：`WireDispenser.Demo/Views/OpStepFlow.cs` + `OpView.xaml.cs`（占位校验，接真 MES 时替换按钮处理即可）。

## 5. 验收要点

- [ ] 冷启动仅步骤①可输入。
- [ ] ①通过后②亮、①不可改。
- [ ] 在②清空批号后，②及以后清空且禁用，①仍锁定。
- [ ] 「重新开始」后回到仅①可输入。
- [ ] 未到站时全流程不可提交开锁。
