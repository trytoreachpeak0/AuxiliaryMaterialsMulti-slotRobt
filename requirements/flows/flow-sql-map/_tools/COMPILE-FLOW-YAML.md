# compile_flow_yaml.py — 使用方式与工作流

将正式 **拆分规格**（`index.yaml` + `nodes/**/*.yaml`）编译为 wire-cabinet / wire-flow-lab **引擎可执行的合并 `flow.yaml`**，并与现有目标文件做结构化对比；**仅在显式 `apply --force` 时覆盖目标**。

---

## 1. 为什么需要这个工具

仓库里存在两套 flow 定义格式：

| 层级 | 路径 | 格式 | 职责 |
|------|------|------|------|
| **正式规格** | `requirements/flows/flow-sql-map/flows/<flow_id>/` | `index.yaml` + `nodes/<subgraph>/<node>.yaml` | 与流程图对齐、评审、长期维护（权威来源） |
| **可执行快照** | `requirements/validation/wire-flow-lab/.../flows/<flow_id>/flow.yaml` | 单文件顶层 `nodes:` | 引擎直接加载执行 |

wire-cabinet 的 `FlowLoader` 读取正式 `index.yaml` 时，因**没有顶层 `nodes:`**，会**回退**到 lab 目录下的 `flow.yaml`。因此：

- 只改正式 `nodes/*.yaml` → **运行时行为不会变**
- 经本工具 `compare` 确认后 `apply` 更新 lab 的 `flow.yaml` → **运行时才会变**

```text
正式 flow-sql-map（拆分）     ← 你维护的规格源
        │
        │  compile_flow_yaml.py compare / apply
        ▼
lab flow.yaml（合并）          ← wire-cabinet 实际执行（回退读取）
        +
正式 data-access-sql/sql-catalog   ← SQL 已接通，会覆盖 lab 同名 sql_id
```

---

## 2. 依赖

```bash
pip install pyyaml
```

在**仓库根目录**执行下方命令（路径与所在目录无关）。

---

## 3. 命令一览

### 3.1 `compare` — 编译并对比（默认，不覆盖）

```bash
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py compare <flow_id>
```

作用：

1. 从正式规格编译生成 `flow.generated.yaml`
2. 与目标 `flow.yaml` 做结构化 diff
3. 在终端打印差异摘要

常用选项：

| 选项 | 说明 |
|------|------|
| `--target PATH` | 指定对比用的 `flow.yaml` 完整路径 |
| `--target-dir PATH` | 指定目标目录（默认 lab 的 `flows/<flow_id>/`） |
| `--out PATH` | 生成版写出路径（默认 `<target_dir>/flow.generated.yaml`） |
| `--normalize-mock-sql` | 对比时将 `mes_mock.*` 与 `mes.*` 视为等价 |
| `--skip-node ID` | 对比时忽略某节点（可重复） |
| `--report-json PATH` | 额外输出 JSON 格式差异报告 |

示例：

```bash
# 物料员存焊丝
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py compare material_handler_load_available_wire

# 操作员存废取新（忽略 mock SQL 前缀差异）
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py compare operator_return_wire_and_issue_available_wire --normalize-mock-sql

# 导出 JSON 报告
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py compare material_handler_load_available_wire --report-json /tmp/diff.json
```

### 3.2 `generate` — 仅生成，不对比

```bash
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py generate <flow_id> --out <输出路径>
```

### 3.3 `compare-all` — 对比 index 中全部 6 条 flow

```bash
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py compare-all
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py compare-all --normalize-mock-sql
```

### 3.4 `apply` — 确认后覆盖目标

```bash
python requirements/flows/flow-sql-map/_tools/compile_flow_yaml.py apply <flow_id> --backup --force
```

| 行为 | 说明 |
|------|------|
| 默认 | 先执行 `compare`；**有差异则中止，不覆盖** |
| `--force` | 忽略差异，用生成版覆盖目标 `flow.yaml` |
| `--backup` | 覆盖前将原 `flow.yaml` 备份为 `flow.yaml.bak.<UTC时间>` |

