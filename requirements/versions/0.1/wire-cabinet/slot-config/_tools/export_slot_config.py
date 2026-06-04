#!/usr/bin/env python3
"""从 仓位信息/*.xlsx 生成 slot-config/*.generated.yaml 并校验 54 路映射。"""
from __future__ import annotations

import re
import sys
from datetime import datetime, timezone
from pathlib import Path

try:
    import openpyxl
except ImportError:
    import subprocess

    subprocess.check_call([sys.executable, "-m", "pip", "install", "openpyxl", "-q"])
    import openpyxl

try:
    import yaml
except ImportError:
    import subprocess

    subprocess.check_call([sys.executable, "-m", "pip", "install", "pyyaml", "-q"])
    import yaml

REPO_ROOT = Path(__file__).resolve().parents[6]
SLOT_INFO = REPO_ROOT / "仓位信息"
OUT_DIR = Path(__file__).resolve().parents[1]
MODULE_KEY_MAP_FILE = OUT_DIR / "module-key-map.yaml"
POSITION_RE = re.compile(r"^(front|rear)\(\d+,\d+\)$", re.I)
DO_RE = re.compile(r"^DO(\d+)$", re.I)
DI_RE = re.compile(r"^DI(\d+)$", re.I)


def load_module_key_map() -> dict[str, str]:
    """xlsx sheet 名（MAC 末两位）→ 完整 MAC module_key。"""
    if not MODULE_KEY_MAP_FILE.exists():
        return {}
    data = yaml.safe_load(MODULE_KEY_MAP_FILE.read_text(encoding="utf-8")) or {}
    raw = data.get("keys") or {}
    return {str(k).strip(): str(v).strip() for k, v in raw.items()}


def resolve_module_key(sheet_name: str, key_map: dict[str, str]) -> str:
    name = str(sheet_name).strip()
    if ":" in name:
        return name
    return key_map.get(name, name)


def parse_do_di(name: str | None) -> int | None:
    if not name:
        return None
    s = str(name).strip()
    m = DO_RE.match(s) or DI_RE.match(s)
    return int(m.group(1)) if m else None


def load_slots() -> list[dict]:
    path = SLOT_INFO / "仓位编号.xlsx"
    wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
    ws = wb[wb.sheetnames[0]]
    slots: list[dict] = []
    for row in ws.iter_rows(values_only=True):
        pos = row[0]
        if not pos or not POSITION_RE.match(str(pos).strip()):
            continue
        slot_code = str(row[1]).strip() if row[1] else None
        if not slot_code:
            continue
        slots.append(
            {
                "slot_index": len(slots) + 1,
                "door_position": str(pos).strip(),
                "slot_code": slot_code,
                "display_name_short": str(row[2]).strip() if row[2] else "",
                "display_name": str(row[3]).strip() if row[3] else "",
                "side": "front" if str(pos).lower().startswith("front") else "rear",
            }
        )
    wb.close()
    return slots


def load_io_mappings(key_map: dict[str, str]) -> list[dict]:
    path = SLOT_INFO / "IO仓位对应表.xlsx"
    wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
    mappings: list[dict] = []
    for sheet_name in wb.sheetnames:
        ws = wb[sheet_name]
        for i, row in enumerate(ws.iter_rows(values_only=True)):
            if i == 0:
                continue
            do_name, pos1, di_name, pos2 = (row + (None,) * 4)[:4]
            do_idx = parse_do_di(do_name)
            di_idx = parse_do_di(di_name)
            if do_idx is None:
                continue
            pos = str(pos1).strip() if pos1 and POSITION_RE.match(str(pos1).strip()) else None
            pos2s = str(pos2).strip() if pos2 else ""
            if pos and pos2s and pos != pos2s:
                raise ValueError(f"模块 {sheet_name} {do_name}: DO/DI 位置不一致 {pos} vs {pos2s}")
            mappings.append(
                {
                    "module_key": resolve_module_key(sheet_name, key_map),
                    "do_point": str(do_name).strip(),
                    "do_index": do_idx,
                    "di_point": str(di_name).strip() if di_name else None,
                    "di_index": di_idx,
                    "door_position": pos,
                    "wired": pos is not None,
                }
            )
    wb.close()
    return mappings


