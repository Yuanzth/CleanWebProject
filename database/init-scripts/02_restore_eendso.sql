USE [db_itassettrack];
GO

-- =============================================================================
-- InfraHub ASSET TRACK & ITAssetTrack PULSE DATABASE BACKUP
-- Generated At : 2026-09-03 14:22:50
-- Server       : asset_sysdv401.infrahub.net,1433
-- Database     : db_itassettrack
-- Cutoff Rule  : Cutoff Kemarin: Data s/d 2026-09-02 23:59:59 (sebelum 2026-09-03)
-- =============================================================================

SET NOCOUNT ON;
GO

-- Table: itassettrack.tbl_m_site
-- Exported Rows: 1
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_site]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_site] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_site] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

INSERT INTO [itassettrack].[tbl_m_site] ([Id], [Code], [Name], [Description], [CreatedAt]) VALUES
    ('78F4BBA5-C3DC-4391-BD80-1A76ABB353A2', 'HQ_DEPT', 'Jakarta Industrial Estate Pulogadung', '', '2026-08-07 09:51:10.482');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_department
-- Exported Rows: 103
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_department]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_department] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_department] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

INSERT INTO [itassettrack].[tbl_m_department] ([Id], [Name], [Description], [CreatedAt]) VALUES
    ('B19325D0-1A74-4687-A152-002869224DA6', 'SECURITY & EXT. RELATION', '', '2026-08-07 09:51:11.998'),
    ('857EE819-B020-4822-94CE-0191E9A44384', 'IT SOLUTION & DATA MANAGEMENT DEP', '', '2026-08-07 09:51:19.029'),
    ('A8F8553C-A92C-46F7-A83D-02FB3F46B159', 'COST CONTROL & CONTRACT ADMINISTR', '', '2026-08-07 09:51:22.372'),
    ('B20AE8D8-EEC7-4A1B-AEF7-051A85C57D99', 'FAO & WAREHOUSE DEPT.', '', '2026-08-07 09:51:22.693'),
    ('DB4509FF-B21D-403B-B958-0597F3D1222D', 'PROJECT & TRANSACTIONAL ADVISORY', '', '2026-08-07 09:51:22.225'),
    ('7EDDF3CF-9BF4-4508-8016-06A46F859121', 'TYRE MGT. & DEV. EXPERT', '', '2026-08-07 09:51:13.352'),
    ('F3758951-949F-4C96-9BB1-0705D46C364C', 'TECHNICAL SERVICE', '', '2026-08-07 09:51:16.800'),
    ('6DF8CC89-E087-41B7-877F-0717210CCA2B', 'OPERATION SHE DEPT.', '', '2026-08-07 09:51:36.664'),
    ('80A1E4DB-9682-41BC-933B-0B43E8FA6AB6', 'SHE OPERATION 1', '', '2026-08-07 09:51:26.220'),
    ('5F1079E6-F2DF-40DD-B06A-14B7C8550429', 'SHE OPERATION 2', '', '2026-08-07 09:51:13.297'),
    ('6A81D42B-65D0-47C6-8770-16BD97871ADA', 'OPERATIONAL CONTROL DEPT.', '', '2026-08-07 09:51:32.756'),
    ('2B3D553E-1936-4B37-A6E0-1E981EB98854', 'CORPORATE SOCIAL RESPONSIBILITY', '', '2026-08-07 09:51:11.878'),
    ('9E77D659-865A-4086-852C-21588CBF5A37', 'GENERAL SERVICES DEPT.', '', '2026-08-07 09:51:44.774'),
    ('EA3F27AE-9976-4616-94F0-28BB35B11122', 'MINE FINANCIAL EXPERT', '', '2026-08-07 09:51:27.964'),
    ('D67BAB99-FFB5-4BD4-B30A-2E3E021E9393', 'SHE REGION 2 DEPT.', '', '2026-08-07 09:51:37.729'),
    ('7901F892-C427-4DB4-9BEF-2EBDB2840092', 'TYRE MANAGEMENT & DEVELOPMENT', '', '2026-08-07 09:51:12.786'),
    ('547917B4-6B39-4D24-96BB-317EEE389B6D', 'MAIN PRODUCT & SSE LOGISTICS DEPT', '', '2026-08-07 09:51:15.175'),
    ('07DA08EB-77A1-45F6-8C8C-322FEBF03D05', 'PLANT EQUIP. MGT. & ADMINISTRATIO', '', '2026-08-07 09:51:21.041'),
    ('7CD62624-A0A1-4C4D-9F84-3251EACDDD1E', 'OPERATION REGION 2', '', '2026-08-07 09:51:41.693'),
    ('16BD251B-5D49-44C9-8EFA-360C44AE1484', 'MINE INFRASTRUCTURE', '', '2026-08-07 09:51:16.718'),
    ('18884A76-E035-46EB-BE55-36E8DB63EFCA', 'NEW & USED EQP. MGT DEPT.', '', '2026-08-07 09:51:46.805'),
    ('F2E59361-5508-4D82-9737-38F0B8196945', 'CORP. INNOVATION & SYSTEM DEV.', '', '2026-08-07 09:51:13.693'),
    ('160096E5-B14A-4CD9-A210-3CB9384ED01F', 'CORP. SECRETARY & COMPLIANCE', '', '2026-08-07 09:51:18.667'),
    ('A844B145-A527-4E88-AA87-3D873032CA1F', 'PLANT PEOPLE DEVELOPMENT', '', '2026-08-07 09:51:18.328'),
    ('0C520A54-49F2-465C-8730-401379392F2B', 'IT SOLUTION & DATA MANAGEMENT DEPT.', '', '2026-08-07 09:51:12.325'),
    ('9656022C-9D6D-4C11-9C8D-41CBD41B7682', 'MANAGEMENT SCIENCE', '', '2026-08-07 09:51:16.647'),
    ('2E6E89FF-B1D6-4DDA-A557-45C58056F098', 'PLANT SHE', '', '2026-08-07 09:51:22.901'),
    ('0F6A1BCD-0569-4A49-B098-47E383334138', 'ENGINEERING PEOPLE DEVELOPMENT', '', '2026-08-07 09:51:12.043'),
    ('7680B62A-65BD-4211-A7E9-481A76067ACF', 'SUPPORT EQUIPMENT EXPERT', '', '2026-08-07 09:51:16.127'),
    ('4BDC56AA-9B1A-43BF-BE4C-4A2914E0EB29', 'BIDANG SEKRETARIAT & ADMINISTRASI', '', '2026-08-07 09:51:15.744'),
    ('E6B0C3F5-E7BE-4E5D-824C-4D4D48951412', 'INTERNAL AUDIT DEPT.', '', '2026-08-07 09:51:12.364'),
    ('D33A4181-7E86-456F-8817-52079F967313', 'MGT. SCIENCE EXPERT', '', '2026-08-07 09:51:14.584'),
    ('B8DB48AE-0464-4434-BD1D-5293A8407D2F', 'SUPPORT EQUIPMENT', '', '2026-08-07 09:51:12.521'),
    ('8F7795CB-D038-4DFF-AD82-52B3A5A16999', 'FINANCIAL PLANNING & CONTROL DEPT', '', '2026-08-07 09:51:15.410'),
    ('01446033-50F3-4687-8662-542A7F3B3490', 'MINE FINANCIAL', '', '2026-08-07 09:51:42.728'),
    ('7EE5FE96-79EC-41C6-9D44-5681C8925B47', 'RISK MANAGEMENT DEPT.', '', '2026-08-07 09:51:31.282'),
    ('F5D6CD0C-9384-458F-8D76-57C8D8AE447D', 'MINING CONTRACTOR AREA 2', '', '2026-08-07 09:51:14.195'),
    ('287235CD-4E22-4859-8A6A-584729BAF3C5', 'SHE PEOPLE & SYSTEM MGT. DEPT.', '', '2026-08-07 09:51:25.417'),
    ('10C809BB-849E-4340-A2BC-5BEAE5ECD154', 'TAX', '', '2026-08-07 09:51:21.644'),
    ('1C993DB8-96F5-4E55-8249-5C36D7711249', 'IT BUSINESS PARTNER DEPT.', '', '2026-08-07 09:51:11.682'),
    ('E1E20EF8-8CF5-45AB-B5EB-5D787B621496', 'MAIN PRODUCT & SSE LOGISTICS', '', '2026-08-07 09:51:12.220'),
    ('02C66833-4679-4681-BE5C-60597D359856', 'PROMOTION & GOV. RELATION', '', '2026-08-07 09:51:12.826'),
    ('B4C64241-F4EF-4C98-B143-636AFBD892A7', 'PROCESSING PLANT & INFRA DEV.', '', '2026-08-07 09:51:44.487'),
    ('C48C8E20-CED2-4165-BEB8-659354808BBD', 'TREASURY DEPT.', '', '2026-08-07 09:51:37.790'),
    ('A63F6C8B-6E06-41CB-BDC8-6A5174807FF4', 'SUPPLY MANAGEMENT SHE', '', '2026-08-07 09:51:39.368'),
    ('D797E15A-5F07-4807-B3E1-6AABCD476321', 'Information Technology', '', '2026-08-07 09:51:10.506'),
    ('D5DDD323-FF95-46C0-8DC8-6B1D8D251945', 'PLANT REGION 1', '', '2026-08-07 09:51:15.942'),
    ('19946469-7092-4D15-956D-6DE96A190EDC', 'BOARD OF DIRECTOR', '', '2026-08-07 09:51:16.055'),
    ('F969FCE4-1610-423B-92F4-6F3BE5305F59', 'ACCOUNTING', '', '2026-08-07 09:51:15.696'),
    ('19DB4FD3-5370-44F6-BAF0-743949466432', 'MINE INFR. & GEOTECHNIC EXPERT', '', '2026-08-07 09:51:16.990'),
    ('B520F983-7D61-44AD-96E4-74480029B87A', 'MAINTENANCE PROCESS EXPERT', '', '2026-08-07 09:51:15.359'),
    ('5C64242B-388C-43B3-99E1-764B26E9B5ED', 'OPR.TECH.SUPP.& SYS CONTROL DEPT.', '', '2026-08-07 09:51:23.650'),
    ('19B27452-F715-4072-ACE4-7987ED4B32D0', 'IT INFRASTRUCTURE', '', '2026-08-07 09:51:11.529'),
    ('533FB716-3F7D-40FD-8DBD-7B568B9754E7', 'PROCUREMENT', '', '2026-08-07 09:51:14.253'),
    ('8B3A41F1-B205-4C38-B176-7DDD54A44FC1', 'MINE RESEARCH & TECH. VALUATION E', '', '2026-08-07 09:51:13.973'),
    ('E2762A2F-0893-44FE-BF2B-812014CC1F71', 'TREASURY', '', '2026-08-07 09:51:20.663'),
    ('45E94206-591F-45EE-B2D2-8180B62D99B3', 'ACCOUNTING DEPT.', '', '2026-08-07 09:51:42.499'),
    ('1D34B5AF-705A-4F17-AEC2-872A32EE6A73', 'HUMAN CAPITAL SERVICES & REWARD', '', '2026-08-07 09:51:11.758'),
    ('51618C57-C86C-4EDF-B01B-872E19E940EC', 'INDUSTRIAL RELATION', '', '2026-08-07 09:51:11.836'),
    ('690900D9-B45B-4A66-B3B4-9096892014D3', 'HQ_DEPT-LEGAL TRNSCTNS, PROJ, CORSEC', '', '2026-08-07 09:51:28.109'),
    ('9FBEBFC2-5820-4084-8E31-911BD741A6FC', 'BIDANG PENGEMBANGAN STD & MUTU', '', '2026-08-07 09:51:21.501'),
    ('733050C9-A5E0-468A-8F09-93FDF2921809', 'OPR TECHNICAL SUPPORT & SYSTEM CONTROL', '', '2026-08-07 09:51:16.241'),
    ('9A53DF33-2CE2-49EB-AE3E-952F57B978CC', 'GENERAL SERVICES', '', '2026-08-07 09:51:13.539'),
    ('9A1878D7-6601-442B-9205-97EB64FD7058', 'LEGAL OPR, COMPLIANCE & LITIGATIO', '', '2026-08-07 09:51:45.244'),
    ('8511C876-B982-4426-80A2-9820C0A27F24', 'MINING CONTRACTOR AREA 1', '', '2026-08-07 09:51:23.130'),
    ('77365141-2FBA-4365-AAA4-98E8B5E9F98F', 'OPERATION REGION 1', '', '2026-08-07 09:51:34.827'),
    ('DA811F73-25F8-4111-BC42-A2C435D2B382', 'PLANT REGION 2', '', '2026-08-07 09:51:31.720'),
    ('E3C02610-3B97-4ED1-963E-AB715415DC24', 'BIDANG SERTIFIKASI', '', '2026-08-07 09:51:20.705'),
    ('3538C752-4D7B-4EB9-A6B6-AC4C13A2391D', 'SUSTAINABILITY MANAGEMENT', '', '2026-08-07 09:51:12.269'),
    ('64DA0423-83C9-42B1-A0D4-AD898C0E96A0', 'PLANT REGION 3', '', '2026-08-07 09:51:23.412'),
    ('1330FF54-0A7C-4BD1-9E23-ADA3662034E5', 'SUPPLY MANAGEMENT DEVELOPMENT', '', '2026-08-07 09:51:14.323'),
    ('60EC77A4-7763-4E2A-9465-AED392186327', 'HUMAN CAPITAL & TALENT DEVELOPMENT', '', '2026-08-07 09:51:12.996'),
    ('73018AD4-B3FC-46B0-9213-B2A980D53D55', 'OPERATION REGION 3', '', '2026-08-07 09:51:40.624'),
    ('07A1F76E-C561-4395-A8C4-B2EB4B239AFC', 'ORG. DEV. & PERF. MGT. DEPT.', '', '2026-08-07 09:51:36.173'),
    ('858933C9-2E58-4FC7-A542-B52BD4588DD3', 'TAX DEPT.', '', '2026-08-07 09:51:28.598'),
    ('4B850E1E-E5AC-4FB2-B856-B56FC0FF4CC8', 'IT INFRASTRUCTURE DEPT.', '', '2026-08-07 09:51:33.126'),
    ('AC02F1DC-8F84-4B87-AD82-BDD4211C2A36', 'ORGANIZATION DEV & PERFORMANCE MGT', '', '2026-08-07 09:51:11.957'),
    ('98329C4D-A127-4261-B8B5-C035ABBDEE0A', 'OPERATIONAL CONTROL', '', '2026-08-07 09:51:13.443'),
    ('31F1970A-11FA-4842-B0DD-C0FEAA12396B', 'PEOPLE DEV. MAINT. EXPERT', '', '2026-08-07 09:51:16.508'),
    ('6EDA8A60-00EE-48DF-9F7C-C55E847EC98F', 'ENGINEERING PEOPLE DEV. DEPT.', '', '2026-08-07 09:51:23.695'),
    ('11929107-6680-470F-9A5D-C6FD695FD1B5', 'LIFE CYCLE & REMANUFACTURING EXPE', '', '2026-08-07 09:51:24.099'),
    ('022F8062-B538-428B-9F11-C9D8C5BED2C9', 'MINING CONTRACTOR AREA 3', '', '2026-08-07 09:51:26.530'),
    ('A045EB71-948C-479D-935F-CADE68F63D12', 'MINE RESEARCH & TECH. VALUATION', '', '2026-08-07 09:51:16.760'),
    ('F0BF95A9-E5E6-4CA2-B9B3-CBFE30F0944C', 'NEW & USED EQP MGT DEPT', '', '2026-08-07 09:51:22.010'),
    ('C1073EBF-3F41-4A79-97EB-CE2A105945DF', 'CORP. CULTURE & KNOWLEDGE MANAGEM', '', '2026-08-07 09:51:15.510'),
    ('6E3237D3-5EAE-4A1A-814E-CFE1775DFDAF', 'INFRASTRUCTURE', '', '2026-08-07 09:51:12.097'),
    ('7B888750-36A1-4807-AFB2-D1AA6F515782', 'OPERATION', '', '2026-08-07 09:51:24.891'),
    ('D223C766-F5D6-4367-968E-D304A10AFE62', 'TECHNICAL SERVICE EXPERT', '', '2026-08-07 09:51:17.504'),
    ('81753BE9-BAA7-4652-BEFA-D4A69895AE74', 'CIS GOVERNANCE', '', '2026-08-07 09:51:12.136'),
    ('058DCC91-3577-4B91-BBCC-D77860C36302', 'OPERATION STAFF DEVELOPMENT', '', '2026-08-07 09:51:14.132'),
    ('EE51AF52-192F-408F-863A-D77ACB639362', 'b3dce08c471b2a107d0c8339736d43d1', '', '2026-08-07 09:51:46.662'),
    ('3037DAEC-93AF-4BB1-BC38-DBADC3EEBC24', 'HC & GENERAL SERVICES', '', '2026-08-07 09:51:15.319'),
    ('37DDFE0C-85C9-4E4C-8337-DCC50FFA987E', 'CORP. STRATEGY & PDCA', '', '2026-08-07 09:51:12.656'),
    ('3FE95E0F-C221-48A2-BFA9-E0C561180A16', 'MINING CONTRACTOR', '', '2026-08-07 09:51:21.345'),
    ('8EEFBB0A-08F4-4E5D-967F-E185530AE277', 'SHE PEOPLE & SYSTEM DEVELOPMENT', '', '2026-08-07 09:51:12.910'),
    ('D7BB4CB0-A576-452C-8199-E2611595B975', 'SUBCONTRACTOR MANAGEMENT', '', '2026-08-07 09:51:16.199'),
    ('085486D3-413B-4D1B-8B8C-E6453E7D5838', 'DIGITAL TECHNOLOGY & INNOVATION D', '', '2026-08-07 09:51:13.883'),
    ('010072C2-7447-4C56-835F-EC883C7720F5', 'LITIG., LEGAL OPRT, SUPP. & ASSET', '', '2026-08-07 09:51:19.296'),
    ('1B6EE0A6-2E21-4A7C-AAF6-F0CD6A985EBC', 'FINANCIAL PLANNING & CONTROL', '', '2026-08-07 09:51:20.999'),
    ('0EE5C336-4C52-4622-A0E7-F1C016EB2978', 'INTERNAL AUDIT', '', '2026-08-07 09:51:13.155'),
    ('E598D7D9-7496-468B-BE5C-F63A42A0CD5D', 'CORP. VALUE CHAIN & STRATEGIC ITT', '', '2026-08-07 09:51:24.053'),
    ('077A55AE-AE80-4224-9EBA-FC72374698C8', 'RISK MANAGEMENT', '', '2026-08-07 09:51:23.321'),
    ('6DEABC57-AD8F-40DA-95E3-FF4B69255959', 'OPERATION TRAINING & SERVICES', '', '2026-08-07 09:51:12.174');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_division
