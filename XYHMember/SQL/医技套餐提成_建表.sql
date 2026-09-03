/* ============================================================
   医技「套餐提成」—— 建表脚本
   目标库：FGHIS5（fghis5..）
   执行：sqlcmd -S 172.68.1.11 -U richhis -P 'his123!@#' -d FGHIS5 -f 65001 -i 医技套餐提成_建表.sql
   ============================================================ */

-- 套餐岗位提成比例（按 套餐名称+岗位 配置，登记时用登记行的套餐名称匹配）
CREATE TABLE fghis5..医技套餐提成比例表 (
    序号 INT IDENTITY(1,1) PRIMARY KEY,
    套餐名称 NVARCHAR(200) NOT NULL,     -- 登记的套餐名称，LTRIM(RTRIM) 匹配
    岗位 NVARCHAR(50) NOT NULL,          -- 医师 / 理疗师 / 护士
    提成比例 DECIMAL(10,2) NOT NULL,     -- 百分比，如 8 / 5
    CONSTRAINT UX_医技套餐提成_名称岗位 UNIQUE (套餐名称, 岗位)
);

-- 套餐提成明细（登记时按参与执行人逐人落一笔）
CREATE TABLE fghis5..医技提成明细表 (
    序号 INT IDENTITY(1,1) PRIMARY KEY,
    登记ID INT NOT NULL,                 -- 关联 医技登记表.登记ID
    执行人工号 NVARCHAR(20) NULL,
    执行人姓名 NVARCHAR(50) NULL,
    岗位 NVARCHAR(50) NULL,
    提成比例 DECIMAL(10,2) NULL,
    提成基数 DECIMAL(18,2) NULL,          -- 整单实际支付金额（追溯用）
    提成金额 DECIMAL(18,2) NULL,
    登记时间 DATETIME NULL
);
GO

-- 幂等/去重：同一登记同一执行人只一条（过滤索引需 QUOTED_IDENTIFIER ON）
SET QUOTED_IDENTIFIER ON;
GO
CREATE UNIQUE INDEX UX_医技提成明细_登记人 ON fghis5..医技提成明细表(登记ID, 执行人工号)
  WHERE 执行人工号 IS NOT NULL;
GO

PRINT '医技套餐提成表已创建';
