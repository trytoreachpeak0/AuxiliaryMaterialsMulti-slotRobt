# flow-sql-map 维护工具

本目录存放与 `requirements/flows/flow-sql-map/` 配套的脚本与说明。

| 文件 | 用途 |
|------|------|
| [**COMPILE-FLOW-YAML.md**](COMPILE-FLOW-YAML.md) | **正式拆分规格 → 可执行 flow.yaml**：编译、对比、覆盖的完整工作流（主文档） |
| [compile_flow_yaml.py](compile_flow_yaml.py) | 编译与对比脚本 |
| [compile_overrides.yaml](compile_overrides.yaml) | 对比时的可选忽略规则（`skip_nodes`、`normalize_mock_sql` 等） |
| [migrate_subgraph_nodes.py](migrate_subgraph_nodes.py) | 批量将节点 YAML 迁入 `nodes/<subgraph_id>/` 子目录 |
| [apply_output_fields.py](apply_output_fields.py) | 批量补全节点 `output_fields`（一次性迁移用） |

## 日常推荐工作流（维护流程规格）

```text
改正式 nodes/*.yaml 或 index.yaml
    → python compile_flow_yaml.py compare <flow_id>
    → 并排查看 flow.generated.yaml 与 lab/flow.yaml
    → 确认差异后 apply --backup --force
    → 启动 wire-cabinet / wire-flow-lab 验证
```

细节、命令参数、预期差异、与 wire-cabinet 的关系见 **[COMPILE-FLOW-YAML.md](COMPILE-FLOW-YAML.md)**。
