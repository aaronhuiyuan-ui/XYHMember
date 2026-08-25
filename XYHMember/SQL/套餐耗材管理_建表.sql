/* ============================================================
   套餐耗材维护 + 每日自动扣减库存 —— 建表脚本
   目标库：FGHIS5（fghis5..）
   执行：sqlcmd -S 172.68.1.11 -U richhis -P 'his123!@#' -d FGHIS5 -f 65001 -i 套餐耗材管理_建表.sql
   ============================================================ */

CREATE TABLE fghis5..套餐表 (
    序号 INT IDENTITY(1,1) PRIMARY KEY,
    套餐名称 NVARCHAR(255) NOT NULL,
    备注 NVARCHAR(500) NULL,
    CONSTRAINT UX_套餐表_名称 UNIQUE (套餐名称)
);

CREATE TABLE fghis5..套餐耗材明细 (
    序号 INT IDENTITY(1,1) PRIMARY KEY,
    套餐ID INT NOT NULL,                    -- 关联 套餐表.序号
    物料编码 NVARCHAR(100) NULL,
    耗材名称 NVARCHAR(200) NULL,
    规格型号 NVARCHAR(200) NULL,
    单位 NVARCHAR(50) NULL,
    数量 DECIMAL(18,3) NOT NULL DEFAULT 0   -- 开一次套餐消耗该耗材的总量
);

ALTER TABLE fghis5..耗材出库单 ADD 来源类型 NVARCHAR(20) NOT NULL DEFAULT '手工出库';
ALTER TABLE fghis5..耗材出库单 ADD 来源标识 NVARCHAR(200) NULL;
GO

-- 幂等：同一套餐使用事件只扣一次
CREATE UNIQUE INDEX UX_耗材出库单_来源标识 ON fghis5..耗材出库单(来源标识) WHERE 来源标识 IS NOT NULL;
GO
