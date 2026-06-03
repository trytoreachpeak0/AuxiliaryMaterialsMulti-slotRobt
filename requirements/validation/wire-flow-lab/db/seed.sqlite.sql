-- 焊丝发放流程验证工具 SQLite 种子数据
-- 与各 sql-catalog 的 sample_result 及流程默认输入对齐：
--   操作员 1001=张三、归还批号 14Z1888-0160、可用批号 14Z1888-0888、机台 1QH029、规格 φ42(MAXSOFT)

DELETE FROM app_slot;
DELETE FROM app_returned_weight;
DELETE FROM app_operation_log;
DELETE FROM mv_fw_username;
DELETE FROM v_fw_material_indetail;
DELETE FROM fw_eqpres_eqpinformation;
DELETE FROM fw_wip_trans;
DELETE FROM v_fw_wip_sublot;
DELETE FROM mes_mock_function_result;

-- ===== 应用库：格口 =====
INSERT INTO app_slot (slot_id, slot_no, usage_type, biz_state, door_state, lock_state, wire_lot_no, wire_spec, wire_code, shelflife, matlot, qty, wire_state, updated_at) VALUES
 (1, 'A01', 'available', 'idle',           'closed', 'locked', NULL,           NULL,            NULL,       NULL,                  NULL,      NULL, NULL,   datetime('now')),
 (2, 'A02', 'available', 'idle',           'closed', 'locked', NULL,           NULL,            NULL,       NULL,                  NULL,      NULL, NULL,   datetime('now')),
 (3, 'A03', 'shared',    'idle',           'closed', 'locked', NULL,           NULL,            NULL,       NULL,                  NULL,      NULL, NULL,   datetime('now')),
 (4, 'A04', 'returned',  'idle',           'closed', 'locked', NULL,           NULL,            NULL,       NULL,                  NULL,      NULL, NULL,   datetime('now')),
 (5, 'A05', 'available', 'available_wire', 'closed', 'locked', '14Z1888-0888', 'φ42(MAXSOFT)', 'WIRE-φ42', '2026-12-31 00:00:00', '14Z1888', 1.0, '可用', datetime('now')),
 (6, 'A06', 'returned',  'returned_wire',  'closed', 'locked', '14Z1888-0777', 'φ42(MAXSOFT)', 'WIRE-φ42', '2026-10-31 00:00:00', '14Z1888', 1.0, '归还', datetime('now')),
 (7, 'A07', 'available', 'available_wire', 'closed', 'locked', '14Z1999-0001', 'φ36(SOFT)',    'WIRE-φ36', '2026-11-30 00:00:00', '14Z1999', 1.0, '可用', datetime('now')),
 (8, 'A08', 'returned',  'idle',           'closed', 'locked', NULL,           NULL,            NULL,       NULL,                  NULL,      NULL, NULL,   datetime('now'));

-- ===== 应用库：按规格的归还重量 =====
INSERT INTO app_returned_weight (wire_spec, return_weight) VALUES
 ('φ42(MAXSOFT)', 120.5),
 ('φ36(SOFT)',    98.0);

-- ===== MES mock：操作员 =====
INSERT INTO mv_fw_username (usercode, username) VALUES
 ('1001', '张三'),
 ('1002', '李四');

-- ===== MES mock：物料明细（焊丝） =====
INSERT INTO v_fw_material_indetail (sublot, spec, type, code, shelflife, matlot, qty, state) VALUES
 ('14Z1888-0160', 'φ42(MAXSOFT)', '焊丝', 'WIRE-φ42', '2026-12-31 00:00:00', '14Z1888', 1.0, '归还'),
 ('14Z1888-0888', 'φ42(MAXSOFT)', '焊丝', 'WIRE-φ42', '2026-12-31 00:00:00', '14Z1888', 1.0, '可用'),
 ('14Z1888-0777', 'φ42(MAXSOFT)', '焊丝', 'WIRE-φ42', '2026-10-31 00:00:00', '14Z1888', 1.0, '归还'),
 ('14Z1999-0001', 'φ36(SOFT)',    '焊丝', 'WIRE-φ36', '2026-11-30 00:00:00', '14Z1999', 1.0, '可用');

-- ===== MES mock：机台 =====
INSERT INTO fw_eqpres_eqpinformation (eqpno, step) VALUES
 ('1QH029', '焊线'),
 ('1QH030', '焊线');

-- ===== MES mock：在制品事务（最近完工产品批号） =====
INSERT INTO fw_wip_trans (lot, eqp, step, task, remark, dates) VALUES
 ('P20260601-01', '1QH029', '焊线01', '完工', NULL,      '2026-06-01 08:00:00'),
 ('P20260602-01', '1QH029', '键合01', '完工', NULL,      '2026-06-02 09:30:00'),
 ('P20260602-09', '1QH029', '焊线01', '完工', '取消-误报','2026-06-02 23:59:00');

-- ===== MES mock：在制品子批（按产品批号聚合） =====
INSERT INTO v_fw_wip_sublot (lot, qty, step, state) VALUES
 ('P20260602-01', 3000, '焊线', '在制'),
 ('P20260602-01', 2000, '焊线', '在制'),
 ('P20260601-01', 1500, '键合', '关闭');

-- ===== MES mock：存储函数默认结果 =====
INSERT INTO mes_mock_function_result (func_name, result) VALUES
 ('Get_Mat_QuotaCheck', '0'),
 ('FUN_MAT_TRANS_NEW',  'SUCCESS');
