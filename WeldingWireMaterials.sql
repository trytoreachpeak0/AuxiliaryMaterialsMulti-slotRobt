/*
 Navicat Premium Data Transfer

 Source Server         : 前线多仓位数据库
 Source Server Type    : SQL Server
 Source Server Version : 16001000
 Source Host           : 172.19.206.222:1433
 Source Catalog        : SQNewWireAGV01
 Source Schema         : dbo

 Target Server Type    : SQL Server
 Target Server Version : 16001000
 File Encoding         : 65001

 Date: 03/06/2026 22:36:26
*/


-- ----------------------------
-- Table structure for WeldingWireMaterials
-- ----------------------------
IF EXISTS (SELECT * FROM sys.all_objects WHERE object_id = OBJECT_ID(N'[dbo].[WeldingWireMaterials]') AND type IN ('U'))
	DROP TABLE [dbo].[WeldingWireMaterials]
GO

CREATE TABLE [dbo].[WeldingWireMaterials] (
  [id] int  IDENTITY(1,1) NOT NULL,
  [material_type] nvarchar(50) COLLATE Chinese_PRC_CI_AS  NOT NULL,
  [specification_model] nvarchar(100) COLLATE Chinese_PRC_CI_AS  NOT NULL,
  [empty_spool_weight] decimal(10,2)  NOT NULL,
  [created_at] datetime DEFAULT (getdate()) NULL,
  [updated_at] datetime DEFAULT (getdate()) NULL
)
GO

ALTER TABLE [dbo].[WeldingWireMaterials] SET (LOCK_ESCALATION = TABLE)
GO


-- ----------------------------
-- Records of [WeldingWireMaterials]
-- ----------------------------
SET IDENTITY_INSERT [dbo].[WeldingWireMaterials] ON
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'1', N'焊丝', N'φ18 EX1P', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'2', N'焊丝', N'φ18 HA6', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'3', N'焊丝', N'φ18(0.7mil)RF2', N'20.00', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'4', N'焊丝', N'φ20', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'5', N'焊丝', N'φ20 AG0F', N'16.50', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'6', N'焊丝', N'φ20 EX1P-H', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'7', N'焊丝', N'φ20 EX1R', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'8', N'焊丝', N'φ20 GPH', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'9', N'焊丝', N'φ20 HA6', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'10', N'焊丝', N'φ20 HS-GP', N'20.00', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'11', N'焊丝', N'φ20 KL1C', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'12', N'焊丝', N'φ20(0.8mil)EX1', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'13', N'焊丝', N'φ20(0.8mil)RF2', N'20.00', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'14', N'焊丝', N'φ20-EX1P', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'15', N'焊丝', N'φ23 EX1P', N'15.60', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'16', N'焊丝', N'φ23 HA6', N'11.55', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'17', N'焊丝', N'φ23(0.9mil)RF2', N'16.60', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'18', N'焊丝', N'φ25 AG0F', N'16.50', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'19', N'焊丝', N'φ25 EX1P', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'20', N'焊丝', N'φ25 EX1p-H', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'21', N'焊丝', N'φ25 EX1R', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'22', N'焊丝', N'φ25 GPH', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'23', N'焊丝', N'φ25 HA6', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'24', N'焊丝', N'φ25 HS-GP', N'20.00', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'25', N'焊丝', N'φ25 KL1C', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'26', N'焊丝', N'φ25(1.0mil)', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'27', N'焊丝', N'φ25(1.0mil)EX1', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'28', N'焊丝', N'φ25(1.0mil)RF2', N'20.00', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'29', N'焊丝', N'φ30 EX1P', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'30', N'焊丝', N'φ30 EX1p-H', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'31', N'焊丝', N'φ30 EX1R', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'32', N'焊丝', N'φ30 HA6', N'11.55', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'33', N'焊丝', N'φ30 KL1C', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'34', N'焊丝', N'φ30(1.2mil)EX1', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'35', N'焊丝', N'φ30(1.2mil)Pdsoft', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'36', N'焊丝', N'φ30(MAXSOFT)', N'11.53', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'37', N'焊丝', N'φ32 EX1R', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'38', N'焊丝', N'φ32 HA6', N'11.55', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'39', N'焊丝', N'φ32 HS-GP', N'20.00', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'40', N'焊丝', N'φ32 Pdsoft', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'41', N'焊丝', N'φ32(MAXSOFT)', N'11.53', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'42', N'焊丝', N'φ38 HA6', N'11.55', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'43', N'焊丝', N'φ38(1.5mil)', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'44', N'焊丝', N'φ38(1.5mil)Pdsoft', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'45', N'焊丝', N'φ38(MAXSOFT)', N'11.53', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'46', N'焊丝', N'φ38-EX1R', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'47', N'焊丝', N'φ42 HS-GP', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'48', N'焊丝', N'φ42(MAXSOFT)', N'11.55', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'49', N'焊丝', N'φ50 HA6', N'11.55', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'50', N'焊丝', N'φ50(MAXSOFT)', N'11.53', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'51', N'焊丝', N'φ50-EX1R', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'52', N'焊丝', N'φ63 EX1R', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

INSERT INTO [dbo].[WeldingWireMaterials] ([id], [material_type], [specification_model], [empty_spool_weight], [created_at], [updated_at]) VALUES (N'53', N'焊丝', N'φ63 EX1S', N'17.54', N'2025-07-27 16:58:51.330', N'2025-07-27 16:58:51.330')
GO

SET IDENTITY_INSERT [dbo].[WeldingWireMaterials] OFF
GO


-- ----------------------------
-- Uniques structure for table WeldingWireMaterials
-- ----------------------------
ALTER TABLE [dbo].[WeldingWireMaterials] ADD CONSTRAINT [UQ_WeldingWire] UNIQUE NONCLUSTERED ([material_type] ASC, [specification_model] ASC)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)  
ON [PRIMARY]
GO


-- ----------------------------
-- Primary Key structure for table WeldingWireMaterials
-- ----------------------------
ALTER TABLE [dbo].[WeldingWireMaterials] ADD CONSTRAINT [PK__WeldingW__3213E83FFA73CA7A] PRIMARY KEY CLUSTERED ([id])
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)  
ON [PRIMARY]
GO

