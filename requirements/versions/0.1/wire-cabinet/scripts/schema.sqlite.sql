-- 0.1 焊丝柜 SQLite 应用库

CREATE TABLE IF NOT EXISTS app_slot (
    slot_id     INTEGER PRIMARY KEY,
    slot_no     TEXT NOT NULL UNIQUE,
    door_position TEXT,
    slot_index  INTEGER,
    is_enabled  INTEGER NOT NULL DEFAULT 1,
    io_wired    INTEGER NOT NULL DEFAULT 1,
    usage_type  TEXT NOT NULL,
    biz_state   TEXT NOT NULL,
    door_state  TEXT NOT NULL DEFAULT 'closed',
    lock_state  TEXT NOT NULL DEFAULT 'locked',
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
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    ts           TEXT NOT NULL DEFAULT (datetime('now')),
    operator_id  TEXT,
    flow_id      TEXT,
    node_id      TEXT,
    slot_no      TEXT,
    lot_no       TEXT,
    message      TEXT,
    mes_result   TEXT,
    lock_result  TEXT,
    error_info   TEXT
);

CREATE TABLE IF NOT EXISTS agv_move_log (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ts              TEXT NOT NULL DEFAULT (datetime('now')),
    target_station  TEXT,
    order_id        TEXT,
    move_state      TEXT,
    battery_percent REAL,
    is_charge       INTEGER DEFAULT 0,
    message         TEXT
);
