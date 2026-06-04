-- 0.1 应用库：焊丝规格与空盘重量（镜像现场 WeldingWireMaterials）
CREATE TABLE IF NOT EXISTS welding_wire_materials (
    id                   INTEGER PRIMARY KEY,
    material_type        TEXT NOT NULL,
    specification_model  TEXT NOT NULL,
    empty_spool_weight   REAL NOT NULL,
    created_at           TEXT,
    updated_at           TEXT,
    UNIQUE (material_type, specification_model)
);