-- Exported Rows: 20
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_division]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_division] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_division] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

INSERT INTO [itassettrack].[tbl_m_division] ([Id], [Code], [Name], [CreatedAt]) VALUES
    ('620A0400-908B-4D7F-A02D-1CB3BA27A416', 'FA', 'FA', '2026-08-07 09:51:15.418'),
    ('22476103-908C-4879-AF55-38707F03C20E', 'COLE', 'COLE', '2026-08-07 09:51:18.676'),
    ('2D52D867-E844-41F5-9582-53DF16B9EA48', 'CIA', 'CIA', '2026-08-07 09:51:12.372'),
    ('BF8D5DA8-A6FE-484E-9FBC-613D3F07A688', 'SM', 'SM', '2026-08-07 09:51:12.228'),
    ('9A0185DF-E3C4-4DCC-BD1E-61CF6C4640B6', 'LSP', 'LSP', '2026-08-07 09:51:15.704'),
    ('DEF0D5B3-88DA-4ECB-9907-6A76660E74AC', 'PLANT', 'PLANT', '2026-08-07 09:51:12.530'),
    ('2EC35837-D79C-4813-9E50-74B6A3AF4091', 'DIGI', 'DIGI', '2026-08-07 09:51:13.452'),
    ('41F39194-D8FC-4F27-ACCC-76D8368083B3', 'SHE', 'SHE', '2026-08-07 09:51:12.277'),
    ('8920E128-6B8A-4B96-950A-86BD5F202297', 'ITOS', 'IT Operation & Support', '2026-08-07 09:51:10.524'),
    ('8B5CE33E-9D12-49C9-B9A2-8AEF16092585', 'CCKM', 'CCKM', '2026-08-07 09:51:15.518'),
    ('28DDAA55-C4AA-40A8-A1DF-9D537E009587', 'ENG', 'ENG', '2026-08-07 09:51:12.056'),
    ('91E59831-55C6-4CF3-8C54-9EAF719480C3', 'YKP', 'YKP', '2026-08-07 09:51:16.516'),
    ('B5FE32BF-0011-4ED2-8CF3-A01249EDBB85', 'CPMD', 'CPMD', '2026-08-07 09:51:12.665'),
    ('DF425DB0-6199-4DEA-8258-A0743B63826F', 'SRGS', 'SRGS', '2026-08-07 09:51:11.886'),
    ('B1F958A7-E354-4958-B9A4-AFC614D75050', 'HCL', 'HCL', '2026-08-07 09:51:11.765'),
    ('36481590-A4BD-4610-88A5-BE700D43ACB4', 'CIS', 'CIS', '2026-08-07 09:51:11.551'),
    ('110EA5D9-6E28-4A27-B737-C0CA57EA46E4', 'BUSDEV', 'BUSDEV', '2026-08-07 09:51:12.834'),
    ('0AADAD5A-2AEB-4C93-8E52-D35DEA9E448B', 'BOD', 'BOD', '2026-08-07 09:51:16.401'),
    ('2A4A30E5-26F2-49BA-AD4B-D54AE252B178', 'OPR', 'OPR', '2026-08-07 09:51:12.181'),
    ('9D6BA664-DCDA-4FAF-AE7F-FAC95252CE8B', 'YIP', 'YIP', '2026-08-07 09:51:25.943');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_device_type
