# 流程节点 ID 约定

## 通用约定

- 开始节点：`start`
- 结束节点：`End`，建议统一为 `end` 或全项目保留 `End`，不要混用。
- 输入节点：以 `input` 开头，例如 `inputAvailableWireLotNo`。
- 查询节点：以 `query` 开头，例如 `queryWireSpecByLotNo`。
- 判断节点：以 `check` 或 `is` 开头，例如 `checkWireLotNoExists`、`isAllSlotDoorsClosed`。
- 显示节点：以 `show` 开头，例如 `showWireSpecNotExistsMessage`。
- 打开节点：以 `open` 开头，例如 `openAvailableSlot`。
- 关闭节点：以 `close` 开头，例如 `closeReturnSlotDoor`。
- 提交节点：以 `submit` 开头，例如 `submitWireReturn`。
- 更新节点：以 `update` 开头，例如 `updateSlot`。

## 已修正的节点 ID 问题

- `openAllRetunedWireSlots` 拼写错误，已改为 `openAllReturnedWireSlots`。
- `checksubmitWireReturnSuccess` 大小写不一致，已改为 `checkSubmitWireReturnSuccess`。
- `switchMHUI` 和 `switchOPUI` 使用缩写，建议确认是否全项目统一；如需更清晰，可使用 `switchMaterialHandlerUI`、`switchOperatorUI`。
- `showLoadWireAndCloseDoorMessage1`、`showUnloadWireAndCloseDoorMessage2` 通过数字区分语义，建议改为更具体的名称，如 `showLoadReturnedWireAndCloseDoorMessage`、`showUnloadIssueWireAndCloseDoorMessage`。
- `checkWireQuotaLE500` 已移除；用量校验改由 `queryWireQuotaCheck`（Get_Mat_QuotaCheck）直接判定，ORA-20007 时走 `showRemainingQtyWrongHint`。

## Mermaid 形状建议

- 用户输入节点：`[/"..."/]`
- 普通动作节点：`("...")`
- 数据库读写节点：`[("...")]`
- 判断节点：`{"..."}`
- 开始和结束节点：`(["..."])`
