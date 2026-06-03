# 场景：操作员存入归还焊丝并取出可用焊丝

流程：`operator_return_wire_and_issue_available_wire`

## 正常路径（happy path）

| 输入 | 值 |
|---|---|
| 工号 / 班组 / 班次 | `1001` / `A组` / `白班` |
| 归还焊丝批号 | `14Z1888-0160` |
| 机台号 | `1QH029` |
| 剩余芯片数量 | `3000` |
| MES 配额差值 | `0`（≤500） |
| MES 提交结果 | `SUCCESS` |

期望：依次通过操作员校验→批号/规格→匹配可用焊丝(A05/14Z1888-0888)→归还重量(120.5)→机台→最近产品批号(P20260602-01)→配额≤500→提交归还成功→领用焊丝 MES/产品信息→提交领用成功→`showWireIssueSuccessMessage`（success）。

## 异常分支

| 场景 | 触发方式 | 期望终止节点 |
|---|---|---|
| 操作员不存在 | 工号填 `9999` | `showOPIdNotExistsMessage` |
| 归还批号不存在 | 批号填 `NOEXIST` | `showWireLotNoInputInvalidOrNotExistsMessage` |
| 无匹配可用焊丝 | 批号用一个规格在柜内无 `available_wire` 的（如先把 A05/A07 占用或换规格） | `showNoMatchedWireMessage` |
| 未配置归还重量 | 归还焊丝规格在 `app_returned_weight` 无记录 | `showReturnedWeightNotExists` |
| 机台不存在 | 机台填 `9XX999` | `showEqpNoNotExistsMessage` |
| 配额超限 | MES 配额差值填 `600`（>500） | `showRemainingQtyWrongHint` |
| 归还提交失败 | MES 提交结果填 `ERROR: quota` | `showWireReturnFailMessage` |
| 领用提交失败 | 归还成功后把提交结果改为 `ERROR: xxx` 再继续 | `showWireIssueFailMessage` |

## 已知问题映射

- 最近产品批号判定恒为 yes（见 findings F-1）。
- 归还成功后的开门/放料/关门物理步骤未在 map 中（findings F-3）。
- 配额超限在验证版按终止处理，流程图为回环重输（findings F-4）。
