#!/usr/bin/env python3
"""
从正式 flow-sql-map（index.yaml + nodes/**/*.yaml）编译为 wire-cabinet / wire-flow-lab
可执行的合并 flow.yaml，并与现有目标文件做结构化 diff；仅在显式 --apply 时覆盖目标。

用法:
  python compile_flow_yaml.py compare [flow_id ...] [--target-dir PATH] [--out PATH]
  python compile_flow_yaml.py generate flow_id --out PATH
  python compile_flow_yaml.py apply flow_id [--target-dir PATH] [--backup]

默认对比/覆盖目标:
  requirements/validation/wire-flow-lab/data-access-sql/flow-sql-map/flows/<flow_id>/flow.yaml
"""
from __future__ import annotations

import argparse
import copy
import json
import re
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import yaml

ROOT = Path(__file__).resolve().parents[3]  # requirements/
FORMAL_MAP_ROOT = ROOT / "flows" / "flow-sql-map"
FORMAL_INDEX = FORMAL_MAP_ROOT / "index.yaml"
LAB_MAP_ROOT = ROOT / "validation" / "wire-flow-lab" / "data-access-sql" / "flow-sql-map"
SQL_CATALOG_DIR = ROOT / "data-access-sql" / "sql-catalog" / "items"
OVERRIDES_FILE = Path(__file__).resolve().parent / "compile_overrides.yaml"

FLOW_CONTEXT_DEFAULTS: dict[str, dict[str, str]] = {
    "operator_return_wire_and_issue_available_wire": {
        "return_task": "归还",
        "issue_task": "领用",
        "return_remark": "",
    },
}

TERMINAL_SUCCESS_IDS = frozenset({"End", "endSuccess"})
TERMINAL_FAIL_NAME_RE = re.compile(
    r"^(show.+Message|show.+Hint)$", re.IGNORECASE
)
# PyYAML 会把 yes/no 解析为 True/False，读 outcome_routing 时必须还原
_ROUTING_KEY_CANON = {True: "yes", False: "no"}


def canon_routing_key(key: Any) -> str:
    if key in _ROUTING_KEY_CANON:
        return _ROUTING_KEY_CANON[key]
    return str(key)


UI_MESSAGE_AS_USER_INPUT = frozenset({
    "showOPName",
    "showWireReturnSuccessMessage",
    "showLoadWireAndCloseDoorMessage",
    "showWireIssueSuccessMessage",
    "showUnloadWireAndCloseDoorMessage",
    "showRemainingQtyWrongHint",
})


def load_yaml(path: Path) -> Any:
    with path.open(encoding="utf-8") as f:
        return yaml.safe_load(f) or {}


def dump_yaml(data: Any) -> str:
    return yaml.dump(
        data,
        allow_unicode=True,
        sort_keys=False,
        default_flow_style=False,
        width=120,
    )


def repo_flow_ids() -> list[str]:
    index = load_yaml(FORMAL_INDEX)
    return [e["flow_id"] for e in index.get("flows_index", []) if e.get("flow_id")]


def default_target_path(flow_id: str, target_dir: Path | None) -> Path:
    base = target_dir or (LAB_MAP_ROOT / "flows" / flow_id)
    return base / "flow.yaml"


def load_sql_operations() -> dict[str, str]:
    ops: dict[str, str] = {}
    if not SQL_CATALOG_DIR.is_dir():
        return ops
    for path in SQL_CATALOG_DIR.glob("*.yaml"):
        doc = load_yaml(path)
        sql_id = doc.get("id")
        if sql_id:
            ops[sql_id] = (doc.get("operation") or "read").lower()
    return ops


def load_overrides() -> dict[str, Any]:
    if OVERRIDES_FILE.is_file():
        return load_yaml(OVERRIDES_FILE) or {}
    return {}


def formal_flow_dir(flow_id: str) -> Path:
    return FORMAL_MAP_ROOT / "flows" / flow_id


def load_formal_index(flow_id: str) -> dict[str, Any]:
    path = formal_flow_dir(flow_id) / "index.yaml"
    if not path.is_file():
        raise FileNotFoundError(f"正式流程索引不存在: {path}")
    return load_yaml(path)