**不要**在未查看 `compare` 结果的情况下使用 `--force`。

---

## 4. 默认路径

| 角色 | 路径 |
|------|------|
| 流程索引 | `requirements/flows/flow-sql-map/index.yaml` |
| 正式规格 | `requirements/flows/flow-sql-map/flows/<flow_id>/index.yaml` + `nodes/**` |
| 对比/覆盖目标（默认） | `requirements/validation/wire-flow-lab/data-access-sql/flow-sql-map/flows/<flow_id>/flow.yaml` |
| 生成版（compare 默认） | 同上目录下的 `flow.generated.yaml` |

`flow.generated.yaml` 为本地对比产物，可加入 `.gitignore` 或提交前自行删除；**不要**与 `flow.yaml` 混淆。

当前 6 个 `flow_id`：

- `operator_return_wire_and_issue_available_wire`
- `material_handler_load_available_wire`
- `material_handler_open_single_slot`
- `material_handler_open_all_available_wire_slots`
- `material_handler_open_all_returned_wire_slots`
- `material_handler_open_all_slots`

---

## 5. 推荐工作流

```text
┌─────────────────────────────────────────────────────────────┐
│ 1. 修改正式规格                                              │
│    flows/<flow_id>/nodes/<subgraph>/<node>.yaml             │
│    或 index.yaml（node_order / node_files / sections）       │
└───────────────────────────┬─────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. compare                                                  │
│    python .../compile_flow_yaml.py compare <flow_id>        │
└───────────────────────────┬─────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. 审阅差异                                                  │
│    · 终端摘要                                                │
│    · IDE 并排：flow.generated.yaml  vs  flow.yaml           │
│    · 可选：--report-json / compile_overrides.yaml            │
└───────────────────────────┬─────────────────────────────────┘
                            ▼
              ┌─────────────┴─────────────┐
              │ 差异可接受？               │
              └─────────────┬─────────────┘
                    否      │      是
                    ▼       │       ▼
         继续改正式规格      │   apply --backup --force
                            │       │
                            │       ▼
                            │   wire-cabinet / wire-flow-lab 验证
                            └───────┘
```

### 步骤说明

1. **改正式规格**  
   在 `requirements/flows/flow-sql-map/` 下维护；与 Mermaid 流程图、`sql-catalog` 保持一致。

2. **运行 compare**  
   生成 `flow.generated.yaml`，打印差异；**不会修改** `flow.yaml`。

3. **审阅差异**  
   - 终端：仅生成版 / 仅目标版有的节点、`node_order`、各节点字段 diff  
   - 编辑器：并排 diff 两个 yaml  
   - 可选：编辑 [`compile_overrides.yaml`](compile_overrides.yaml) 配置 `skip_nodes`（见 §7）

4. **apply（可选）**  
   确认后：`apply <flow_id> --backup --force`  
   更新 lab 的 `flow.yaml` → wire-cabinet 回退加载的内容随之更新。

5. **验证**  
   启动 wire-cabinet 或 wire-flow-lab，跑对应用例 / 自测。

---

## 6. 对比报告读法

`compare` 退出码：`0` = 在所用规则下完全一致；`1` = 有差异。

报告包含：

| 区块 | 含义 |
|------|------|
| `仅生成版有的节点` | 正式路由引用或编译产生的节点，目标里没有 |
| `仅目标版有的节点` | lab 快照独有（如 `endSuccess` / `endFail`） |
| `顶层字段` | `title`、`diagram`、`start`、`context_defaults` 等 |
| `node_order` | 主路径节点顺序是否一致 |
| `节点级差异` | 每节点的 `type`、`sql_id`、`routing`、`inputs`、`check`、`text` 等 |

---

## 7. compile_overrides.yaml

对比时的可选规范化，**不修改**源文件，只影响 diff 报告。

```yaml
# 全局：对比时 mes_mock.* 与 mes.* 等价
normalize_mock_sql: false

flows:
  operator_return_wire_and_issue_available_wire:
    skip_nodes:
      - showOPName   # 流程图展示节点；lab 已内联到 checkOPIdExists → inputReturnedWireLotNo
  material_handler_load_available_wire:
    skip_nodes:
      - endSuccess
      - endFail      # 正式路由到 End；lab 用独立 terminal 节点
```

