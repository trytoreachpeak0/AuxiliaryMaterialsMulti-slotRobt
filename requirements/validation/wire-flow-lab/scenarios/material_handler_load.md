# 场景：物料员存入可用焊丝

流程：`material_handler_load_available_wire`

## 正常路径

| 输入 | 值 |
|---|---|
| 可用焊丝批号 | `14Z1888-0888` |

期望：`checkAvailableSlot` 找到空闲格口 A01 → MES 校验批号/规格(φ42, 可用) → `openAvailableSlot` 打开 A01（格口面板门变红）→ 在面板点「关门」或单步 `closeAvailableSlotDoor` → `updateDB` 把焊丝绑定到 A01（biz_state=available_wire）→ `endSuccess`。

## 异常分支

| 场景 | 触发方式 | 期望终止节点 |
|---|---|---|
| 无空闲格口 | 先把 A01/A02/A03 全部置为非 idle（或用界面把它们绑定焊丝），使 `app.slot.find_available` 返回空 | `showNoAvailableSlotMessage` |
| 批号不存在 | 批号填 `NOEXIST` | `showWireLotNoInputInvalidOrNotExistsMessage` |
| 规格缺失 | 批号对应 MES 记录 spec 为空 | `showWireSpecNotExistsMessage` |

## 备注

- `checkAvailableSlot` 用查询基数（single/empty）路由，体现「预留归还格口」规则只在 `available/shared` 中选空闲格口。
