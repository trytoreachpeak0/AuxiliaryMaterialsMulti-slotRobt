-- 焊丝发放流程验证工具 SQLite 建表脚本
-- 包含：应用数据库表(app_*) + MES mock 表(镜像 Oracle) + 函数 mock 结果表

-- ===== 应用数据库 =====
CREATE TABLE IF NOT EXISTS app_slot (
    slot_id     INTEGER PRIMARY KEY,
    slot_no     TEXT NOT NULL,
    usage_type  TEXT NOT NULL,                 -- available / returned / shared
    biz_state   TEXT NOT NULL,                 -- idle / available_wire / returned_wire / processing
    door_state  TEXT NOT NULL DEFAULT 'closed',-- open / closed
    lock_state  TEXT NOT NULL DEFAULT 'locked',-- locked / unlocked
    wire_lot_no TEXT,
    wire_spec   TEXT,
    wire_code   TEXT,
    shelflife   TEXT,
    matlot      TEXT,
    qty         REAL,
    wire_state  TEXT,
    updated_at  TEXT
);

CREATE TABLE IF NOT EXISTS welding_wire_materials (
    id                   INTEGER PRIMARY KEY,
    material_type        TEXT NOT NULL,
    specification_model  TEXT NOT NULL,
    empty_spool_weight   REAL NOT NULL,
    created_at           TEXT,
    updated_at           TEXT,
    UNIQUE (material_type, specification_model)
);

CREATE TABLE IF NOT EXISTS app_operation_log (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    ts      TEXT,
    flow_id TEXT,
    node_id TEXT,
    message TEXT
);

-- ===== MES mock 表 (镜像 Oracle) =====
CREATE TABLE IF NOT EXISTS mv_fw_username (
    usercode TEXT PRIMARY KEY,
    username TEXT
);

CREATE TABLE IF NOT EXISTS v_fw_material_indetail (
    sublot    TEXT,
    spec      TEXT,
    type      TEXT,
    code      TEXT,
    shelflife TEXT,
    matlot    TEXT,
    qty       REAL,
    state     TEXT,
    DISCOEQPNO TEXT
);

CREATE TABLE IF NOT EXISTS fw_eqpres_eqpinformation (
    eqpno TEXT,
    step  TEXT
);

CREATE TABLE IF NOT EXISTS fw_wip_trans (
    lot    TEXT,
    eqp    TEXT,
    step   TEXT,
    task   TEXT,
    remark TEXT,
    dates  TEXT
);

CREATE TABLE IF NOT EXISTS v_fw_wip_sublot (
    lot   TEXT,
    qty   REAL,
    step  TEXT,
    state TEXT
);

CREATE TABLE IF NOT EXISTS mes_mock_function_result (
    func_name TEXT PRIMARY KEY,
    result    TEXT
);