def load_formal_node(flow_dir: Path, rel_path: str) -> dict[str, Any]:
    path = flow_dir / rel_path
    if not path.is_file():
        raise FileNotFoundError(f"节点文件不存在: {path}")
    doc = load_yaml(path)
    if not isinstance(doc, dict):
        raise ValueError(f"节点 YAML 格式错误: {path}")
    return doc


def diagram_path(index: dict[str, Any]) -> str:
    rel = index.get("diagram") or index.get("file") or ""
    if rel.startswith("requirements/"):
        return rel
    if rel.startswith("diagrams/"):
        return f"requirements/flows/{rel}"
    return rel


def expand_field_names(field_expr: str) -> list[str]:
    return [p.strip() for p in str(field_expr).split("/") if p.strip()]


def mapping_to_from(im: dict[str, Any]) -> str | None:
    st = im.get("source_type")
    if st == "constant":
        val = im.get("value", "")
        if isinstance(val, str) and (val == "" or " " in val):
            return f'const:"{val}"'
        return f"const:{val}"
    if st == "context":
        return f"context:{im.get('source_field', '')}"
    if st == "system_parameter":
        field = im.get("source_field", "")
        if field == "configured_mes_writer":
            return "system:configured_mes_writer"
        return f"system:{field}"
    if st == "system":
        return f"system:{im.get('source_field', '')}"
    if st == "application_db":
        return f"context:{im.get('source_field', '')}"
    if st == "ui_input":
        return None
    node = im.get("source_node")
    field = im.get("source_field")
    if node and field:
        parts = expand_field_names(field)
        if len(parts) == 1:
            return f"node:{node}.{parts[0]}"
        return None
    return None


def build_inputs(node: dict[str, Any]) -> list[dict[str, str]]:
    inputs: list[dict[str, str]] = []
    for im in node.get("input_mappings") or []:
        name = im.get("name")
        if not name:
            continue
        field_expr = im.get("source_field", "")
        node_id = im.get("source_node")
        if node_id and field_expr and "/" in field_expr:
            for part in expand_field_names(field_expr):
                inputs.append({"name": part, "from": f"node:{node_id}.{part}"})
            continue
        if name == "wire_info" and node_id:
            for part in ("spec", "code", "shelflife", "matlot", "qty", "state"):
                inputs.append({"name": part, "from": f"node:{node_id}.{part}"})
            continue
        frm = mapping_to_from(im)
        if frm:
            inputs.append({"name": name, "from": frm})
    return inputs


def primary_sql_id(node: dict[str, Any]) -> str | None:
    sql_ids = node.get("sql_ids") or []
    if sql_ids:
        return sql_ids[0]
    for of in node.get("output_fields") or []:
        if of.get("source_type") == "sql_catalog" and of.get("source_ref"):
            return of["source_ref"]
    for im in node.get("input_mappings") or []:
        ref = im.get("source_ref")
        if ref:
            return ref
    return None


def infer_engine_type(node: dict[str, Any], sql_ops: dict[str, str]) -> str:
    access = node.get("access_type", "")
    node_id = node.get("node_id", "")
    sql_id = primary_sql_id(node)

    if access == "user_input":
        return "user_input"
    if access == "ui_message":
        return "user_input"
    if access == "decision":
        return "decision"
    if access == "mes_sql":
        return "query"
    if access == "mes_function":
        return "write"
    if access == "application_db_and_slot_control":
        if sql_id and "close" in (sql_id + node_id).lower():
            return "slot_close"
        if "batch" in node_id.lower() or (node.get("batch") == "all"):
            return "slot_open_batch"
        return "slot_open"
    if access == "application_db":
        if sql_id:
            op = sql_ops.get(sql_id, "read")
            if op in ("write", "function"):
                return "write"
            # 有 sql_catalog 引用时优先视为 query（即使带 decision_rule 说明）
            return "query"
        if node.get("decision_rule"):
            return "decision"
        return "query"
    return "unknown"


def sql_ids_nonempty(node: dict[str, Any]) -> bool:
    return bool(node.get("sql_ids"))


