/* ============================================================
   耗材出库单：把「来源标识」的筛选唯一索引换成普通唯一索引
   目标库：FGHIS5（fghis5..）
   ============================================================
   背景：耗材出库单 上的筛选索引
           UX_耗材出库单_来源标识 UNIQUE (来源标识) WHERE 来源标识 IS NOT NULL
        会让「对该表的任何 INSERT/UPDATE/DELETE」都要求会话 ARITHABORT ON。
        ADO.NET/SqlClient 的连接默认 ARITHABORT OFF（SSMS 是 ON，所以在
        SSMS 里手工执行同样的 SQL 不报错），程序里写入则一律报：
           1934 INSERT 失败，因为下列 SET 选项的设置不正确: ARITHABORT
        而且是对全表生效的准入检查，跟插入的那行的值（哪怕 来源标识 为
        NULL、根本不进这个索引）无关。

   做法：来源标识 改成 NOT NULL，普通唯一索引照样保证幂等，但不再要求
        ARITHABORT —— 之后任何客户端/工具写这张表都不会再报 1934。
        手工出库由程序写入 出库单号；套餐自动扣减/退费回冲本来就写
        结帐ID_处方ID_套餐名称 / 回冲_... ，不受影响。

   ★ 执行顺序：先把新版本程序发布上线，再执行本脚本。
     旧程序 INSERT 不写 来源标识，列改成 NOT NULL 后会报
     「不能将 NULL 值插入列 '来源标识'」。
     反过来先发程序的这段时间，手工出库仍会报原来的 1934（与现状相同，
     不是新程序没生效）——脚本一执行即恢复。

   用法：以 richhis（或更高权限）连接 172.68.1.11 / hisdata，
   选中 fghis5 执行本脚本。可重复执行。
   ============================================================ */

-- 0) 前置检查：来源标识 有重复时唯一索引建不起来，先报清楚
IF EXISTS (SELECT 1 FROM fghis5..耗材出库单 WHERE 来源标识 IS NOT NULL
           GROUP BY 来源标识 HAVING COUNT(*) > 1)
BEGIN
    RAISERROR(N'耗材出库单 存在重复的 来源标识，请先处理重复行再执行本脚本。', 16, 1);
    RETURN;
END
GO

-- 1) 回填历史空值（正常情况下表里没有这样的行；手工出库即「单号」）
UPDATE fghis5..耗材出库单 SET 来源标识 = 出库单号 WHERE 来源标识 IS NULL;
GO

-- 2) 先删旧索引（改造 来源标识 列之前，挂在它上面的索引必须先去掉）
IF EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
           WHERE t.name = N'耗材出库单' AND i.name = N'UX_耗材出库单_来源标识')
    DROP INDEX UX_耗材出库单_来源标识 ON fghis5..耗材出库单;
GO

-- 3) 来源标识 改 NOT NULL
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
           WHERE t.name = N'耗材出库单' AND c.name = N'来源标识' AND c.is_nullable = 1)
    ALTER TABLE fghis5..耗材出库单 ALTER COLUMN 来源标识 NVARCHAR(200) NOT NULL;
GO

-- 4) 普通唯一索引（不带 WHERE，故不触发 ARITHABORT 要求）
IF NOT EXISTS (SELECT 1 FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
               WHERE t.name = N'耗材出库单' AND i.name = N'UX_耗材出库单_来源标识')
    CREATE UNIQUE INDEX UX_耗材出库单_来源标识 ON fghis5..耗材出库单(来源标识);
GO
