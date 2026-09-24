-- 医技登记表：新增「是否结束」。界面上的「结束」按钮把它置为 't'，置了之后该登记不能再执行。
-- 旧数据默认为未结束。本脚本必须用 sqlcmd -f 65001 执行（含中文标识符）；可重复执行。
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'fghis5..医技登记表') AND name = N'是否结束')
BEGIN
    ALTER TABLE fghis5..医技登记表
        ADD 是否结束 CHAR(1) NOT NULL CONSTRAINT DF_医技登记表_是否结束 DEFAULT 'f';
    PRINT '已新增列 是否结束';
END
ELSE
    PRINT '是否结束 列已存在';
GO