def build_check(node: dict[str, Any]) -> tuple[dict[str, Any] | None, bool]:
    """返回 (check, invert_yes_no_routing)。"""
    rule = node.get("decision_rule") or {}
    if rule.get("result_field"):
        ims = node.get("input_mappings") or []
        if ims:
            sn = ims[0].get("source_node", "")
            sf = ims[0].get("source_field", rule["result_field"])
            return {"kind": "submit_success", "ref": f"{sn}.{sf}"}, False
    success_rules = rule.get("success_when") or []
    condition = success_rules[0].get("condition") if success_rules else None
    field = success_rules[0].get("field") if success_rules else None

    if condition == "exists":
        for im in node.get("input_mappings") or []:
            if im.get("source_node"):
                return {"kind": "exists", "node": im["source_node"]}, False
        return {"kind": "exists", "field": field}, False

    if condition == "is_null_or_empty":
        for im in node.get("input_mappings") or []:
            if im.get("source_node"):
                # 与 lab 一致：exists 判定 + 交换 yes/no 路由
                return {"kind": "exists", "node": im["source_node"]}, True

    if condition in ("is_not_null_or_empty", "field_not_empty", "equals_after_normalize"):
        for im in node.get("input_mappings") or []:
            sn = im.get("source_node")
            sf = im.get("source_field")
            if sn and sf:
                first = expand_field_names(sf)[0]
                return {"kind": "field_not_empty", "ref": f"{sn}.{first}"}, False
        if field:
            return {"kind": "field_not_empty", "ref": field}, False

    failure_rules = rule.get("failure_when") or []
    if condition == "otherwise" and failure_rules:
        na_equals = next(
            (
                f
                for f in failure_rules
                if f.get("condition") == "equals" and f.get("value") is not None
            ),
            None,
        )
        has_empty = any(f.get("condition") == "is_null_or_empty" for f in failure_rules)
        if na_equals and has_empty:
            for im in node.get("input_mappings") or []:
                sn = im.get("source_node")
                sf = im.get("source_field")
                if sn and sf:
                    first = expand_field_names(sf)[0]
                    return {
                        "kind": "field_not_empty_and_not_equals",
                        "ref": f"{sn}.{first}",
                        "value": na_equals["value"],
                    }, False
    return None, False


def build_outputs(node: dict[str, Any]) -> list[dict[str, Any]]:
    outputs: list[dict[str, Any]] = []
    access = node.get("access_type")
    node_id = node.get("node_id", "")

    if access in ("user_input", "ui_message"):
        for of in node.get("output_fields") or []:
            if of.get("source_type") == "ui_input":
                item: dict[str, Any] = {
                    "name": of["name"],
                    "label": of.get("source_text", of["name"])[:40],
                }
                outputs.append(item)
        if not outputs:
            if node_id in UI_MESSAGE_AS_USER_INPUT or access == "ui_message":
                outputs.append({"name": "acknowledged", "label": "已阅读", "default": True})
            elif access == "user_input":
                if "close" in node_id.lower() and "door" in node_id.lower():
                    outputs.append({"name": "closed", "label": "已关门", "default": True})
                elif "load" in node_id.lower():
                    outputs.append({"name": "loaded", "label": "已放入", "default": True})
                elif "unload" in node_id.lower():
                    outputs.append({"name": "unloaded", "label": "已取出", "default": True})
    return outputs


def routing_from_outcome(
    node: dict[str, Any], *, invert_yes_no: bool = False
) -> dict[str, str]:
    routing: dict[str, str] = {}
    orouting = node.get("outcome_routing") or {}
    access = node.get("access_type", "")
    engine_type = node.get("_engine_type", "")

    for key, val in orouting.items():
        if isinstance(val, dict):
            nxt = val.get("next")
        else:
            nxt = val
        if not nxt:
            continue
        ck = canon_routing_key(key)
        if invert_yes_no and ck in ("yes", "no"):
            ck = "no" if ck == "yes" else "yes"
        out_key = ck
        if access == "application_db" and engine_type == "query" and ck in ("yes", "no"):
            out_key = "single" if ck == "yes" else "empty"
        routing[out_key] = nxt
    return routing


def infer_slot_ref(node: dict[str, Any]) -> str | None:
    for im in node.get("input_mappings") or []:
        if im.get("name") == "slot_id" and im.get("source_node") and im.get("source_field"):
            return f"{im['source_node']}.{im['source_field']}"
    ims = node.get("input_mappings") or []
    for im in ims:
        if im.get("source_node") and im.get("source_field"):
            return f"{im['source_node']}.{expand_field_names(im['source_field'])[0]}"
    return None


