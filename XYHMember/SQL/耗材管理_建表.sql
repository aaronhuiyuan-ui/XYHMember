/* ============================================================
   耗材管理模块 建表脚本
   库：hisdata   架构：fghis5
   ============================================================
   用法：在 SQL Server Management Studio 里以 richhis（或更高权限）
   连接 172.68.1.11 / hisdata，选中 fghis5 执行本脚本。
   ============================================================ */

-- 1) 耗材入库表（本地，审核落库用）
IF OBJECT_ID('fghis5..耗材入库表') IS NOT NULL
    DROP TABLE fghis5..耗材入库表;
GO
CREATE TABLE fghis5..耗材入库表 (
    序号       INT IDENTITY(1,1) PRIMARY KEY,
    入库日期   DATETIME      NULL,            -- 金蝶EAS 入库日期
    单号       NVARCHAR(50)  NULL,            -- 金蝶EAS 单据编号
    仓库       NVARCHAR(100) NULL,
    物料编码   NVARCHAR(100) NULL,
    物料名称   NVARCHAR(200) NULL,
    规格       NVARCHAR(200) NULL,
    产地编码   NVARCHAR(100) NULL,
    产地名称   NVARCHAR(100) NULL,
    批号       NVARCHAR(100) NULL,
    有效期     DATETIME      NULL,
    单位       NVARCHAR(50)  NULL,
    数量       DECIMAL(18,3) NULL,
    入库人     NVARCHAR(50)  NULL,
    物料类别   NVARCHAR(100) NULL,
    审核时间   DATETIME      NULL,
    审核人     NVARCHAR(50)  NULL,
    状态       NVARCHAR(20)  NOT NULL DEFAULT '已审核',
    剩余数量   DECIMAL(18,3) NULL,            -- 初始 = 数量，出库时扣减
    唯一键     NVARCHAR(300) NOT NULL,        -- 单号|物料编码|批号（去重键）
    CONSTRAINT UX_耗材入库_唯一键 UNIQUE (唯一键)
);
GO

-- 2) 耗材出库单（主表，一单多条）
IF OBJECT_ID('fghis5..耗材出库单') IS NOT NULL
    DROP TABLE fghis5..耗材出库单;
GO
CREATE TABLE fghis5..耗材出库单 (
    出库单号     NVARCHAR(50)  PRIMARY KEY,
    出库日期     DATETIME      NULL,
    领用人       NVARCHAR(50)  NULL,
    发料人签字   NVARCHAR(50)  NULL,
    登记人       NVARCHAR(50)  NULL,
    登记时间     DATETIME      NOT NULL DEFAULT GETDATE(),
    备注         NVARCHAR(500) NULL
);
GO

-- 3) 耗材出库明细（从表）
IF OBJECT_ID('fghis5..耗材出库明细') IS NOT NULL
    DROP TABLE fghis5..耗材出库明细;
GO
CREATE TABLE fghis5..耗材出库明细 (
    序号         INT IDENTITY(1,1) PRIMARY KEY,
    出库单号     NVARCHAR(50)  NOT NULL,
    关联入库序号 INT           NULL,          -- 关联 耗材入库表.序号
    物料编码     NVARCHAR(100) NULL,
    耗材名称     NVARCHAR(200) NULL,
    规格型号     NVARCHAR(200) NULL,
    单位         NVARCHAR(50)  NULL,
    批号         NVARCHAR(100) NULL,
    领用数量     DECIMAL(18,3) NULL,
    申领日期     DATETIME      NULL,
    到库日期     DATETIME      NULL,
    保质期       DATETIME      NULL
);
GO