def build_slot_io(slots: list[dict], io_rows: list[dict]) -> tuple[list[dict], list[str]]:
    pos_to_code = {s["door_position"]: s["slot_code"] for s in slots}
    code_to_slot = {s["slot_code"]: s for s in slots}
    by_pos: dict[str, list[dict]] = {}
    errors: list[str] = []

    for m in io_rows:
        if not m["wired"]:
            continue
        pos = m["door_position"]
        by_pos.setdefault(pos, []).append(m)
        if pos not in pos_to_code:
            errors.append(f"IO 有位置无编号表: {pos} ({m['module_key']}/{m['do_point']})")

    for pos, items in by_pos.items():
        if len(items) > 1:
            errors.append(f"同位置多模块绑定: {pos} -> {[x['module_key'] for x in items]}")

    wired_mappings: list[dict] = []
    for s in slots:
        pos = s["door_position"]
        code = s["slot_code"]
        items = by_pos.get(pos, [])
        if not items:
            wired_mappings.append(
                {
                    "slot_code": code,
                    "door_position": pos,
                    "slot_index": s["slot_index"],
                    "module_key": None,
                    "do_point": None,
                    "do_index": None,
                    "di_point": None,
                    "di_index": None,
                    "wired": False,
                    "enabled_default": False,
                }
            )
            continue
        io = items[0]
        wired_mappings.append(
            {
                "slot_code": code,
                "door_position": pos,
                "slot_index": s["slot_index"],
                "module_key": io["module_key"],
                "do_point": io["do_point"],
                "do_index": io["do_index"],
                "di_point": io["di_point"],
                "di_index": io["di_index"],
                "wired": True,
                "enabled_default": True,
            }
        )

    wired_count = sum(1 for x in wired_mappings if x["wired"])
    slot_codes = {s["slot_code"] for s in slots}
    for m in io_rows:
        if m["wired"] and m["door_position"] in pos_to_code:
            sc = pos_to_code[m["door_position"]]
            if sc not in slot_codes:
                errors.append(f"编号表缺失: {sc}")

    if len(slots) != 54:
        errors.append(f"期望 54 格，编号表有效行={len(slots)}")
    if wired_count != 54:
        errors.append(f"期望 54 路接线，实际 wired={wired_count}")

    return wired_mappings, errors


def yaml_dump(data: dict) -> str:
    return yaml.dump(
        data,
        allow_unicode=True,
        default_flow_style=False,
        sort_keys=False,
        width=120,
    )


def main() -> int:
    generated_at = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    key_map = load_module_key_map()
    if not key_map:
        print("WARN: module-key-map.yaml 缺失或为空，module_key 将沿用 xlsx sheet 名", file=sys.stderr)
    slots = load_slots()
    io_rows = load_io_mappings(key_map)
    slot_io, errors = build_slot_io(slots, io_rows)

    slots_doc = {
        "schema_version": "0.1",
        "generated_at": generated_at,
        "source": "仓位信息/仓位编号.xlsx",
        "slot_count": len(slots),
        "slots": slots,
    }
    mapping_doc = {
        "schema_version": "0.1",
        "generated_at": generated_at,
        "source": "仓位信息/IO仓位对应表.xlsx + 仓位编号.xlsx",
        "wired_count": sum(1 for x in slot_io if x["wired"]),
        "mappings": slot_io,
    }
    unwired_io = [m for m in io_rows if not m["wired"]]

    (OUT_DIR / "slots.generated.yaml").write_text(yaml_dump(slots_doc), encoding="utf-8")
    (OUT_DIR / "slot-io-mapping.generated.yaml").write_text(yaml_dump(mapping_doc), encoding="utf-8")

    validation_lines = [
        "# slot-config 校验摘要",
        "",
        f"- 生成时间（UTC）: {generated_at}",
        f"- 格口数: {len(slots)}",
        f"- 已接线映射: {mapping_doc['wired_count']}",
        f"- 未接线 IO 行（模块占位）: {len(unwired_io)}",
        "",
    ]
    if errors:
        validation_lines.append("## 错误")
        for e in errors:
            validation_lines.append(f"- {e}")
    else:
        validation_lines.append("## 结果")
        validation_lines.append("- 校验通过：54 格编号唯一，54 路 DO/DI 一一对应，无跨模块重复绑定。")

    (OUT_DIR / "VALIDATION.md").write_text("\n".join(validation_lines) + "\n", encoding="utf-8")

    print(f"Wrote {OUT_DIR / 'slots.generated.yaml'} ({len(slots)} slots)")
    print(f"Wrote {OUT_DIR / 'slot-io-mapping.generated.yaml'} (wired={mapping_doc['wired_count']})")
    if errors:
        for e in errors:
            print(f"ERROR: {e}", file=sys.stderr)
        return 1
    print("Validation OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