def compile_node(node: dict[str, Any], sql_ops: dict[str, str]) -> dict[str, Any]:
    access = node.get("access_type", "")
    node_id = node.get("node_id", "")
    engine_type = infer_engine_type(node, sql_ops)
    node = {**node, "_engine_type": engine_type}

    out: dict[str, Any] = {
        "text": node.get("node_text") or node_id,
        "type": engine_type,
    }

    sql_id = primary_sql_id(node)
    if sql_id and engine_type in ("query", "write", "slot_open", "slot_open_batch"):
        if engine_type.startswith("slot"):
            out["data_source"] = "app"
            if engine_type == "slot_open":
                out["sql_id"] = sql_id or "app.slot.open"
            else:
                out["sql_id"] = sql_id
        elif access.startswith("mes"):
            out["data_source"] = "mes"
            out["sql_id"] = sql_id
        else:
            out["data_source"] = "app"
            out["sql_id"] = sql_id

    invert_routing = False
    if engine_type == "decision":
        chk, invert_routing = build_check(node)
        if chk:
            out["check"] = chk
    else:
        inputs = build_inputs(node)
        if inputs:
            out["inputs"] = inputs

    outputs = build_outputs(node)
    if outputs:
        out["outputs"] = outputs

    if engine_type in ("slot_open", "slot_close", "slot_open_batch"):
        slot_ref = infer_slot_ref(node)
        if slot_ref:
            out["slot_ref"] = slot_ref

    routing = routing_from_outcome(node, invert_yes_no=invert_routing)
    if routing:
        out["routing"] = routing

    if node.get("batch"):
        out["batch"] = node["batch"]

    return out


def collect_referenced_targets(nodes: dict[str, dict[str, Any]]) -> set[str]:
    refs: set[str] = set()
    for node in nodes.values():
        for val in (node.get("outcome_routing") or {}).values():
            if isinstance(val, dict) and val.get("next"):
                refs.add(val["next"])
            elif isinstance(val, str):
                refs.add(val)
    return refs


TERMINAL_TEXT: dict[str, str] = {
    "showOPIdMultipleRecordsMessage": "提示操作员工号查询到多条记录（数据异常）",
    "showEqpNoMultipleRecordsMessage": "提示机台号查询到多条记录（数据异常）",
    "showWireQuotaCheckAbnormalMessage": "提示查询实际与理论消耗差值异常（转人工）",
    "showOpenReturnSlotFailedMessage": "提示无法打开归还格口",
    "showWireByLotNoMultipleRecordsMessage": "提示焊丝批号查询到多条记录（数据异常）",
    "showWireInfoNotExists": "提示可用焊丝信息不存在",
    "showQueryWireByLotNoErrorMessage": "提示查询可用焊丝信息失败（MES 查询异常）",
    "showQueryProductByLotNoErrorMessage": "提示查询产品批次信息失败（MES 查询异常）",
    "showCheckProductInfoNotExistsMessage": "提示产品信息不存在（数量或工序为空）",
    "showSubmitWireIssueErrorMessage": "提示领用提交调用失败",
    "showCheckSubmitWireIssueFailMessage": "提示领用提交未通过",
    "showOpenIssueSlotErrorMessage": "提示无法打开领用格口",
    "showUpdateIssueSlotAfterUnloadErrorMessage": "提示领用格口库存更新失败",
    "showNoAvailableSlotMessage": "提示无空闲格口",
    "showWireLotNoInputInvalidOrNotExistsMessage": "提示焊丝批号无效或不存在",
    "showWireSpecNotExistsMessage": "提示焊丝规格不存在",
    "showWireLotNoAlreadyInCabinetMessage": "提示焊丝批号已在柜内",
    "endSuccess": "存入完成",
    "endFail": "存入失败",
}


