-- 医技登记表：新增整单默认执行人（执行人工号/执行人姓名/执行人岗位），可空
-- 说明：登记界面直接选执行人后写入；旧数据该三列为 NULL，登记时未指定岗位则提成仍走原逻辑
-- 本脚本必须用 sqlcmd -f 65001 执行（含中文标识符）；可重复执行
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'fghis5..医技登记表') AND name = N'执行人工号')
BEGIN
    ALTER TABLE fghis5..医技登记表 ADD 执行人工号 NVARCHAR(50) NULL;
    PRINT '已新增列 执行人工号';
END
ELSE
    PRINT '执行人工号 列已存在';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'fghis5..医技登记表') AND name = N'执行人姓名')
BEGIN
    ALTER TABLE fghis5..医技登记表 ADD 执行人姓名 NVARCHAR(50) NULL;
    PRINT '已新增列 执行人姓名';
END
ELSE
    PRINT '执行人姓名 列已存在';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'fghis5..医技登记表') AND name = N'执行人岗位')
BEGIN
    ALTER TABLE fghis5..医技登记表 ADD 执行人岗位 NVARCHAR(50) NULL;
    PRINT '已新增列 执行人岗位';
END
ELSE
    PRINT '执行人岗位 列已存在';
GO
