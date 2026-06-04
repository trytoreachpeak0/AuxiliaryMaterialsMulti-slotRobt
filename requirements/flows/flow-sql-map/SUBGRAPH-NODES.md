# subgraph 节点目录约定

本文件与 [`.cursor/rules/flow-sql-map-subgraph-nodes.mdc`](../../../.cursor/rules/flow-sql-map-subgraph-nodes.mdc) 内容一致，供人工查阅；AI 维护 flow-sql-map 时会加载该规则。

## 目录结构

```text
flows/<flow_id>/
  index.yaml                    # sections + node_files + node_order
  nodes/<subgraph_id>/          # subgraph_id = Mermaid subgraph 的 id
    <node_id>.yaml
```

**操作员流程**示例（嵌套 subgraph 取**叶子**目录）：

```text
nodes/inputOperatorInfo/inputOperatorWorkInfo.yaml
nodes/inputReturnedWireLotNoSection/queryWireSpecByLotNo.yaml
nodes/queryWireAndProductSection/queryWireByLotNo.yaml
```

**物料员流程**示例：

```text
nodes/inputWireLotNo/checkAvailableSlot.yaml
nodes/openSlot/closeSingleSlotDoor.yaml
```

## 与流程图同步 checklist

- [ ] `diagrams/*.md` 每个段落有 `subgraph <id>[标题]`
- [ ] `index.yaml` 的 `sections` 覆盖所有 subgraph（含暂无 YAML 的 UI 段）
- [ ] 每个有 spec 的节点在 `node_files` 且路径为 `nodes/<subgraph_id>/<node_id>.yaml`
- [ ] 节点 YAML 含 `subgraph_id`，与所在目录名一致
- [ ] 嵌套 subgraph 在 `sections` 用 `parent_subgraph_id` 关联父段

## 迁移脚本

```bash
python requirements/flows/flow-sql-map/_tools/migrate_subgraph_nodes.py
```

修改脚本内 `FLOW_SUBGRAPHS` 映射后执行，会移动文件并更新各文件 `subgraph_id`。

## 当前各流程 subgraph 一览

| flow_id | subgraph_id（有 YAML） |
|---------|------------------------|
| `operator_return_wire_and_issue_available_wire` | `inputOperatorInfo`, `inputReturnedWireLotNoSection`, `queryReturnedWeightSection`, `inputEqpNoSection`, `queryLastProductLotNoSection`, `inputRemainingQtySection`, `submitWireReturnSection`, `queryWireAndProductSection`, `submitWireIssueSection` |
| `material_handler_load_available_wire` | `inputWireLotNo`, `openSlotLoadWire` |
| `material_handler_open_single_slot` | `openSlot`, `closeSlotDoor` |
| `material_handler_open_all_slots` | `openSlots`, `closeSlotDoors` |
| `material_handler_open_all_available_wire_slots` | `openAvailableWireSlots`, `closeSlotDoors` |
| `material_handler_open_all_returned_wire_slots` | `openReturnedWireSlots`, `closeSlotDoors` |

暂无 YAML、仅在流程图与 `sections` 占位的段（操作员流程）：`switchUI`, `returnWire`, `loadReturnedWireSection`, `issueWire`, `unloadIssueWireSection`。
