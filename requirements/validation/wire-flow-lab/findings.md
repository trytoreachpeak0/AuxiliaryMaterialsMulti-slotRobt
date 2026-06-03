# findings — 焊丝发放流程验证结论

> 由 `WireFlowLab` 验证工具（WPF + SQLite + Oracle 回退）在 SQLite mock 模式下端到端跑通全部 6 个流程后整理。
> 工具内置静态校验（断链/缺 SQL/入参来源/不可达/分支不全）+ 运行时 trace + findings 导出。
> 自测原始 trace 见 `db/selftest-output.txt`，可用界面「无界面自测 / 校验全部流程 / 导出 findings」复现。

## 一、整体结论

- 6 个流程在补齐应用库/格口 SQL 后均可由 YAML 驱动逐节点跑通主路径，分支路由与流程图一致。
- 此前 `data-access-sql/flow-sql-map` 中 `catalog_status: pending_catalog` / `sql_ids: []` 的 18 个应用库/格口节点，已在本验证版 `data-access-sql/sql-catalog/items/app.*.yaml` 补齐 SQLite SQL，并被引擎成功解析执行。
- MES 7 条 Oracle SQL / 存储函数已改写为 `mes_mock.*` 的 SQLite 等价版本（原生 Oracle 以 `oracle_sql` 保留），可在界面切到 Oracle 模式对接真实库。

## 二、流程图 vs flow-sql-map 的差异（需求侧需确认）

| 编号 | 流程 / 节点 | 问题 | 建议 |
|---|---|---|---|
| F-1 | operator / `checkLastProductLotNoExists` | `mes.product.get_latest_product_lot_by_eqp` 用 `NVL(MAX(lot),'N/A')` 恒返回一行（可能为 `N/A`）。map 的判定语义是「非空即可」，于是该判定**恒为 yes**，永远走不到流程图里的「否→End」。 | 明确判定到底是「lot 存在」还是「lot 非 N/A」。若业务要求必须有真实批号，判定应改为 `lot <> 'N/A'`。 |
| F-2 | operator / `queryMatchedAvailableWire` | `outcome_routing` 把 `empty` 与 `single` 都路由到同一判定节点 `checkMatchedWireExists`，靠判定再分流。逻辑可行，但「空结果」与「正常单行」在路由层未区分，排障时不直观。 | 可接受；建议在 catalog 注明 empty 语义，或单独给 empty 一个提示分支。 |
| F-3 | operator / 归还成功后的物理动作 | 流程图在 `归还提交成功` 后还有「开归还格口 → 放入焊丝 → 关门」再进入领用；但 `flow-sql-map` 的 `node_order` 未建模这些开门/关门/等待关闭节点，直接从 `checkSubmitWireReturnSuccess` 跳到 `queryWireByLotNo`。 | 若需要工具/系统真正驱动归还格口仓门，应在 map 中补 `openReturnSlot / closeReturnSlotDoor / isAllSlotDoorsClosed` 等节点（可复用打开格口流程的循环）。 |
| F-4 | operator / `showRemainingQtyWrongHint` | 流程图为「配额>500 → 提示数量有误 → 返回重输数量」（回环）；map 未建模回环，验证版按失败终止处理。 | 若要支持重输，map 应将该提示节点 `success` 路由回 `inputRemainingQty`。 |
| F-5 | operator / `queryProductByLotNo` | `SUM/MAX` 聚合查询无匹配行时仍返回一行但 `qty/step` 为空，必须靠 `checkProductInfoExists` 判空，否则会把空值当成功提交领用。 | 已正确建模为 `all_fields_not_empty`；提醒实现端务必判空。 |

## 三、sql-catalog / schema 数据一致性问题

> 核对范围：本目录 `data-access-sql/`（SQLite 验证镜像）及正式目录 `../../data-access-sql/`。已解决项在下方记录回写位置；仍需推动正式目录的见 C-5。

| 编号 | 位置（本验证目录） | 问题 | 状态 / 回写说明 |
|---|---|---|---|
| C-1 | （镜像无对应文件；见正式 `../../data-access-sql/sql-catalog/items/mes.wire_quota.check_quota.yaml`） | 正式 catalog 中 `status: verfied` 拼写错误。 | **已解决**（2026-06-03）：正式目录已改为 `verified`；本镜像 `mes_mock.wire_quota.check_quota.yaml` 无 `status` 字段，不适用。 |
| C-2 | `data-access-sql/schema/tables/v_fw_wip_sublot.yaml` | 表定义 `name` 与 `schema/index.yaml` 索引名不一致。 | **已解决**：本镜像 `name: v_fw_wip_sublot`，与 `data-access-sql/schema/index.yaml` 一致。 |
| C-3 | `data-access-sql/schema/tables/fw_wip_trans.yaml` | `mes_mock.product.get_latest_product_lot_by_eqp` 按 `dates` 排序，schema 需声明该列。 | **已解决**：本镜像 schema 与 `db/schema.sqlite.sql` 均已含 `dates` 列。 |
| C-4 | `data-access-sql/sql-catalog/items/mes_mock.operator.get_by_id.yaml` | SQL 别名 `user_name` 与正式 catalog / flow 判定字段 `operator_name` 不一致。 | **已解决**（2026-06-03）：`sql` / `oracle_sql` / `outputs` / `sample_result` 已统一为 `operator_name`（对齐正式 `mes.operator.get_by_id`）。 |
| C-5 | 正式 `../../data-access-sql/schema/` | 正式 schema 仅有 MES 表，缺格口/归还重量等应用库表。 | **待处理**：本镜像已具备 `app_slot` / `app_returned_weight` / `app_operation_log` 及 enums，待回写正式目录。 |

## 四、缺 SQL 节点的补齐对照

| 原 pending 节点 | 补齐的 SQLite catalog 条目 |
|---|---|
| `queryMatchedAvailableWire` | `app.wire_inventory.find_matched_available` |
| `queryWireReturnedWeightBySpec` | `app.returned_weight.get_by_spec` |
| `checkAvailableSlot` | `app.slot.find_available` |
| `openSingleSlot / openAvailableSlot` | `app.slot.open`（+ `app.slot.get_status`） |
| `openAllSlots / openAllAvailableWireSlots / openAllReturnedWireSlots` | `app.slot.list_by_filter` + `app.slot.open` |
| `updateDB` | `app.slot.bind_wire` |
| `updateCorrespondingSlot(s) / updateAllSlots / updateSlot` | `app.slot.update_state` |
| `isAllSlotDoorsClosed` | `app.slot.are_all_doors_closed` |

## 五、验证范围与限制

- 主路径与主要异常分支均可在 mock 模式离线验证；异常分支用例见 `scenarios/`。
- MES 存储函数（配额核对、归还/领用提交）在 mock 模式下用界面可配置结果驱动；真实 Oracle 行为需切到 Oracle 模式并提供连接串后验证。
- 工具的格口门状态由 `MockSlotController` 模拟并回写 `app_slot.door_state`，因此 `app.slot.are_all_doors_closed` 读到的是真实门状态。