-- Exported Rows: 2
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_device_type]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_device_type] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_device_type] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

INSERT INTO [itassettrack].[tbl_m_device_type] ([Id], [Name], [CreatedAt]) VALUES
    ('AA1DFCB4-A670-424E-9707-761C39C2CF0D', 'Desktop', '2026-08-07 09:51:11.559'),
    ('7A8570E4-03FC-4026-933B-FCC824C51F92', 'Laptop', '2026-08-07 09:51:11.690');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_device_category
-- Exported Rows: 5
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_device_category]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_device_category] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_device_category] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

INSERT INTO [itassettrack].[tbl_m_device_category] ([Id], [Name], [CreatedAt]) VALUES
    ('16A13023-E448-4FFA-91E8-09C3C3A12765', 'NB Working Level', '2026-08-07 09:51:11.697'),
    ('7529C54B-0C58-4442-AF48-3D932634EDB2', 'NB Extreme Level', '2026-08-07 09:51:13.039'),
    ('FEE05E1B-833B-40C3-890F-746EF89F3681', 'NB Custom Level', '2026-08-07 09:51:13.309'),
    ('5313EBB4-1B61-4CF8-97DF-A5C991ED93CB', 'PC Working Level', '2026-08-07 09:51:16.559'),
    ('1210C116-8014-45C2-92EA-B007E84D3E2D', 'PC Workstation', '2026-08-07 09:51:11.574');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_brand
