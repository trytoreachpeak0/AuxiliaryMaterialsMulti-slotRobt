"""Move node YAML files into nodes/<subgraph_id>/ and update subgraph_id in each file."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "flows"

# flow_id -> { subgraph_id: [node_id, ...] }
FLOW_SUBGRAPHS: dict[str, dict[str, list[str]]] = {
    "operator_return_wire_and_issue_available_wire": {
        "inputOperatorInfo": [
            "inputOperatorWorkInfo",
            "queryOPById",
            "checkOPIdExists",
        ],
        "inputReturnedWireLotNoSection": [
            "inputReturnedWireLotNo",
            "queryWireSpecByLotNo",
            "checkWireLotNoExists",
            "checkWireSpecExists",
            "queryMatchedAvailableWire",
            "checkMatchedWireExists",
            "findReturnSlot",
        ],
        "queryReturnedWeightSection": [
            "queryWireReturnedWeightBySpec",
            "checkReturnedWeightExists",
        ],
        "inputEqpNoSection": [
            "inputEqpNo",
            "queryEqpByEqpNo",
            "checkEqpNoExists",
        ],
        "queryLastProductLotNoSection": [
            "queryLastProductLotNo",
            "checkLastProductLotNoExists",
        ],
        "inputRemainingQtySection": [
            "inputRemainingQty",
            "queryWireQuotaCheck",
            "checkWireQuotaLE500",
            "showRemainingQtyWrongHint",
        ],
        "submitWireReturnSection": [
            "submitWireReturn",
            "checkSubmitWireReturnSuccess",
        ],
        "loadReturnedWireSection": [
            "showWireReturnSuccessMessage",
            "openReturnSlot",
            "showLoadWireAndCloseDoorMessage",
            "loadReturnedWire",
            "closeReturnSlotDoor",
            "updateReturnSlotDB",
        ],
        "queryWireAndProductSection": [
            "queryWireByLotNo",
            "checkWireInfoExists",
            "queryProductByLotNo",
            "checkProductInfoExists",
        ],
        "submitWireIssueSection": [
            "submitWireIssue",
            "checkSubmitWireIssueSuccess",
        ],
        "unloadIssueWireSection": [
            "showWireIssueSuccessMessage",
            "openIssueSlot",
            "showUnloadWireAndCloseDoorMessage",
            "unloadWire",
            "closeIssueSlotDoor",
            "updateIssueSlotAfterUnload",
        ],
    },
    "material_handler_load_available_wire": {
        "inputWireLotNo": [
            "inputAvailableWireLotNo",
            "checkAvailableSlot",
            "queryWireSpecByLotNo",
            "checkWireLotNoExists",
            "checkWireSpecExists",
        ],
        "openSlotLoadWire": [
            "openAvailableSlot",
            "closeAvailableSlotDoor",
            "updateDB",
        ],
    },
    "material_handler_open_single_slot": {
        "openSlot": [
            "inputSelectedSlot",
            "openSingleSlot",
            "updateCorrespondingSlot",
        ],
        "closeSlotDoor": [
            "isAllSlotDoorsClosed",
            "closeSingleSlotDoor",
            "updateSlot",
        ],
    },
    "material_handler_open_all_slots": {
        "openSlots": [
            "openAllSlots",
            "updateAllSlots",
        ],
        "closeSlotDoors": [
            "isAllSlotDoorsClosed",
            "closeSingleSlotDoor",
            "updateSlot",
        ],
    },
    "material_handler_open_all_available_wire_slots": {
        "openAvailableWireSlots": [
            "openAllAvailableWireSlots",
            "updateCorrespondingSlots",
        ],
        "closeSlotDoors": [
            "isAllSlotDoorsClosed",
            "closeSingleSlotDoor",
            "updateSlot",
        ],
    },
    "material_handler_open_all_returned_wire_slots": {
        "openReturnedWireSlots": [
            "openAllReturnedWireSlots",
            "updateCorrespondingSlots",
        ],
        "closeSlotDoors": [
            "isAllSlotDoorsClosed",
            "closeSingleSlotDoor",
            "updateSlot",
        ],
    },
}


def find_node_file(nodes_dir: Path, node_id: str) -> Path | None:
    direct = nodes_dir / f"{node_id}.yaml"
    if direct.is_file():
        return direct
    for p in nodes_dir.rglob(f"{node_id}.yaml"):
        if p.parent != nodes_dir or p.parent.name != "nodes":
            return p
    matches = list(nodes_dir.rglob(f"{node_id}.yaml"))
    return matches[0] if len(matches) == 1 else None


def set_subgraph_id(text: str, subgraph_id: str) -> str:
    if re.search(r"^subgraph_id:\s*\S+", text, re.M):
        return re.sub(r"^subgraph_id:\s*\S+", f"subgraph_id: {subgraph_id}", text, count=1, flags=re.M)
    if text.startswith("node_id:"):
        return f"subgraph_id: {subgraph_id}\n{text}"
    return f"subgraph_id: {subgraph_id}\n{text}"


def migrate_flow(flow_id: str, subgraphs: dict[str, list[str]]) -> dict[str, str]:
    flow_dir = ROOT / flow_id
    nodes_dir = flow_dir / "nodes"
    node_files: dict[str, str] = {}

    for subgraph_id, node_ids in subgraphs.items():
        dest_dir = nodes_dir / subgraph_id
        dest_dir.mkdir(parents=True, exist_ok=True)
        for node_id in node_ids:
            src = find_node_file(nodes_dir, node_id)
            if src is None:
                raise FileNotFoundError(f"{flow_id}: missing node {node_id}")
            dest = dest_dir / f"{node_id}.yaml"
            if src.resolve() != dest.resolve():
                if dest.exists():
                    dest.unlink()
                src.rename(dest)
            text = dest.read_text(encoding="utf-8")
            dest.write_text(set_subgraph_id(text, subgraph_id), encoding="utf-8", newline="\n")
            node_files[node_id] = f"nodes/{subgraph_id}/{node_id}.yaml"
            print(f"  {node_id} -> nodes/{subgraph_id}/")

    # remove empty legacy dirs
    for d in sorted(nodes_dir.iterdir()):
        if d.is_dir() and d.name in ("returnWire", "issueWire") and not any(d.iterdir()):
            d.rmdir()
            print(f"  removed empty {d.name}/")

    return node_files


def main() -> None:
    for flow_id, subgraphs in FLOW_SUBGRAPHS.items():
        print(f"migrate {flow_id}")
        migrate_flow(flow_id, subgraphs)
    print("done")


if __name__ == "__main__":
    main()
