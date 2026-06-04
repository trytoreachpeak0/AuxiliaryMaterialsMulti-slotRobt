"""One-off: add/normalize output_fields on flow-sql-map node YAML files."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "flows"

MES_WIRE = [
    ("spec", "焊丝规格"),
    ("code", "焊丝物料编码"),
    ("matlot", "焊丝主批次"),
    ("qty", "焊丝数量"),
    ("shelflife", "保质期"),
    ("state", "焊丝状态"),
]

def sql_fields(sql_id: str, fields: list[tuple[str, str]], pending: bool = False) -> str:
    lines = ["output_fields:"]
    for name, text in fields:
        lines.append(f"  - name: {name}")
        lines.append("    source_type: sql_catalog")
        lines.append(f"    source_ref: {sql_id}")
        lines.append(f"    catalog_field: {name}")
        if pending:
            lines.append("    catalog_status: pending_catalog")
        lines.append(f"    source_text: {text}")
    return "\n".join(lines)

def app_fields(sql_id: str, fields: list[tuple[str, str]], extra: str = "") -> str:
    return sql_fields(sql_id, fields, pending=True) + (f"\n# {extra}" if extra else "")

# node_id -> output_fields yaml block (without trailing newline issues)
OUTPUTS: dict[str, str] = {
    "queryOPById": sql_fields("mes.operator.get_by_id", [("operator_name", "MES 操作员姓名。")]),
    "queryWireSpecByLotNo": sql_fields(
        "mes.wire.get_by_lot_no",
        [(n, t) for n, t in zip(
            ["spec", "code", "matlot", "qty", "shelflife", "state"],
            ["焊丝规格。", "物料编码。", "主批次。", "数量。", "保质期。", "状态。"],
        )],
    ),
    "queryMatchedAvailableWire": sql_fields(
        "app.wire_inventory.find_matched_available",
        [
            ("matched_available_wire_lot_no", "匹配规格的可用焊丝批号。"),
            ("slot_id", "可用焊丝所在格口 ID。"),
            ("slot_no", "格口编号。"),
            ("wire_spec", "焊丝规格（与归还规格一致）。"),
        ],
        pending=True,
    ),
    "queryWireReturnedWeightBySpec": sql_fields(
        "app.returned_weight.get_by_spec",
        [("return_weight", "该规格配置的归还重量。")],
        pending=True,
    ),
    "queryEqpByEqpNo": sql_fields(
        "mes.eqp.get_by_wire_bonding_eqp_no",
        [("eqp_no", "命中机台时用于下游 exists 判定（与入参机台号一致）。")],
    ),
    "queryLastProductLotNo": sql_fields(
        "mes.product.get_latest_product_lot_by_eqp",
        [("lot", "机台最近生产产品批号；无数据时 SQL NVL 为 N/A。")],
    ),
    "queryWireQuotaCheck": sql_fields(
        "mes.wire_quota.check_quota",
        [("result", "真实用量与预计用量差值，供配额判定。")],
    ),
    "queryWireByLotNo": sql_fields(
        "mes.wire.get_by_lot_no",
        [
            ("code", "可用焊丝物料编码。"),
            ("matlot", "可用焊丝主批次。"),
            ("qty", "可用焊丝数量。"),
            ("shelflife", "可用焊丝保质期。"),
        ],
    ),
    "queryProductByLotNo": sql_fields(
        "mes.product.query_product_by_lot_no",
        [("qty", "未关闭子批数量。"), ("step", "产品工序。")],
    ),
    "submitWireReturn": sql_fields(
        "mes.mat_trans.submit_return",
        [("result", "MES 归还事务返回结果；空或 SUCCESS 为成功。")],
    ),
    "submitWireIssue": sql_fields(
        "mes.mat_trans.submit_return",
        [("result", "MES 领用事务返回结果；空或 SUCCESS 为成功。")],
    ),
    "checkAvailableSlot": sql_fields(
        "app.slot.find_available",
        [("available_slot_id", "选中的空闲格口 ID。"), ("slot_no", "格口编号。")],
        pending=True,
    ),
    "openAvailableSlot": sql_fields(
        "app.slot.open",
        [("opened_slot_id", "已下发开门指令的格口 ID。")],
        pending=True,
    ),
    "updateDB": sql_fields(
        "app.slot.bind_wire",
        [("rows_affected", "格口绑定焊丝信息的更新行数。")],
        pending=True,
    ),
    "openSingleSlot": sql_fields(
        "app.slot.get_status",
        [
            ("slot_id", "格口 ID。"),
            ("slot_no", "格口编号。"),
            ("slot_door_status", "开门前门状态。"),
            ("lock_state", "锁状态。"),
            ("biz_state", "格口业务状态。"),
        ],
        pending=True,
    )
    + "\n  - name: opened_slot_id\n    source_type: slot_control\n    source_ref: app.slot.open\n    catalog_status: pending_catalog\n    source_text: 门控开门后回写的格口 ID（与 slot_id 一致）。",
    "openAllSlots": sql_fields(
        "app.slot.list_by_filter",
        [
            ("slot_id", "待打开格口 ID（批量时多行）。"),
            ("slot_no", "格口编号。"),
            ("biz_state", "业务状态。"),
            ("door_state", "门状态。"),
        ],
        pending=True,
    )
    + "\n  - name: slot_ids\n    source_type: runtime\n    source_text: 批量开门后的格口 ID 列表，供 updateAllSlots 使用。",
    "openAllAvailableWireSlots": sql_fields(
        "app.slot.list_by_filter",
        [
            ("slot_id", "含可用焊丝的格口 ID。"),
            ("slot_no", "格口编号。"),
            ("biz_state", "业务状态。"),
            ("door_state", "门状态。"),
        ],
        pending=True,
    )
    + "\n  - name: slot_ids\n    source_type: runtime\n    source_text: 批量开门格口 ID 列表。",
    "openAllReturnedWireSlots": sql_fields(
        "app.slot.list_by_filter",
        [
            ("slot_id", "含归还焊丝的格口 ID。"),
            ("slot_no", "格口编号。"),
            ("biz_state", "业务状态。"),
            ("door_state", "门状态。"),
        ],
        pending=True,
    )
    + "\n  - name: slot_ids\n    source_type: runtime\n    source_text: 批量开门格口 ID 列表。",
    "updateAllSlots": sql_fields(
        "app.slot.update_state",
        [("rows_affected", "批量更新格口状态影响行数。")],
        pending=True,
    )
    + "\n  - name: opened_slot_ids\n    source_type: runtime\n    source_text: 本次打开并等待关门的格口 ID 列表。",
    "updateCorrespondingSlots": sql_fields(
        "app.slot.update_state",
        [("rows_affected", "批量更新影响行数。")],
        pending=True,
    )
    + "\n  - name: opened_slot_ids\n    source_type: runtime\n    source_text: 已打开格口 ID 列表。",
    "updateCorrespondingSlot": sql_fields(
        "app.slot.update_state",
        [("rows_affected", "单格口状态更新行数。")],
        pending=True,
    )
    + "\n  - name: opened_slot_id\n    source_type: runtime\n    source_text: 已打开并待关闭的格口 ID。",
    "updateSlot": sql_fields(
        "app.slot.update_state",
        [("rows_affected", "关门后状态更新行数。")],
        pending=True,
    ),
}

# Normalize existing user_input output_fields: ensure structure (already have source_type)
USER_INPUT_ENHANCE = {
    "inputOperatorWorkInfo": None,  # keep existing
    "closeAvailableSlotDoor": [
        ("closed", "物料员关闭格口门确认。"),
    ],
}

def strip_output_fields(text: str) -> str:
    """Remove existing output_fields block."""
    return re.sub(
        r"\noutput_fields:\n(?:  - .*\n(?:    .*\n)*)*",
        "",
        text,
        count=1,
    )

def insert_after_business_meaning(text: str, block: str) -> str:
    text = strip_output_fields(text)
    if "business_meaning:" in text:
        return re.sub(
            r"(business_meaning:.*\n)",
            r"\1" + block + "\n",
            text,
            count=1,
        )
    # fallback: after sql_ids / catalog_status block
    m = re.search(r"(sql_ids:.*\n(?:catalog_status:.*\n)?)", text)
    if m:
        pos = m.end()
        return text[:pos] + block + "\n" + text[pos:]
    return text

def main() -> None:
    updated = 0
    for path in sorted(ROOT.glob("**/nodes/**/*.yaml")):
        text = path.read_text(encoding="utf-8")
        m = re.search(r"^node_id:\s*(\S+)", text, re.M)
        if not m:
            continue
        nid = m.group(1)
        if nid not in OUTPUTS:
            continue
        new_text = insert_after_business_meaning(text, OUTPUTS[nid])
        if new_text != text:
            path.write_text(new_text, encoding="utf-8", newline="\n")
            updated += 1
            print(path.relative_to(ROOT.parent), nid)
    print(f"updated {updated} files")

if __name__ == "__main__":
    main()