-- Exported Rows: 6
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_brand]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_brand] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_brand] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

INSERT INTO [itassettrack].[tbl_m_brand] ([Id], [Name], [CreatedAt]) VALUES
    ('1BB19A98-BCA9-4B6D-A998-0390EC5DD8A7', 'Apple', '2026-08-07 09:51:13.047'),
    ('C1494C7B-2B09-46CD-ACDC-32BCFB4B9672', 'HP', '2026-08-07 09:51:16.567'),
    ('28C2CEA0-1F3D-4D77-B50A-3CA9D75D1A04', 'Lenovo', '2026-08-07 09:51:20.476'),
    ('3929B72F-2D2F-4157-B101-51D63859157A', 'Rakitan', '2026-08-07 09:51:18.014'),
    ('D1F1CC59-DA73-48E2-9B17-66A56C17893A', 'Dell', '2026-08-07 09:51:11.591'),
    ('B486270F-F6A2-4224-A9A8-FB2173044A03', 'Axioo', '2026-08-07 09:51:20.519');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_vendor
-- Exported Rows: 7
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_vendor]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_vendor] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(150) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_vendor] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

INSERT INTO [itassettrack].[tbl_m_vendor] ([Id], [Name], [CreatedAt]) VALUES
    ('DD64C107-580A-4974-A15D-27D1754B3515', 'GENERIC VENDOR A', '2026-08-07 09:51:11.605'),
    ('E05D6E62-C73E-49E8-A1DA-2C791EC4228C', 'KOPERASI InfraHubNDIRI', '2026-08-07 09:51:18.082'),
    ('60E945AA-B620-41E5-94D7-3292964FAA48', 'InfraHub', '2026-08-07 09:51:13.055'),
    ('5DBD8B17-533C-48CB-A855-5D3F8E02EED0', 'QUARTEE TECHNOLOGIES, PT', '2026-08-07 09:51:46.355'),
    ('7D4E0F85-4E5A-4609-91B5-6384DA8E2C91', 'MITRA INTEGRASI INFORMATIKA, PT', '2026-08-07 09:51:20.333'),
    ('92AD5C0E-A66D-4FC6-8660-7D9FADC1DB56', 'PANCA PUTRA SOLUSINDO, PT', '2026-08-07 09:51:46.415'),
    ('E83D6008-F8A8-47AE-97A6-B19CBF779C7A', 'BERCA HARDAYAPERKASA, PT', '2026-08-07 09:51:16.577');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_employee
-- Exported Rows: 787
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_employee]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_employee] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [NRP] NVARCHAR(50) NOT NULL,
    [FullName] NVARCHAR(150) NOT NULL,
    [Email] NVARCHAR(100) NOT NULL,
    [Phone] NVARCHAR(50) NOT NULL,
    [SiteId] UNIQUEIDENTIFIER NULL,
    [DepartmentId] UNIQUEIDENTIFIER NULL,
    [DivisionId] UNIQUEIDENTIFIER NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_employee] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_trouble_category
-- Exported Rows: 0
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_trouble_category]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_trouble_category] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_trouble_category] PRIMARY KEY CLUSTERED ([Id])
);
END
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_m_user
-- Exported Rows: 13
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_m_user]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_m_user] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Username] NVARCHAR(50) NOT NULL,
    [FullName] NVARCHAR(100) NOT NULL,
    [NRP] NVARCHAR(50) NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    [Role] NVARCHAR(30) NOT NULL,
    [Site] NVARCHAR(50) NOT NULL,
    [IsActive] BIT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [LastLoginAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_m_user] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_asset
