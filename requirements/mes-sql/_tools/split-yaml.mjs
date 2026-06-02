import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import YAML from "yaml";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const base = path.resolve(__dirname, "..");

function writeYaml(filePath, data) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, YAML.stringify(data, { lineWidth: 120 }), "utf8");
}

const catalog = YAML.parse(fs.readFileSync(path.join(base, "mes-sql-catalog.yaml"), "utf8"));
const schema = YAML.parse(fs.readFileSync(path.join(base, "mes-schema.yaml"), "utf8"));
const flowmap = YAML.parse(fs.readFileSync(path.join(base, "flow-sql-map.yaml"), "utf8"));

// mes-sql-catalog
const catIndex = {
  version: catalog.version,
  owner: catalog.owner,
  description: catalog.description,
  sql_items_index: [],
};
for (const item of catalog.sql_items) {
  const rel = `items/${item.id}.yaml`;
  writeYaml(path.join(base, "mes-sql-catalog", rel), item);
  catIndex.sql_items_index.push({ id: item.id, path: rel });
}
writeYaml(path.join(base, "mes-sql-catalog", "index.yaml"), catIndex);

// mes-schema
const schIndex = {
  version: schema.version,
  description: schema.description,
  datasources_path: "datasources.yaml",
  tables_index: [],
  enums_index: [],
};
writeYaml(path.join(base, "mes-schema", "datasources.yaml"), {
  datasources: schema.datasources ?? [],
});
for (const t of schema.tables ?? []) {
  const rel = `tables/${t.name}.yaml`;
  writeYaml(path.join(base, "mes-schema", rel), t);
  schIndex.tables_index.push({ name: t.name, path: rel });
}
for (const [i, e] of (schema.enums ?? []).entries()) {
  const field = e.field ?? `enum_${i}`;
  const rel = `enums/${field}.yaml`;
  writeYaml(path.join(base, "mes-schema", rel), e);
  schIndex.enums_index.push({ field, path: rel });
}
writeYaml(path.join(base, "mes-schema", "index.yaml"), schIndex);

// flow-sql-map — align sql_ids with catalog while splitting
const idFix = {
  "mes.wire_lot.get_type_by_lot_no": "mes.wire_lot.get_by_lot_no",
  "mes.machine.get_by_machine_no": "mes.eqp.get_by_wire_bonding_eqp_no",
  "mes.work_order.get_latest_lot_by_machine": "mes.product.get_latest_product_lot_by_eqp",
};
function fixFlow(flow) {
  for (const node of flow.nodes ?? []) {
    if (node.sql_ids) {
      node.sql_ids = node.sql_ids.map((id) => idFix[id] ?? id);
    }
  }
  return flow;
}

const flowIndex = { version: flowmap.version, flows_index: [] };
for (const flow of flowmap.flows) {
  const fixed = fixFlow(structuredClone(flow));
  const rel = `flows/${fixed.flow_id}.yaml`;
  writeYaml(path.join(base, "flow-sql-map", rel), fixed);
  flowIndex.flows_index.push({ flow_id: fixed.flow_id, path: rel });
}
writeYaml(path.join(base, "flow-sql-map", "index.yaml"), flowIndex);

console.log(
  `Split: ${catalog.sql_items.length} sql, ${schema.tables?.length ?? 0} tables, ${flowmap.flows.length} flows`
);
