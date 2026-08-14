-- =====================================================
-- 上海真仁堂统计汇总（加工费及快递费汇总 → 导入到OA）
-- 库：fghis5
-- 字段：序号(自增)、月份(yyyy-MM，唯一)、开始日期、结束日期、
--       应付加工费、应付快递费、应付总金额(=加工费+快递费)、导入时间
-- =====================================================
IF OBJECT_ID('fghis5.dbo.上海真仁堂统计汇总') IS NULL
BEGIN
    CREATE TABLE fghis5.dbo.上海真仁堂统计汇总 (
        序号         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        月份         NVARCHAR(7)  NOT NULL,             -- yyyy-MM
        开始日期      NVARCHAR(10) NULL,                 -- yyyy-MM-dd
        结束日期      NVARCHAR(10) NULL,                 -- yyyy-MM-dd
        应付加工费    DECIMAL(18,2) NOT NULL DEFAULT 0,
        应付快递费    DECIMAL(18,2) NOT NULL DEFAULT 0,
        应付总金额    DECIMAL(18,2) NULL,                -- = 应付加工费 + 应付快递费
        导入时间      DATETIME    NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_上海真仁堂统计汇总_月份 UNIQUE (月份)
    );
END
GO

-- 若在旧表上迁移，用下面的语句补列并回填历史数据：
-- ALTER TABLE fghis5.dbo.上海真仁堂统计汇总 ADD 开始日期 NVARCHAR(10) NULL, 结束日期 NVARCHAR(10) NULL, 应付总金额 DECIMAL(18,2) NULL;
-- UPDATE fghis5.dbo.上海真仁堂统计汇总
--    SET 开始日期   = 月份 + '-01',
--        结束日期   = CONVERT(varchar(10), DATEADD(day, -1, DATEADD(month, 1, 月份 + '-01')), 23),
--        应付总金额 = ISNULL(应付加工费, 0) + ISNULL(应付快递费, 0)
--   WHERE 开始日期 IS NULL OR 结束日期 IS NULL OR 应付总金额 IS NULL;
GO