def stub_terminal(node_id: str) -> dict[str, Any]:
    if node_id in TERMINAL_SUCCESS_IDS:
        return {"text": "流程成功结束", "type": "terminal", "terminal_kind": "success"}
    if node_id.endswith("Success") or node_id == "endSuccess":
        text = TERMINAL_TEXT.get(node_id, node_id)
        return {"text": text, "type": "terminal", "terminal_kind": "success"}
    text = TERMINAL_TEXT.get(node_id, f"提示: {node_id}")
    return {"text": text, "type": "terminal", "terminal_kind": "fail"}


def compile_formal_flow(flow_id: str, sql_ops: dict[str, str]) -> dict[str, Any]:
    index = load_formal_index(flow_id)
    flow_dir = formal_flow_dir(flow_id)
    node_files: dict[str, str] = index.get("node_files") or {}
    node_order: list[str] = index.get("node_order") or []

    raw_nodes: dict[str, dict[str, Any]] = {}
    for node_id, rel in node_files.items():
        doc = load_formal_node(flow_dir, rel)
        doc.setdefault("node_id", node_id)
        raw_nodes[node_id] = doc

    refs = collect_referenced_targets(raw_nodes)
    for ref in sorted(refs):
        if ref not in raw_nodes and ref not in ("End",):
            raw_nodes[ref] = {
                "node_id": ref,
                "node_text": ref,
                "access_type": "terminal",
                "sql_ids": [],
            }

    compiled_nodes: dict[str, dict[str, Any]] = {}
    for node_id in sorted(raw_nodes.keys()):
        if node_id not in node_files:
            compiled_nodes[node_id] = stub_terminal(node_id)
            continue
        compiled_nodes[node_id] = compile_node(raw_nodes[node_id], sql_ops)

    flow: dict[str, Any] = {
        "flow_id": flow_id,
        "title": index.get("title", flow_id),
        "diagram": diagram_path(index),
        "start": node_order[0] if node_order else "",
        "node_order": list(node_order),
        "nodes": compiled_nodes,
    }
    ctx = FLOW_CONTEXT_DEFAULTS.get(flow_id)
    if ctx:
        flow["context_defaults"] = copy.deepcopy(ctx)
    return flow


def normalize_sql_id(sql_id: str, normalize_mock: bool) -> str:
    if normalize_mock and sql_id.startswith("mes_mock."):
        return "mes." + sql_id[len("mes_mock.") :]
    return sql_id


def normalize_routing_keys(node: dict[str, Any]) -> None:
    routing = node.get("routing")
    if not isinstance(routing, dict):
        return
    normalized: dict[str, Any] = {}
    for k, v in routing.items():
        normalized[canon_routing_key(k)] = v
    node["routing"] = normalized


def normalize_flow_for_compare(
    flow: dict[str, Any], *, normalize_mock: bool, skip_nodes: set[str]
) -> dict[str, Any]:
    data = copy.deepcopy(flow)
    if skip_nodes:
        data["node_order"] = [n for n in data.get("node_order", []) if n not in skip_nodes]
        nodes = data.get("nodes", {})
        for sid in skip_nodes:
            nodes.pop(sid, None)
    for node in data.get("nodes", {}).values():
        if not isinstance(node, dict):
            continue
        normalize_routing_keys(node)
        if node.get("sql_id"):
            node["sql_id"] = normalize_sql_id(node["sql_id"], normalize_mock)
    return data


def deep_diff(a: Any, b: Any, path: str = "") -> list[str]:
    lines: list[str] = []
    if type(a) != type(b):
        lines.append(f"{path or '$'}: 类型不同 {type(a).__name__} vs {type(b).__name__}")
        return lines
    if isinstance(a, dict):
        keys_a = set(a.keys())
        keys_b = set(b.keys())
        for k in sorted(keys_a - keys_b):
            lines.append(f"{path}.{k}: 仅生成版有")
        for k in sorted(keys_b - keys_a):
            lines.append(f"{path}.{k}: 仅目标版有")
        for k in sorted(keys_a & keys_b):
            lines.extend(deep_diff(a[k], b[k], f"{path}.{k}" if path else k))
    elif isinstance(a, list):
        if a != b:
            lines.append(f"{path}: 列表不同\n    生成: {json.dumps(a, ensure_ascii=False)}\n    目标: {json.dumps(b, ensure_ascii=False)}")
    elif a != b:
        lines.append(f"{path}: {json.dumps(a, ensure_ascii=False)} → {json.dumps(b, ensure_ascii=False)}")
    return lines