-- Exported Rows: 875
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_asset]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_asset] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [NoSN] NVARCHAR(50) NOT NULL,
    [SerialNumber] NVARCHAR(100) NOT NULL,
    [ComputerName] NVARCHAR(100) NOT NULL,
    [Model] NVARCHAR(150) NOT NULL,
    [OperatingSystem] NVARCHAR(MAX) NOT NULL,
    [WindowsVersion] NVARCHAR(MAX) NOT NULL,
    [Ram] NVARCHAR(MAX) NOT NULL,
    [Storage] NVARCHAR(MAX) NOT NULL,
    [Processor] NVARCHAR(MAX) NOT NULL,
    [Gpu] NVARCHAR(MAX) NOT NULL,
    [Status] NVARCHAR(50) NOT NULL,
    [Remarks] NVARCHAR(MAX) NOT NULL,
    [DeviceTypeId] UNIQUEIDENTIFIER NULL,
    [DeviceCategoryId] UNIQUEIDENTIFIER NULL,
    [BrandId] UNIQUEIDENTIFIER NULL,
    [VendorId] UNIQUEIDENTIFIER NULL,
    [DeviceType] NVARCHAR(MAX) NOT NULL,
    [Brand] NVARCHAR(MAX) NOT NULL,
    [Series] NVARCHAR(MAX) NOT NULL,
    [Site] NVARCHAR(MAX) NOT NULL,
    [Division] NVARCHAR(MAX) NOT NULL,
    [Department] NVARCHAR(MAX) NOT NULL,
    [AssignedTo] NVARCHAR(MAX) NOT NULL,
    [NRP] NVARCHAR(MAX) NOT NULL,
    [KeyboardSN] NVARCHAR(MAX) NOT NULL,
    [MouseSN] NVARCHAR(MAX) NOT NULL,
    [MonitorSN] NVARCHAR(MAX) NOT NULL,
    [AdditionalHardware] NVARCHAR(MAX) NOT NULL,
    [LastOnlineTime] DATETIME2 NULL,
    [IsAFK] BIT NOT NULL,
    [Uptime] NVARCHAR(MAX) NOT NULL,
    [ActiveApps] NVARCHAR(MAX) NOT NULL,
    [LastIp] NVARCHAR(45) NOT NULL,
    [LastLoggedInUser] NVARCHAR(100) NOT NULL,
    [LastBatteryHealthPct] INT NULL,
    [IsOnline] BIT NOT NULL,
    [IsUserMismatch] BIT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_asset] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_pm_plan
-- Exported Rows: 2
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_pm_plan]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_pm_plan] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [MonthYear] NVARCHAR(MAX) NOT NULL,
    [TargetQuantity] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_pm_plan] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_scheduled_maint
-- Exported Rows: 2
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_scheduled_maint]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_scheduled_maint] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [PlanMonth] NVARCHAR(MAX) NOT NULL,
    [Tanggal] DATETIME2 NOT NULL,
    [StartTime] TIME NOT NULL,
    [EndTime] TIME NOT NULL,
    [SerialNumber] NVARCHAR(MAX) NOT NULL,
    [NamaUser] NVARCHAR(MAX) NOT NULL,
    [Site] NVARCHAR(MAX) NOT NULL,
    [ComputerName] NVARCHAR(MAX) NOT NULL,
    [DeviceType] NVARCHAR(MAX) NOT NULL,
    [Brand] NVARCHAR(MAX) NOT NULL,
    [Processor] NVARCHAR(MAX) NOT NULL,
    [ProcessorKondisi] NVARCHAR(MAX) NOT NULL,
    [Ram] NVARCHAR(MAX) NOT NULL,
    [RamKondisi] NVARCHAR(MAX) NOT NULL,
    [Storage] NVARCHAR(MAX) NOT NULL,
    [StorageKondisi] NVARCHAR(MAX) NOT NULL,
    [WindowsVersion] NVARCHAR(MAX) NOT NULL,
    [KeyboardBrand] NVARCHAR(MAX) NOT NULL,
    [KeyboardSN] NVARCHAR(MAX) NOT NULL,
    [KeyboardKondisi] NVARCHAR(MAX) NOT NULL,
    [MouseBrand] NVARCHAR(MAX) NOT NULL,
    [MouseSN] NVARCHAR(MAX) NOT NULL,
    [MouseKondisi] NVARCHAR(MAX) NOT NULL,
    [MonitorBrand] NVARCHAR(MAX) NOT NULL,
    [MonitorModel] NVARCHAR(MAX) NOT NULL,
    [MonitorSN] NVARCHAR(MAX) NOT NULL,
    [MonitorKondisi] NVARCHAR(MAX) NOT NULL,
    [FisikKondisiBaik] BIT NOT NULL,
    [UEFIPassword] BIT NOT NULL,
    [BootOrderStorage] BIT NOT NULL,
    [BootOrderDisable] BIT NOT NULL,
    [DisableAdmin] BIT NOT NULL,
    [DiskCleanUp] BIT NOT NULL,
    [SpaceSSD] BIT NOT NULL,
    [Remark] NVARCHAR(MAX) NOT NULL,
    [Priority] NVARCHAR(MAX) NOT NULL,
    [Status] NVARCHAR(MAX) NOT NULL,
    [Reschedule3Sig] NVARCHAR(MAX) NULL,
    [Reschedule2Sig] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [ActualDate] DATETIME2 NULL,
    [ContactInfo] NVARCHAR(MAX) NOT NULL,
    [DeviceCategory] NVARCHAR(MAX) NOT NULL,
    [DeviceKondisi] NVARCHAR(MAX) NOT NULL,
    [Division] NVARCHAR(MAX) NOT NULL,
    [Email] NVARCHAR(MAX) NOT NULL,
    [ExecBySignature] NVARCHAR(MAX) NULL,
    [ItosSectionHeadSignature] NVARCHAR(MAX) NULL,
    [Keluhan] NVARCHAR(MAX) NOT NULL,
    [LeaderSignature] NVARCHAR(MAX) NULL,
    [Model] NVARCHAR(MAX) NOT NULL,
    [NRP] NVARCHAR(MAX) NOT NULL,
    [NoDocument] NVARCHAR(MAX) NOT NULL,
    [PlanDate] DATETIME2 NULL,
    [Reschedule1Date] DATETIME2 NULL,
    [Reschedule1Sig] NVARCHAR(MAX) NULL,
    [Reschedule2Date] DATETIME2 NULL,
    [Reschedule3Date] DATETIME2 NULL,
    [StartTimeRecorded] BIT NOT NULL,
    [ContactMethod] NVARCHAR(MAX) NULL,
    [ContactedAt] DATETIME2 NULL,
    [ContactedBy] NVARCHAR(MAX) NULL,
    [LeaderApproved] BIT NOT NULL,
    [LeaderApprovedAt] DATETIME2 NULL,
    [LeaderApprovedBy] NVARCHAR(MAX) NULL,
    [OriginalPlanDate] DATETIME2 NULL,
    [PerformedBy] NVARCHAR(MAX) NULL,
    [PmCompletedAt] DATETIME2 NULL,
    [PmStartedAt] DATETIME2 NULL,
    [Reschedule1Reason] NVARCHAR(MAX) NULL,
    [Reschedule2Reason] NVARCHAR(MAX) NULL,
    [Reschedule3Reason] NVARCHAR(MAX) NULL,
    [ReturnNotes] NVARCHAR(MAX) NULL,
    [ReturnedAt] DATETIME2 NULL,
    [SectionHeadApproved] BIT NOT NULL,
    [SectionHeadApprovedAt] DATETIME2 NULL,
    [SectionHeadApprovedBy] NVARCHAR(MAX) NULL,
    [UserConfirmedAt] DATETIME2 NULL,
    [UserHandoverSignature] NVARCHAR(MAX) NULL,
    [UserNotPresent] BIT NOT NULL,
    [UserResponse] NVARCHAR(MAX) NULL,
    [KeyboardAda] BIT NOT NULL,
    [MonitorAda] BIT NOT NULL,
    [MouseAda] BIT NOT NULL,
    [ChargerAda] BIT NOT NULL,
    [MultiportAda] BIT NOT NULL,
    [TasLaptopAda] BIT NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_scheduled_maint] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_unscheduled_maint
