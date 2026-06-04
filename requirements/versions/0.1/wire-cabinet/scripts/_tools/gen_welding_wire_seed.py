"""Generate SQLite seed from repo-root WeldingWireMaterials.sql."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
SRC = ROOT / "WeldingWireMaterials.sql"
OUT = Path(__file__).resolve().parents[1] / "seed-welding_wire_materials.sqlite.sql"

text = SRC.read_text(encoding="utf-8")
pat = re.compile(
    r"VALUES \(N'(\d+)', N'([^']*)', N'((?:[^']|'')*)', N'([^']*)'",
    re.I,
)
rows = pat.findall(text)
lines = [
    "-- Auto-generated from WeldingWireMaterials.sql (do not hand-edit; re-run gen_welding_wire_seed.py)",
    "DELETE FROM welding_wire_materials;",
    "INSERT INTO welding_wire_materials (id, material_type, specification_model, empty_spool_weight, created_at, updated_at) VALUES",
]
for id_, mt, spec, w in rows:
    spec_esc = spec.replace("'", "''")
    lines.append(
        f" ({id_}, '{mt}', '{spec_esc}', {w}, '2025-07-27 16:58:51', '2025-07-27 16:58:51'),"
    )
lines[-1] = lines[-1].rstrip(",") + ";"
OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"{len(rows)} rows -> {OUT}")