def compare_flows(
    generated: dict[str, Any],
    target: dict[str, Any],
    *,
    normalize_mock: bool,
    skip_nodes: set[str],
) -> dict[str, Any]:
    g = normalize_flow_for_compare(generated, normalize_mock=normalize_mock, skip_nodes=skip_nodes)
    t = normalize_flow_for_compare(target, normalize_mock=normalize_mock, skip_nodes=skip_nodes)

    g_nodes = set(g.get("nodes", {}))
    t_nodes = set(t.get("nodes", {}))

    report: dict[str, Any] = {
        "flow_id": generated.get("flow_id"),
        "identical": False,
        "only_in_generated": sorted(g_nodes - t_nodes),
        "only_in_target": sorted(t_nodes - g_nodes),
        "node_order_diff": g.get("node_order") != t.get("node_order"),
        "top_level_diffs": [],
        "node_diffs": {},
    }

    for key in ("flow_id", "title", "diagram", "start", "context_defaults"):
        if g.get(key) != t.get(key):
            report["top_level_diffs"].append(
                {"field": key, "generated": g.get(key), "target": t.get(key)}
            )

    if g.get("node_order") != t.get("node_order"):
        report["node_order"] = {"generated": g.get("node_order"), "target": t.get("node_order")}

    for nid in sorted(g_nodes & t_nodes):
        diffs = deep_diff(g["nodes"][nid], t["nodes"][nid], nid)
        if diffs:
            report["node_diffs"][nid] = diffs

    report["identical"] = (
        not report["only_in_generated"]
        and not report["only_in_target"]
        and not report["top_level_diffs"]
        and not report["node_order_diff"]
        and not report["node_diffs"]
    )
    return report


def print_report(report: dict[str, Any]) -> int:
    fid = report["flow_id"]
    print(f"\n{'=' * 60}")
    print(f"流程: {fid}")
    print(f"{'=' * 60}")
    if report["identical"]:
        print("结果: 一致（在所用规范化规则下）")
        return 0

    print("结果: 存在差异\n")
    if report["only_in_generated"]:
        print("仅生成版有的节点:")
        for n in report["only_in_generated"]:
            print(f"  + {n}")
    if report["only_in_target"]:
        print("仅目标版有的节点:")
        for n in report["only_in_target"]:
            print(f"  - {n}")
    if report.get("top_level_diffs"):
        print("\n顶层字段:")
        for d in report["top_level_diffs"]:
            print(f"  {d['field']}:")
            print(f"    生成: {json.dumps(d['generated'], ensure_ascii=False)}")
            print(f"    目标: {json.dumps(d['target'], ensure_ascii=False)}")
    if report.get("node_order"):
        print("\nnode_order 不同（详见 --out 生成文件）")
    if report["node_diffs"]:
        print("\n节点级差异:")
        for nid, diffs in report["node_diffs"].items():
            print(f"  [{nid}]")
            for line in diffs[:12]:
                print(f"    {line}")
            if len(diffs) > 12:
                print(f"    ... 另有 {len(diffs) - 12} 处")
    return 1


def cmd_compare(args: argparse.Namespace) -> int:
    overrides = load_overrides()
    per_flow = (overrides.get("flows") or {}).get(args.flow_id, {})
    skip_nodes = set(args.skip_node or []) | set(per_flow.get("skip_nodes") or [])
    normalize_mock = args.normalize_mock_sql or overrides.get("normalize_mock_sql", False)
    sql_ops = load_sql_operations()

    target_path = args.target or default_target_path(args.flow_id, args.target_dir)
    if args.out is None:
        args.out = target_path.parent / "flow.generated.yaml"

    generated = compile_formal_flow(args.flow_id, sql_ops)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(dump_yaml(generated), encoding="utf-8")
        print(f"已写入生成版: {args.out}")

    if not target_path.is_file():
        print(f"目标不存在: {target_path}")
        print("仅生成模式；若要对比请先提供目标 flow.yaml")
        return 0

    target = load_yaml(target_path)
    report = compare_flows(
        generated, target, normalize_mock=normalize_mock, skip_nodes=skip_nodes
    )
    if args.report_json:
        args.report_json.write_text(
            json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
        )
        print(f"差异报告 JSON: {args.report_json}")
    return print_report(report)


