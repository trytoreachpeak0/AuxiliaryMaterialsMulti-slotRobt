#!/usr/bin/env python3
"""从 slot-config/slots.generated.yaml 生成 seed-app_slot.sqlite.sql"""
from pathlib import Path
import yaml

root = Path(__file__).resolve().parents[2]
slots_file = root / "slot-config" / "slots.generated.yaml"
out_file = root / "scripts" / "seed-app_slot.sqlite.sql"

data = yaml.safe_load(slots_file.read_text(encoding="utf-8"))
lines = [
    "-- 54 格 app_slot 种子（由 gen_seed_app_slot.py 生成）",
    "-- 演示：slot 5/7 含同规格可用焊丝（OP 批号查询 checkMatchedWireExists 用）",
    "DELETE FROM app_slot;",
    "INSERT INTO app_slot (slot_id, slot_no, door_position, slot_index, is_enabled, io_wired, usage_type, biz_state, door_state, lock_state, wire_lot_no, wire_spec, wire_code, shelflife, matlot, qty, wire_state, updated_at) VALUES",
]
rows = []
for s in data["slots"]:
    idx = s["slot_index"]
    code = s["slot_code"]
    pos = s.get("door_position", "")
    wire_cols = "NULL, NULL, NULL, NULL, NULL, NULL, NULL"
    # 演示：前 8 格沿用 lab 布局语义，其余 idle available
    if idx <= 4:
        ut, bs = ("available", "idle") if idx <= 2 else (("shared", "idle") if idx == 3 else ("returned", "idle"))
    elif idx == 5:
        ut, bs = "available", "available_wire"
        wire_cols = "'14Z1888-0888', 'φ42(MAXSOFT)', 'WIRE-φ42', '2026-12-31 00:00:00', '14Z1888', 1.0, '可用'"
    elif idx == 6:
        ut, bs = "returned", "returned_wire"
        wire_cols = "'14Z1888-0777', 'φ42(MAXSOFT)', 'WIRE-φ42', '2026-10-31 00:00:00', '14Z1888', 1.0, '归还'"
    elif idx == 7:
        ut, bs = "available", "available_wire"
        wire_cols = "'14Z1999-0001', 'φ36(SOFT)', 'WIRE-φ36', '2026-11-30 00:00:00', '14Z1999', 1.0, '可用'"
    else:
        ut, bs = "available", "idle"
    enabled = 1 if idx <= 8 else 1
    wired = 1
    rows.append(
        f" ({idx}, '{code}', '{pos}', {idx}, {enabled}, {wired}, '{ut}', '{bs}', 'closed', 'locked', {wire_cols}, datetime('now'))"
    )
lines.append(",\n".join(rows) + ";")
out_file.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Wrote {out_file} ({len(rows)} slots)")
