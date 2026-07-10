# 场景：操作员存入归还焊丝并取出可用焊丝

流程：`operator_return_wire_and_issue_available_wire`

## 正常路径（happy path）

| 输入 | 值 |
|---|---|
| 工号 / 班组 / 班次 | `1001` / `A组` / `白班` |
| 归还焊丝批号 | `14Z1888-0160` |
| 机台号 | `1QH029` |
| 剩余芯片数量 | `3000` |
| MES 配额差值 | `0`（米，任意数值即通过） |
| MES 提交结果 | `SUCCESS` |
| 领用工单批次号（操作员手动输入） | 可与机台最近产品批号相同，如 `1QH029-BATCH01`；也可为不同批次，两种取值均应正常通过 |

期望：依次通过操作员校验→批号/规格→匹配可用焊丝→归还重量→机台→（归还用）产品批号→配额校验通过→提交归还成功→**开归还格口/存料/关门**→**操作员手动输入领用工单批次号**（不预填/不复用归还批号）→领用 MES/产品信息→提交领用成功→**开领用格口/取料/关门**→`endSuccess`。

补充验证点：分别用「领用批次号 = 归还批次号」与「领用批次号 ≠ 归还批次号」各跑一遍，确认两种取值都能正常查到产品信息（`queryProductByLotNo`）并提交领用成功，且 `submitWireIssue.v_PrdLot` 使用的是操作员本次手动输入的领用批次号，而非步骤③自动查询的归还批次号。

## 异常分支

| 场景 | 触发方式 | 期望终止节点 |
|---|---|---|
| 操作员不存在 | 工号填 `9999` | `showOPIdNotExistsMessage` |
| 操作员工号多条记录 | MES 同一工号返回多行 | `showOPIdMultipleRecordsMessage` |
| 归还批号不存在 | 批号填 `NOEXIST` | `showWireLotNoInputInvalidOrNotExistsMessage` |
| 无匹配可用焊丝 | 批号用一个规格在柜内无 `available_wire` 的（如先把 A05/A07 占用或换规格） | `showNoMatchedWireMessage` |
| 未配置归还重量 | 归还焊丝规格在 `welding_wire_materials` 无记录 | `showReturnedWeightNotExists` |
| 机台不存在 | 机台填 `9XX999` | `showEqpNoNotExistsMessage` |
| 机台号多条记录 | MES 同一机台号返回多行 | `showEqpNoMultipleRecordsMessage` |
| 配额校验失败（数量有误） | MES 配额错误填 `ORA-20007: 剩余产量不能大于待完工产量！` | `showRemainingQtyWrongHint` 后回到 `inputRemainingQty` 重输 |
| 用量差值查询异常 | MES 返回空结果、其他 ORA-20007 或连接错误 | `showWireQuotaCheckAbnormalMessage`（转人工） |
| 归还数量大于待完工数量 | MES 归还提交抛 `ORA-20007:归还数量1000不能大于产品待完工数量0!` 或 result 含同文案 | `return_qty_reject` → `showRemainingQtyWrongHint` → `inputRemainingQty`（wire-cabinet 回步骤④重输并重新校验） |
| 归还提交失败 | MES 提交结果填 `库存不足` 等业务错误文本 | `showWireReturnFailMessage`（界面展示该文本，单行截断） |
| 领用提交调用失败 | 归还成功后 MES 函数调用异常（error） | `showSubmitWireIssueErrorMessage` |
| 领用提交校验未通过 | 归还成功后把提交结果改为具体错误文本再继续 | `showCheckSubmitWireIssueFailMessage`（界面展示该文本） |
| 查询可用焊丝信息异常 | `queryWireByLotNo` MES 查询 error | `showQueryWireByLotNoErrorMessage` |
| 查询产品批次信息异常 | `queryProductByLotNo` MES 查询 error | `showQueryProductByLotNoErrorMessage` |
| 产品信息校验不通过 | 产品 qty/step 为空（含操作员手动输入的领用批次号查无对应产品信息，如填 `NOEXIST-LOT`） | `showCheckProductInfoNotExistsMessage` |
| 无法打开领用格口 | `openIssueSlot` error | `showOpenIssueSlotErrorMessage` |
| 领用格口库存更新失败 | `updateIssueSlotAfterUnload` error | `showUpdateIssueSlotAfterUnloadErrorMessage` |
| 可用焊丝批号多条记录 | MES 同一批号返回多行 | `showWireByLotNoMultipleRecordsMessage` |
| 可用焊丝信息不存在 | MES 查无领用焊丝信息（empty） | `showWireInfoNotExists` |

## 补充场景：同规格多卷可用焊丝时验证 FEFO 优先发放

验证客户要求：柜内同规格（同 `spec`）存在多卷可用焊丝时，优先发放 shelflife（过期时间）最先到期的一卷。

| 步骤 | 操作 | 说明 |
|---|---|---|
| ① | 切到「物料员」界面，执行「存入可用焊丝」，输入批号 `14Z1888-0555` | 该批号在 MES mock 中为 φ42(MAXSOFT)、shelflife `2026-08-31`（早于柜内已有 A05 的 `14Z1888-0888`，其 shelflife 为 `2026-12-31`），存入任意空闲格口（默认 A01 或 A02） |
| ② | 切回「操作员」界面，执行「归还焊丝并领用可用焊丝」，归还批号 `14Z1888-0160`（φ42(MAXSOFT)） | 此时柜内同规格有两卷可用焊丝：A05（`14Z1888-0888`，2026-12-31 过期）与步骤①存入的格口（`14Z1888-0555`，2026-08-31 过期） |

期望：`queryMatchedAvailableWire` 匹配到的 `matched_available_wire_lot_no` 是过期时间更早的 `14Z1888-0555`，而不是原先按格口号排序会选中的 `14Z1888-0888`，验证 FEFO（最先过期先发放）规则生效。

补充：如需验证「shelflife 为空视为最先发放」，可在步骤①改存一个 MES mock 中 shelflife 为空的同规格批号（或临时在 `v_fw_material_indetail` 追加一条 shelflife 为 `NULL` 的记录），期望该批号被优先匹配。

## 已知问题映射

- 最近产品批号判定恒为 yes（见 findings F-1）。
- 归还/领用物理开门步骤已写入 formal `flow-sql-map` 与 lab `flow.yaml`（findings F-3 已关闭）。
- 仅「剩余产量不能大于待完工产量」走 `quota_reject` → `showRemainingQtyWrongHint` 回环；其他异常走 `showWireQuotaCheckAbnormalMessage`。
- 归还提交时「归还数量…不能大于产品待完工数量」走 `return_qty_reject` → `showRemainingQtyWrongHint` 回环（与步骤④配额错误共用提示节点，文案不同）。
