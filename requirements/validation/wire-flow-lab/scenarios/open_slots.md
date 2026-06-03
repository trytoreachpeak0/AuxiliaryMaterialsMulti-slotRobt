# 场景：打开格口（单个 / 全部 / 所有可用 / 所有归还）

流程：`material_handler_open_single_slot` / `material_handler_open_all_slots` /
`material_handler_open_all_available_wire_slots` / `material_handler_open_all_returned_wire_slots`

## 共同循环

开门（单个或批量）→ 更新格口为开门 → `isAllSlotDoorsClosed` 判定：
- 否 → `closeSingleSlotDoor`（在格口面板手动关一扇门，或单步自动关下一扇）→ `updateSlot` → 回到判定
- 是 → `endSuccess`

门状态由 `MockSlotController` 模拟并回写 `app_slot.door_state`，因此关门判定读到真实门状态。

## 各流程开门集合（基于种子数据）

| 流程 | filter | 打开格口 |
|---|---|---|
| 打开单个 | 输入 `selected_slot_id`（默认 1） | A01 |
| 打开所有 | `all_slots` | A01–A08（8 个） |
| 打开所有可用 | `available_wire_slots` | A05, A07 |
| 打开所有归还 | `returned_wire_slots` | A06 |

## 正常路径

选流程 →「开始」→「连续执行」：批量开门后逐个关门，最终 `all_closed=1` → `endSuccess`。
也可「单步」配合右下角格口面板的「开门/关门」按钮手动驱动等待关闭分支。

## 边界

| 场景 | 触发方式 | 期望 |
|---|---|---|
| 无匹配格口 | 打开所有归还时若柜内无 `returned_wire` 格口 | `openAll*` 打开 0 个 → 直接 `isAllSlotDoorsClosed=是` → `endSuccess`（findings：空集合提示 Info） |
| 单个开门失败 | `selected_slot_id` 填一个不存在的 id | `openSingleSlot` → `error` → `endFail` |
