/* ============================================================
   抵扣券管理 —— 建表脚本
   目标库：hisdata（hisdata.dbo）
   执行：sqlcmd -S 172.68.1.11 -U richhis -P 'his123!@#' -d hisdata -f 65001 -i 抵扣券管理_建表.sql
   说明：
     1. 350 元锁客券独立记账，不写入储值卡 MS_BRZH / MS_SZMX。
     2. 可重复执行。
     3. 不建筛选索引、不建索引计算列（本库连接默认 ARITHABORT OFF，会触发 1934）。
   ============================================================ */
SET NOCOUNT ON;
GO

/* ---------- 抵扣券主表 ---------- */
IF OBJECT_ID(N'hisdata.dbo.抵扣券表', N'U') IS NULL
BEGIN
    CREATE TABLE hisdata.dbo.抵扣券表
    (
        序号        INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_抵扣券表 PRIMARY KEY,
        券码        VARCHAR(20)      NOT NULL,
        PID         INT              NOT NULL,
        门诊号      VARCHAR(12)      NOT NULL,
        姓名        NVARCHAR(50)     NULL,
        手机        VARCHAR(20)      NULL,
        券面额      DECIMAL(18,2)    NOT NULL
            CONSTRAINT DF_抵扣券表_券面额 DEFAULT (350),
        已用金额    DECIMAL(18,2)    NOT NULL
            CONSTRAINT DF_抵扣券表_已用金额 DEFAULT (0),
        状态        NVARCHAR(10)     NOT NULL
            CONSTRAINT DF_抵扣券表_状态 DEFAULT (N'未使用'),
        支付方式    NVARCHAR(20)     NULL,
        发券工号    VARCHAR(12)      NULL,
        发券时间    DATETIME         NOT NULL
            CONSTRAINT DF_抵扣券表_发券时间 DEFAULT (GETDATE()),
        作废工号    VARCHAR(12)      NULL,
        作废时间    DATETIME         NULL,
        备注        NVARCHAR(200)    NULL,
        CONSTRAINT CK_抵扣券表_状态 CHECK (状态 IN (N'未使用', N'已使用', N'已作废'))
    );
    PRINT '已创建 抵扣券表';
END
ELSE
    PRINT '抵扣券表 已存在';
GO

/* 已有旧表时，把券面额默认值统一为 350 */
DECLARE @df sysname;
DECLARE @sql nvarchar(max);

SELECT @df = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c
  ON dc.parent_object_id = c.object_id
 AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'hisdata.dbo.抵扣券表')
  AND c.name = N'券面额'
  AND dc.definition NOT LIKE N'%350%';

IF @df IS NOT NULL
BEGIN
    SET @sql = N'ALTER TABLE hisdata.dbo.抵扣券表 DROP CONSTRAINT ' + QUOTENAME(@df);
    EXEC sp_executesql @sql;

    ALTER TABLE hisdata.dbo.抵扣券表
        ADD CONSTRAINT DF_抵扣券表_券面额 DEFAULT (350) FOR 券面额;
    PRINT '已将券面额默认值更新为 350';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UX_抵扣券表_券码'
                 AND object_id = OBJECT_ID(N'hisdata.dbo.抵扣券表'))
    CREATE UNIQUE INDEX UX_抵扣券表_券码 ON hisdata.dbo.抵扣券表(券码);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_抵扣券表_PID_状态'
                 AND object_id = OBJECT_ID(N'hisdata.dbo.抵扣券表'))
    CREATE INDEX IX_抵扣券表_PID_状态 ON hisdata.dbo.抵扣券表(PID, 状态);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_抵扣券表_发券时间'
                 AND object_id = OBJECT_ID(N'hisdata.dbo.抵扣券表'))
    CREATE INDEX IX_抵扣券表_发券时间 ON hisdata.dbo.抵扣券表(发券时间);
GO

/* ---------- 抵扣券流水表 ---------- */
IF OBJECT_ID(N'hisdata.dbo.抵扣券流水表', N'U') IS NULL
BEGIN
    CREATE TABLE hisdata.dbo.抵扣券流水表
    (
        流水号      INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_抵扣券流水表 PRIMARY KEY,
        券序号      INT              NOT NULL,
        类型        NVARCHAR(10)     NOT NULL,
        发生金额    DECIMAL(18,2)    NOT NULL,
        券余额      DECIMAL(18,2)    NOT NULL,
        操作工号    VARCHAR(12)      NULL,
        操作时间    DATETIME         NOT NULL
            CONSTRAINT DF_抵扣券流水表_操作时间 DEFAULT (GETDATE()),
        备注        NVARCHAR(200)    NULL,
        关联结帐ID  INT              NULL,       -- 预留：将来核销时记录 HIS 结帐ID
        关联发票号  VARCHAR(20)      NULL,       -- 预留：将来核销时记录发票号
        CONSTRAINT FK_抵扣券流水_抵扣券 FOREIGN KEY (券序号)
            REFERENCES dbo.抵扣券表(序号)
    );
    PRINT '已创建 抵扣券流水表';
END
ELSE
    PRINT '抵扣券流水表 已存在';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_抵扣券流水表_券序号'
                 AND object_id = OBJECT_ID(N'hisdata.dbo.抵扣券流水表'))
    CREATE INDEX IX_抵扣券流水表_券序号 ON hisdata.dbo.抵扣券流水表(券序号);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_抵扣券流水表_操作时间'
                 AND object_id = OBJECT_ID(N'hisdata.dbo.抵扣券流水表'))
    CREATE INDEX IX_抵扣券流水表_操作时间 ON hisdata.dbo.抵扣券流水表(操作时间);
GO

PRINT '抵扣券建表脚本执行完成';
GO
