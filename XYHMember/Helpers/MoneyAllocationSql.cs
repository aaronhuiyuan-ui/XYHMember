using System.Globalization;

namespace XYHMember
{
    /// <summary>
    /// 生成「按单据分摊 + 平衡到最小单位」的 SQL 表达式。
    /// 分摊原始值本身带小数，逐行取整后相加会和单据总额差几分钱，
    /// 所以用最大余数法：取整产生的差额按余数从大到小逐单位派发（差额为负则从余数最小的行扣回）。
    /// 结果是每行与真实分摊值最多差一个最小单位，且同一单据内各行合计 = 该单分摊总额。
    /// </summary>
    public static class MoneyAllocationSql
    {
        /// <param name="shareExpr">分摊原始值表达式。会被多次引用，建议先用 CROSS APPLY 起个短别名</param>
        /// <param name="partitionExpr">单据分组键</param>
        /// <param name="tieBreakExpr">余数相同时的次级排序，保证结果可重复</param>
        /// <param name="scale">最小单位的小数位数，2 = 分，4 = 万分之一</param>
        public static string Balanced(string shareExpr, string partitionExpr, string tieBreakExpr, int scale)
        {
            var rounded = "ROUND(" + shareExpr + ", " + scale + ")";
            var roundedSum = "SUM(" + rounded + ") OVER (PARTITION BY " + partitionExpr + ")";
            var target = "ROUND(SUM(" + shareExpr + ") OVER (PARTITION BY " + partitionExpr + "), " + scale + ")";
            var residual = "(" + target + " - " + roundedSum + ")";
            var units = "ROUND(" + residual + " * " + Pow10(scale) + ", 0)";
            var remainder = "(" + shareExpr + " - " + rounded + ")";
            var unit = UnitLiteral(scale);
            var rowNumber = "ROW_NUMBER() OVER (PARTITION BY " + partitionExpr + " ORDER BY ";

            return rounded
                 + "\n         + CASE WHEN " + residual + " > 0 AND " + rowNumber + remainder + " DESC, " + tieBreakExpr + ") <= " + units
                 + "\n                 THEN " + unit
                 + "\n            WHEN " + residual + " < 0 AND " + rowNumber + remainder + " ASC, " + tieBreakExpr + ") <= -" + units
                 + "\n                 THEN -" + unit
                 + "\n            ELSE 0 END";
        }

        private static string Pow10(int scale)
        {
            long pow = 1;
            for (var i = 0; i < scale; i++) pow *= 10;
            return pow.ToString(CultureInfo.InvariantCulture);
        }

        private static string UnitLiteral(int scale)
        {
            return scale <= 0 ? "1" : "0." + new string('0', scale - 1) + "1";
        }
    }
}
