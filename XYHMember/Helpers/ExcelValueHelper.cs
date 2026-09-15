using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace XYHMember
{
    public static class ExcelValueHelper
    {
        /// <summary>形如 12.00、-1.5000 的字符串，捕获小数点后的位数</summary>
        private static readonly Regex DecimalPattern = new Regex(@"^-?\d+\.(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// 写入单元格。ClosedXML 会把 "12.00" 这类字符串解析成数字，
        /// 末尾的 0 会被丢掉（页面显示 1.5000，导出变成 1.5）。
        /// 这里按原字符串的小数位数设置格式，保证导出与页面显示一致。
        /// </summary>
        public static void SetCellValue(IXLCell cell, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                cell.Value = value;
                return;
            }

            var match = DecimalPattern.Match(value);
            if (match.Success)
            {
                cell.Value = value;
                cell.Style.NumberFormat.Format = "0." + new string('0', match.Groups[1].Length);
                return;
            }

            cell.Value = value;
        }

        /// <summary>
        /// 写入数值单元格并指定显示格式。传入未四舍五入的原始值，
        /// 这样 Excel 整列求和的结果才与页面"先求和再取整"的合计一致
        /// （逐行先取整再相加会差几分钱）。
        /// </summary>
        public static void SetNumberCellValue(IXLCell cell, string value, string numberFormat)
        {
            cell.Value = value;
            cell.Style.NumberFormat.Format = numberFormat;
        }
    }
}