-- Exported Rows: 0
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_unscheduled_maint]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_unscheduled_maint] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Date] DATETIME2 NOT NULL,
    [StartTime] TIME NOT NULL,
    [EndTime] TIME NOT NULL,
    [Priority] NVARCHAR(MAX) NOT NULL,
    [Status] NVARCHAR(MAX) NOT NULL,
    [TroubleCategory] NVARCHAR(MAX) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,
    [ActionTaken] NVARCHAR(MAX) NOT NULL,
    [SerialNumber] NVARCHAR(MAX) NOT NULL,
    [NamaUser] NVARCHAR(MAX) NOT NULL,
    [Site] NVARCHAR(MAX) NOT NULL,
    [ComputerName] NVARCHAR(MAX) NOT NULL,
    [DeviceType] NVARCHAR(MAX) NOT NULL,
    [Brand] NVARCHAR(MAX) NOT NULL,
    [Processor] NVARCHAR(MAX) NOT NULL,
    [Ram] NVARCHAR(MAX) NOT NULL,
    [Storage] NVARCHAR(MAX) NOT NULL,
    [WindowsVersion] NVARCHAR(MAX) NOT NULL,
    [KeyboardSN] NVARCHAR(MAX) NOT NULL,
    [MouseSN] NVARCHAR(MAX) NOT NULL,
    [MonitorSN] NVARCHAR(MAX) NOT NULL,
    [UserSignature] NVARCHAR(MAX) NULL,
    [TechSignature] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    [LeaderSignature] NVARCHAR(MAX) NULL,
    [SectionHeadSignature] NVARCHAR(MAX) NULL,
    [LeaderApproved] BIT NOT NULL,
    [SectionHeadApproved] BIT NOT NULL,
    [LeaderApprovedBy] NVARCHAR(100) NULL,
    [SectionHeadApprovedBy] NVARCHAR(100) NULL,
    [LeaderApprovedAt] DATETIME2 NULL,
    [SectionHeadApprovedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_unscheduled_maint] PRIMARY KEY CLUSTERED ([Id])
);
END
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_serah_terima
-- Exported Rows: 0
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_serah_terima]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_serah_terima] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [NoDocument] NVARCHAR(MAX) NOT NULL,
    [Tipe] NVARCHAR(MAX) NOT NULL,
    [Date] DATETIME2 NOT NULL,
    [NamaUser] NVARCHAR(MAX) NOT NULL,
    [NRP] NVARCHAR(MAX) NOT NULL,
    [Site] NVARCHAR(MAX) NOT NULL,
    [Department] NVARCHAR(MAX) NOT NULL,
    [Division] NVARCHAR(MAX) NOT NULL,
    [DeviceType] NVARCHAR(MAX) NOT NULL,
    [Brand] NVARCHAR(MAX) NOT NULL,
    [Series] NVARCHAR(MAX) NOT NULL,
    [SerialNumber] NVARCHAR(MAX) NOT NULL,
    [ComputerName] NVARCHAR(MAX) NOT NULL,
    [OperatingSystem] NVARCHAR(MAX) NOT NULL,
    [AdditionalHardware] NVARCHAR(MAX) NOT NULL,
    [Ram] NVARCHAR(MAX) NOT NULL,
    [Storage] NVARCHAR(MAX) NOT NULL,
    [KeyboardSN] NVARCHAR(MAX) NOT NULL,
    [MouseSN] NVARCHAR(MAX) NOT NULL,
    [MonitorSN] NVARCHAR(MAX) NOT NULL,
    [Alasan] NVARCHAR(MAX) NOT NULL,
    [Remarks] NVARCHAR(MAX) NOT NULL,
    [Status] NVARCHAR(MAX) NOT NULL,
    [UserSignature] NVARCHAR(MAX) NULL,
    [AdminSignature] NVARCHAR(MAX) NULL,
    [NamaPenyerah] NVARCHAR(MAX) NOT NULL,
    [NamaPenerima] NVARCHAR(MAX) NOT NULL,
    [NamaDiketahui] NVARCHAR(MAX) NOT NULL,
    [SignaturePenyerah] NVARCHAR(MAX) NULL,
    [SignaturePenerima] NVARCHAR(MAX) NULL,
    [SignatureDiketahui] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_serah_terima] PRIMARY KEY CLUSTERED ([Id])
);
END
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_r_asset_assignment
-- Exported Rows: 873
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_r_asset_assignment]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_r_asset_assignment] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [AssetId] UNIQUEIDENTIFIER NOT NULL,
    [EmployeeId] UNIQUEIDENTIFIER NOT NULL,
    [AssignDate] DATETIME2 NOT NULL,
    [ReturnDate] DATETIME2 NULL,
    [IsActive] BIT NOT NULL,
    [Notes] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_r_asset_assignment] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_r_maintenance_photo
-- Exported Rows: 0
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_r_maintenance_photo]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_r_maintenance_photo] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [MaintenanceType] NVARCHAR(MAX) NOT NULL,
    [MaintenanceId] UNIQUEIDENTIFIER NOT NULL,
    [FilePath] NVARCHAR(MAX) NOT NULL,
    [Caption] NVARCHAR(MAX) NOT NULL,
    [UploadedAt] DATETIME2 NOT NULL,
    [PhotoType] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_r_maintenance_photo] PRIMARY KEY CLUSTERED ([Id])
);
END
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_r_serah_terima_photo
-- Exported Rows: 0
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_r_serah_terima_photo]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_r_serah_terima_photo] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [SerahTerimaId] UNIQUEIDENTIFIER NOT NULL,
    [FilePath] NVARCHAR(MAX) NOT NULL,
    [Caption] NVARCHAR(MAX) NOT NULL,
    [UploadedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_r_serah_terima_photo] PRIMARY KEY CLUSTERED ([Id])
);
END
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_s_unregistered_assets
-- Exported Rows: 2
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_s_unregistered_assets]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_s_unregistered_assets] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [SerialNumber] NVARCHAR(100) NOT NULL,
    [Hostname] NVARCHAR(100) NOT NULL,
    [LoggedInUser] NVARCHAR(100) NOT NULL,
    [OsVersion] NVARCHAR(150) NOT NULL,
    [LocalIp] NVARCHAR(45) NOT NULL,
    [Ssid] NVARCHAR(100) NOT NULL,
    [Latitude] DECIMAL(10,8) NULL,
    [Longitude] DECIMAL(11,8) NULL,
    [LastSeenAt] DATETIME2 NOT NULL,
    [ReportCount] INT NOT NULL,
    [Status] NVARCHAR(50) NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_s_unregistered_assets] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: dbo.__EFMigrationsHistory
