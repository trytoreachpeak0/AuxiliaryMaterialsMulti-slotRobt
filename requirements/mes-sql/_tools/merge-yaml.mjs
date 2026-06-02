import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import YAML from "yaml";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const base = path.resolve(__dirname, "..");

function readYaml(filePath) {
  return YAML.parse(fs.readFileSync(filePath, "utf8"));
}

const HEADERS = {
  "mes-sql-catalog.yaml": `# 合并视图（自动生成，请勿直接编辑）
# 请改 mes-sql-catalog/items/<id>.yaml，再执行：cd _tools && npm run merge
`,
  "mes-schema.yaml": `# 合并视图（自动生成，请勿直接编辑）
# 请改 mes-schema/tables|enums/ 下文件，再执行：cd _tools && npm run merge
`,
  "flow-sql-map.yaml": `# 合并视图（自动生成，请勿直接编辑）
# 请改 flow-sql-map/flows/<flow_id>.yaml，再执行：cd _tools && npm run merge
`,
};

function writeYaml(filePath, data, header = "") {
  fs.writeFileSync(filePath, header + YAML.stringify(data, { lineWidth: 120 }), "utf8");
}

// catalog
const catIndex = readYaml(path.join(base, "mes-sql-catalog", "index.yaml"));
const catalog = {
  version: catIndex.version,
  owner: catIndex.owner,
  description: catIndex.description,
  sql_items: catIndex.sql_items_index.map(({ path: rel }) =>
    readYaml(path.join(base, "mes-sql-catalog", rel))
  ),
};
writeYaml(path.join(base, "mes-sql-catalog.yaml"), catalog, HEADERS["mes-sql-catalog.yaml"]);

// schema
const schIndex = readYaml(path.join(base, "mes-schema", "index.yaml"));
const schema = {
  version: schIndex.version,
  description: schIndex.description,
  datasources: readYaml(path.join(base, "mes-schema", schIndex.datasources_path)).datasources,
  tables: schIndex.tables_index.map(({ path: rel }) =>
    readYaml(path.join(base, "mes-schema", rel))
  ),
  enums: schIndex.enums_index.map(({ path: rel }) => readYaml(path.join(base, "mes-schema", rel))),
};
writeYaml(path.join(base, "mes-schema.yaml"), schema, HEADERS["mes-schema.yaml"]);

// flow-sql-map
const flowIndex = readYaml(path.join(base, "flow-sql-map", "index.yaml"));
const flowmap = {
  version: flowIndex.version,
  flows: flowIndex.flows_index.map(({ path: rel }) =>
    readYaml(path.join(base, "flow-sql-map", rel))
  ),
};
writeYaml(path.join(base, "flow-sql-map.yaml"), flowmap, HEADERS["flow-sql-map.yaml"]);

console.log("Merged to mes-sql-catalog.yaml, mes-schema.yaml, flow-sql-map.yaml");
