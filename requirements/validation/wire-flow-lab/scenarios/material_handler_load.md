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
| 批号已在柜内 | 批号 `14Z1888-0888` 已绑定某格口后再次录入同一批号 | `showWireLotNoAlreadyInCabinetMessage`（界面提示「此批号已在柜内（前柜 F-xx）」等，不开门） |

## 异常分支：未关格口恢复

| 场景 | 触发方式 | 期望 |
|---|---|---|
| 中断后仍有格口未关 | 存料流程 `openAvailableSlot` 成功后强制结束 HMI，库表 `door_state` 仍为 `open`；重启后再录入批号 | 界面警告并禁用存料，**不开第二个格口**；关闭该格口后恢复 |
| 中断快照与重启弹窗 | `openAvailableSlot` 成功后杀进程；重启 HMI | 弹窗显示上次格口、批号；门仍开时提示取走焊丝并关门；门已关时提示确认格内实物后重新存料；正常 `updateDB` 后清除快照 |

## 备注

- `checkAvailableSlot` 用查询基数（single/empty）路由，体现「预留归还格口」规则只在 `available/shared` 中选空闲格口。