-- Exported Rows: 8
-- Cutoff: False

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'dbo')
BEGIN
    EXEC('CREATE SCHEMA [dbo]')
END
GO

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId] NVARCHAR(150) NOT NULL,
    [ProductVersion] NVARCHAR(32) NOT NULL,
    CONSTRAINT [PK_dbo___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId])
);
END
GO

INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES
    ('20260722041415_InitialCreate_PrefixedUUID_Clean', '8.0.10'),
    ('20260722090953_UpdateScheduledMaintFormPm', '8.0.10'),
    ('20260724073544_AddPmExecutionFields', '8.0.10'),
    ('20260724074356_AddAppUserTable', '8.0.10'),
    ('20260807030200_AddAgentTelemetryTables', '8.0.10'),
    ('20260810023638_AddAksesorisAdaFlag', '8.0.10'),
    ('20260811010459_AddAccessoryFields', '8.0.10'),
    ('20260819043653_AddLoginSessionsAndDisplayName', '8.0.10');
GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_agent_reports
-- Exported Rows: 165
-- Cutoff: True

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_agent_reports]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_agent_reports] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [AssetId] UNIQUEIDENTIFIER NULL,
    [SerialNumber] NVARCHAR(100) NOT NULL,
    [Hostname] NVARCHAR(100) NOT NULL,
    [LoggedInUser] NVARCHAR(100) NOT NULL,
    [AssignedNrp] NVARCHAR(100) NOT NULL,
    [IsUserMismatch] BIT NOT NULL,
    [OsVersion] NVARCHAR(150) NOT NULL,
    [ReportType] NVARCHAR(50) NOT NULL,
    [ScheduleTriggered] NVARCHAR(50) NULL,
    [RecordIntervalSeconds] INT NOT NULL,
    [PeriodStart] DATETIME2 NOT NULL,
    [PeriodEnd] DATETIME2 NOT NULL,
    [CpuUptimeSeconds] BIGINT NOT NULL,
    [BatteryHealthPct] INT NULL,
    [LocalIp] NVARCHAR(45) NOT NULL,
    [Ssid] NVARCHAR(100) NOT NULL,
    [Latitude] DECIMAL(10,8) NULL,
    [Longitude] DECIMAL(11,8) NULL,
    [CpuMinPct] INT NOT NULL,
    [CpuAvgPct] INT NOT NULL,
    [CpuMaxPct] INT NOT NULL,
    [RamMinPct] INT NOT NULL,
    [RamAvgPct] INT NOT NULL,
    [RamMaxPct] INT NOT NULL,
    [GpuMinPct] INT NOT NULL,
    [GpuAvgPct] INT NOT NULL,
    [GpuMaxPct] INT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [DisplayName] NVARCHAR(150) NOT NULL,
    [DailyNetworkDownloadMb] DECIMAL(12,2) NULL,
    [DailyNetworkUploadMb] DECIMAL(12,2) NULL,
    [DailyNetworkTotalMb] DECIMAL(12,2) NULL,
    [DailyNetworkUsageText] NVARCHAR(255) NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_agent_reports] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_agent_app_timelines
-- Exported Rows: 857
-- Cutoff: True

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_agent_app_timelines]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_agent_app_timelines] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ReportId] UNIQUEIDENTIFIER NOT NULL,
    [SerialNumber] NVARCHAR(100) NOT NULL,
    [AppName] NVARCHAR(255) NOT NULL,
    [StartTime] NVARCHAR(30) NULL,
    [EndTime] NVARCHAR(30) NULL,
    [DurationMinutes] DECIMAL(10,2) NOT NULL,
    [IsAFK] BIT NOT NULL,
    [CreatedAt] DATETIME NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_agent_app_timelines] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_agent_battery_timelines
-- Exported Rows: 377
-- Cutoff: True

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_agent_battery_timelines]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_agent_battery_timelines] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ReportId] UNIQUEIDENTIFIER NOT NULL,
    [SerialNumber] NVARCHAR(100) NOT NULL,
    [Status] NVARCHAR(50) NOT NULL,
    [StartTime] NVARCHAR(20) NOT NULL,
    [EndTime] NVARCHAR(20) NOT NULL,
    [StartPct] INT NOT NULL,
    [EndPct] INT NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_agent_battery_timelines] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_agent_installed_software
-- Exported Rows: 969
-- Cutoff: True

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_agent_installed_software]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_agent_installed_software] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ReportId] UNIQUEIDENTIFIER NOT NULL,
    [SerialNumber] VARCHAR(100) NOT NULL,
    [SoftwareName] NVARCHAR(255) NOT NULL,
    [SoftwareVersion] VARCHAR(100) NULL,
    [Publisher] NVARCHAR(255) NULL,
    [InstallDate] VARCHAR(50) NULL,
    [CreatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_agent_installed_software] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_agent_login_sessions
-- Exported Rows: 143
-- Cutoff: True

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_agent_login_sessions]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_agent_login_sessions] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ReportId] UNIQUEIDENTIFIER NULL,
    [SerialNumber] NVARCHAR(100) NOT NULL,
    [Hostname] NVARCHAR(100) NOT NULL,
    [LoggedInUser] NVARCHAR(100) NOT NULL,
    [DisplayName] NVARCHAR(150) NOT NULL,
    [SessionStart] DATETIME2 NOT NULL,
    [SessionEnd] DATETIME2 NULL,
    [DurationMinutes] INT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_agent_login_sessions] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO


-- ----------------------------------------------------------------------

-- Table: itassettrack.tbl_t_agent_top_apps
-- Exported Rows: 1067
-- Cutoff: True

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'itassettrack')
BEGIN
    EXEC('CREATE SCHEMA [itassettrack]')
END
GO

IF OBJECT_ID(N'[itassettrack].[tbl_t_agent_top_apps]', N'U') IS NULL
BEGIN
CREATE TABLE [itassettrack].[tbl_t_agent_top_apps] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [ReportId] UNIQUEIDENTIFIER NOT NULL,
    [SerialNumber] NVARCHAR(100) NOT NULL,
    [AppName] NVARCHAR(255) NOT NULL,
    [RecordCount] INT NOT NULL,
    [EstimatedDurationMinutes] DECIMAL(10,2) NOT NULL,
    [IsAFK] BIT NOT NULL,
    [PeriodStart] DATETIME2 NOT NULL,
    CONSTRAINT [PK_itassettrack_tbl_t_agent_top_apps] PRIMARY KEY CLUSTERED ([Id])
);
END
GO