也可用命令行 `--skip-node` / `--normalize-mock-sql` 临时覆盖。

---

## 8. 编译规则摘要

| 正式 `access_type` | 引擎 `type` |
|--------------------|-------------|
| `user_input` / `ui_message` | `user_input` |
| `mes_sql` | `query`（`data_source: mes`） |
| `mes_function` | `write`（`data_source: mes`） |
| `application_db` | `query` 或 `write`（按 sql-catalog 的 `operation`） |
| `application_db_and_slot_control` | `slot_open` / `slot_close` / `slot_open_batch` |
| `decision` | `decision` + `check` |

其他映射：

- `input_mappings` → `inputs[].from`（`node:`、`const:`、`context:`、`system:`）
- `outcome_routing` → `routing`（`application_db` 查询节点：`yes/no` → `single/empty`）
- 路由引用但无节点 yaml 的目标 → 生成 `terminal` 占位
- `context_defaults`：OP 流程内置 `return_task` / `issue_task` / `return_remark`
- PyYAML 会把 `yes`/`no` 读成布尔值；脚本已做还原处理

---

## 9. 预期差异（对比时常见）

以下差异**不一定表示正式规格错误**，需结合业务判断后再 `apply`：

| 差异 | 说明 | 处理建议 |
|------|------|----------|
| `mes.wire.*` vs `mes_mock.*` | 正式 catalog vs lab mock | `--normalize-mock-sql` 或接受后统一 |
| `showOPName` | 正式有展示节点，lab 已跳过 | `compile_overrides.yaml` 中 `skip_nodes` |
| 关门节点 `user_input` vs `slot_close` | 正式与流程图一致，lab 用硬件关门节点 | 决定是否改 lab 或改编译规则 |
| `updateDB` 入参名 `spec` vs `wire_spec` 等 | 字段命名约定不同 | 对照 `sql-catalog` 的 `inputs.name` |
| 终点 `End` vs `endSuccess`/`endFail` | 正式路由到 `End`，lab 用 terminal | 可 `skip_nodes` 或 apply 后改引擎 |
| `text` / `label` / `default` 文案 | 展示层差异 | 通常可忽略或 apply 统一 |

---

## 10. 与 wire-cabinet / wire-flow-lab 的关系

| 组件 | 读取内容 |
|------|----------|
| wire-cabinet `FlowLoader` | 正式 `index.yaml` → 无 `nodes` → **回退 lab `flow.yaml`** |
| wire-flow-lab `FlowLoader` | 直接读 lab `index.yaml` 指向的 `flow.yaml` |
| `SqlCatalog` | lab + 正式 `sql-catalog`（**正式同名 id 覆盖 lab**） |

因此：

- **流程编排**：以本工具同步正式规格 → lab `flow.yaml` 为主  
- **SQL 语句**：改 `requirements/data-access-sql/sql-catalog/items/` 即可生效，无需本工具  

---

## 11. 故障排查

| 现象 | 可能原因 |
|------|----------|
| `正式流程索引不存在` | `flow_id` 拼写错误或正式目录未建 |
| `节点文件不存在` | `index.yaml` 的 `node_files` 与磁盘路径不一致 |
| compare 报大量 `terminal` 文案差异 | 正式未单独维护 terminal 节点 yaml，生成版用占位文案 |
| apply 未覆盖 | 未加 `--force`；先看 compare 退出码与终端提示 |
| 改了正式规格但运行不变 | 未执行 `apply`，或 wire-cabinet 未重启 |

---

## 12. 相关文档

- [flow-sql-map/README.md](../README.md) — 拆分目录约定与节点 I/O 规范
- [SUBGRAPH-NODES.md](../SUBGRAPH-NODES.md) — subgraph 与目录对齐
- [requirements/versions/0.1/需求范围.md](../../../versions/0.1/需求范围.md) — 0.1 业务流程范围
