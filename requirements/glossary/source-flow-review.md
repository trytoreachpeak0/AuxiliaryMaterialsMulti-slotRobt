# 源流程图检查结果

检查范围：`requirements/焊丝发放流程图/` 下全部 6 个 `.md` 文件。

## 本次已修正

- 修正 Mermaid 转义节点写法，统一为标准节点形状，例如 `loadReturnedWire[/"操作员存入归还焊丝"/]`。
- 将 `checkWireInfoExists`、`checkProductInfoExists` 从普通节点改为判断节点 `{...}`。
- 修正 `Material Hanlder` 为 `Material Handler`。
- 修正 `openAllRetunedWireSlots` 为 `openAllReturnedWireSlots`。
- 修正 `checksubmitWireReturnSuccess` 为 `checkSubmitWireReturnSuccess`。
- 统一英文标题大小写，例如 `Open All Slots Containing Returned Wire`。
- 统一用户可见文案中的“OP”为“操作员”。
- 统一“焊线用量真实值”为“焊丝用量实际值”。
- 统一“小于500颗”为“小于等于 500 颗”，与 `checkWireQuotaLE500` 保持一致。
- 统一“最后产品批次号”为“最近生产的产品批次号”。
- 统一“没有找到焊丝规格”为“焊丝规格不存在”。
- 将文件名和标题中的非正式格口简称统一为“格口”。
- 将旧口径统一为“可用焊丝”。
- 为 4 个短流程补齐与主流程一致的 `subgraph` 分段结构。

## 业务确认结果

- “发货柜”为正式设备名称，可发放多种物料，包括焊丝。
- 物料员存入后可供操作员领用的焊丝统一写为“可用焊丝”。
- `switchMHUI`、`switchOPUI` 等节点 ID 中的缩写保留。
- 短流程需要补齐与主流程一致的 `subgraph` 分段结构，已完成。