GO

GO


-- ----------------------------------------------------------------------

-- Table: log.tbl_log_database
-- Exported Rows: 114
-- Cutoff: True

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'log')
BEGIN
    EXEC('CREATE SCHEMA [log]')
END
GO

IF OBJECT_ID(N'[log].[tbl_log_database]', N'U') IS NULL
BEGIN
CREATE TABLE [log].[tbl_log_database] (
    [DatabaseLogID] INT IDENTITY(1,1) NOT NULL,
    [PostTime] DATETIME NOT NULL,
    [ServerName] NVARCHAR(128) NOT NULL,
    [DatabaseName] NVARCHAR(128) NOT NULL,
    [DatabaseUser] NVARCHAR(128) NOT NULL,
    [LoginName] NVARCHAR(128) NOT NULL,
    [Event] NVARCHAR(128) NOT NULL,
    [Schema] NVARCHAR(128) NULL,
    [Object] NVARCHAR(128) NULL,
    [TSQL] VARCHAR(MAX) NOT NULL,
    [XmlEvent] XML NOT NULL,
    [Hostname] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_log_tbl_log_database] PRIMARY KEY CLUSTERED ([DatabaseLogID])
);
END
GO

IF OBJECT_ID(N'[log].[tbl_user_db_object_versioning]', N'U') IS NULL
BEGIN
CREATE TABLE [log].[tbl_user_db_object_versioning] (
    [VersioningID] INT IDENTITY(1,1) NOT NULL,
    [PostTime] DATETIME NOT NULL,
    [ServerName] NVARCHAR(128) NOT NULL,
    [DatabaseName] NVARCHAR(128) NOT NULL,
    [SchemaName] NVARCHAR(128) NOT NULL,
    [ObjectName] NVARCHAR(128) NOT NULL,
    [ObjectType] VARCHAR(20) NOT NULL,
    [EventType] NVARCHAR(128) NOT NULL,
    [VersionNumber] INT NOT NULL,
    [IsCurrent] BIT NOT NULL,
    [ObjectDefinition] NVARCHAR(MAX) NULL,
    [DefinitionHash] VARBINARY(32) NULL,
    [LoginName] NVARCHAR(128) NOT NULL,
    [Hostname] NVARCHAR(128) NOT NULL,
    CONSTRAINT [PK_log_tbl_user_db_object_versioning] PRIMARY KEY CLUSTERED ([VersioningID])
);
END
GO

SET IDENTITY_INSERT [log].[tbl_user_db_object_versioning] ON;
GO

SET IDENTITY_INSERT [log].[tbl_user_db_object_versioning] OFF;
GO


-- =============================================================================
-- DUMMY SEED DATA FOR ITAssetTrack SYSTEM
-- =============================================================================
USE [db_itassettrack];
GO

-- Seed Dummy Users (Password: Password123! hashed with BCrypt)
IF NOT EXISTS (SELECT 1 FROM [itassettrack].[tbl_m_user] WHERE Username = 'admin')
BEGIN
    INSERT INTO [itassettrack].[tbl_m_user] 
        ([Id], [Username], [FullName], [NRP], [PasswordHash], [Role], [Site], [IsActive], [CreatedAt])
    VALUES 
        (NEWID(), 'admin', 'Administrator ITAssetTrack', '99999999', '$2a$11$9iJ2h.R7B1D0E3F4G5H6I7J8K9L0M1N2O3P4Q5R6S7T8U9V0W1X2Y', 'Admin', 'HQ_DEPT', 1, GETDATE()),
        (NEWID(), 'elyas', 'Elyas Technician', '88888888', '$2a$11$9iJ2h.R7B1D0E3F4G5H6I7J8K9L0M1N2O3P4Q5R6S7T8U9V0W1X2Y', 'Teknisi', 'HQ_DEPT', 1, GETDATE()),
        (NEWID(), 'franz', 'Franz Approver', '77777777', '$2a$11$9iJ2h.R7B1D0E3F4G5H6I7J8K9L0M1N2O3P4Q5R6S7T8U9V0W1X2Y', 'Approver', 'HQ_DEPT', 1, GETDATE());
END
GO

-- Seed Dummy Employees
IF NOT EXISTS (SELECT 1 FROM [itassettrack].[tbl_m_employee] WHERE NRP = 'EMP001')
BEGIN
    INSERT INTO [itassettrack].[tbl_m_employee]
        ([Id], [NRP], [FullName], [Email], [Phone], [SiteId], [DepartmentId], [DivisionId], [CreatedAt], [UpdatedAt])
    VALUES
        (NEWID(), 'EMP001', 'John Doe', 'john.doe@infrahub.local', '08123456789', '78F4BBA5-C3DC-4391-BD80-1A76ABB353A2', 'D797E15A-5F07-4807-B3E1-6AABCD476321', 'A5C3C623-1F42-4DCE-9F93-7FA4DFAFB4E6', GETDATE(), GETDATE()),
        (NEWID(), 'EMP002', 'Jane Smith', 'jane.smith@infrahub.local', '08123456780', '78F4BBA5-C3DC-4391-BD80-1A76ABB353A2', 'D797E15A-5F07-4807-B3E1-6AABCD476321', 'A5C3C623-1F42-4DCE-9F93-7FA4DFAFB4E6', GETDATE(), GETDATE());
END
GO

-- Seed Dummy Assets
IF NOT EXISTS (SELECT 1 FROM [itassettrack].[tbl_t_asset] WHERE SerialNumber = 'SN-LAPTOP-001')
BEGIN
    INSERT INTO [itassettrack].[tbl_t_asset]
        ([Id], [NoSN], [SerialNumber], [ComputerName], [Model], [OperatingSystem], [WindowsVersion], [Ram], [Storage], [Processor], [Gpu], [Status], [AssetType], [CreatedAt], [UpdatedAt])
    VALUES
        (NEWID(), 'SN-LAPTOP-001', 'SN-LAPTOP-001', 'LAPTOP-DEV-01', 'ThinkPad T14', 'Windows 11 Pro', '23H2', '16GB', '512GB SSD', 'Intel Core i7-12700H', 'Intel Iris Xe', 'Assigned', 'Laptop', GETDATE(), GETDATE()),
        (NEWID(), 'SN-LAPTOP-002', 'SN-LAPTOP-002', 'LAPTOP-DEV-02', 'ThinkPad P16', 'Windows 11 Pro', '23H2', '32GB', '1TB SSD', 'Intel Core i9-12900H', 'NVIDIA RTX A2000', 'Assigned', 'Laptop', GETDATE(), GETDATE());
END
GO