def cmd_generate(args: argparse.Namespace) -> int:
    if args.out is None:
        print("generate 子命令需要 --out", file=sys.stderr)
        return 2
    sql_ops = load_sql_operations()
    generated = compile_formal_flow(args.flow_id, sql_ops)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(dump_yaml(generated), encoding="utf-8")
    print(f"已生成: {args.out}")
    return 0


def cmd_apply(args: argparse.Namespace) -> int:
    exit_code = cmd_compare(args)
    target_path = args.target or default_target_path(args.flow_id, args.target_dir)
    generated_path = args.out
    assert generated_path is not None

    if not generated_path.is_file():
        sql_ops = load_sql_operations()
        generated_path.write_text(
            dump_yaml(compile_formal_flow(args.flow_id, sql_ops)), encoding="utf-8"
        )

    if exit_code != 0 and not args.force:
        print("\n未覆盖目标（存在差异）。确认后请加 --force 执行覆盖。")
        print(f"生成版已保存: {generated_path}")
        return 1

    if args.backup and target_path.is_file():
        ts = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        backup = target_path.with_suffix(f".yaml.bak.{ts}")
        shutil.copy2(target_path, backup)
        print(f"已备份: {backup}")

    shutil.copy2(generated_path, target_path)
    print(f"已覆盖目标: {target_path}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="正式 flow-sql-map → flow.yaml 编译与对比")
    sub = p.add_subparsers(dest="command", required=True)

    def add_common(sp: argparse.ArgumentParser) -> None:
        sp.add_argument("flow_id", help="flow_id，如 material_handler_load_available_wire")
        sp.add_argument(
            "--target-dir",
            type=Path,
            help="目标 flow.yaml 所在目录（默认 wire-flow-lab/.../flows/<flow_id>/）",
        )
        sp.add_argument("--target", type=Path, help="目标 flow.yaml 完整路径")
        sp.add_argument(
            "--out",
            type=Path,
            help="写入生成版 flow.yaml 的路径（默认 compare 时写 <target_dir>/flow.generated.yaml）",
        )
        sp.add_argument(
            "--normalize-mock-sql",
            action="store_true",
            help="对比时将 mes_mock.* 与 mes.* 视为等价",
        )
        sp.add_argument(
            "--skip-node",
            action="append",
            default=[],
            help="对比时忽略的节点 ID（可重复）",
        )
        sp.add_argument("--report-json", type=Path, help="写出结构化差异报告 JSON")

    sp_cmp = sub.add_parser("compare", help="从正式规格编译并与目标 flow.yaml 对比")
    add_common(sp_cmp)
    sp_cmp.set_defaults(func=cmd_compare)

    sp_gen = sub.add_parser("generate", help="仅生成 flow.yaml，不对比")
    add_common(sp_gen)
    sp_gen.set_defaults(func=cmd_generate)

    sp_apply = sub.add_parser("apply", help="对比后覆盖目标（默认有差异则中止，--force 强制）")
    add_common(sp_apply)
    sp_apply.add_argument("--force", action="store_true", help="忽略差异，强制覆盖目标")
    sp_apply.add_argument("--backup", action="store_true", help="覆盖前备份目标文件")
    sp_apply.set_defaults(func=cmd_apply)

    sp_all = sub.add_parser("compare-all", help="对比 index 中全部 flow")
    sp_all.add_argument("--normalize-mock-sql", action="store_true")
    sp_all.add_argument("--skip-node", action="append", default=[])
    sp_all.set_defaults(func=None)
    return p


def cmd_compare_all(args: argparse.Namespace) -> int:
    code = 0
    for fid in repo_flow_ids():
        ns = argparse.Namespace(
            flow_id=fid,
            target_dir=None,
            target=None,
            out=LAB_MAP_ROOT / "flows" / fid / "flow.generated.yaml",
            normalize_mock_sql=args.normalize_mock_sql,
            skip_node=args.skip_node,
            report_json=None,
        )
        if cmd_compare(ns) != 0:
            code = 1
    return code


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    if args.command == "compare-all":
        return cmd_compare_all(args)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
