-- 医技登记表.提成金额 精度 2 位小数 → 4 位小数
-- 背景：套餐提成按各明细项目分摊计算，保留 4 位可使分项合计与整单计提额精确对账
-- 说明：仅放宽列精度，不改动任何数据；可重复执行
-- 本脚本必须用 sqlcmd -f 65001 执行（含中文标识符）
SET NOCOUNT ON;

ALTER TABLE fghis5..医技登记表 ALTER COLUMN 提成金额 DECIMAL(18,4) NULL;
PRINT '提成金额 已设为 DECIMAL(18,4)';
GO
