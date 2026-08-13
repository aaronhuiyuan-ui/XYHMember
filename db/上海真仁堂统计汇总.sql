-- =====================================================
-- 上海真仁堂统计汇总（加工费及快递费汇总 → 导入到OA）
-- 库：fghis5
-- 字段：序号(自增)、月份(yyyy-MM，唯一)、应付加工费、应付快递费、导入时间
-- =====================================================
IF OBJECT_ID('fghis5.dbo.上海真仁堂统计汇总') IS NULL
BEGIN
    CREATE TABLE fghis5.dbo.上海真仁堂统计汇总 (
        序号         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        月份         NVARCHAR(7)  NOT NULL,             -- yyyy-MM
        应付加工费    DECIMAL(18,2) NOT NULL DEFAULT 0,
        应付快递费    DECIMAL(18,2) NOT NULL DEFAULT 0,
        导入时间      DATETIME    NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UQ_上海真仁堂统计汇总_月份 UNIQUE (月份)
    );
END
GO
