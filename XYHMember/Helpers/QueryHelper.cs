using System.Data.SqlClient;

namespace XYHMember
{
    public static class QueryHelper
    {
        /// <summary>
        /// 将 yyyy-MM-dd 格式转为 yyyyMMdd
        /// </summary>
        public static string ParseDate(string datepickerValue)
        {
            return datepickerValue.Substring(0, 4)
                 + datepickerValue.Substring(5, 2)
                 + datepickerValue.Substring(8, 2);
        }

        /// <summary>
        /// 为 name / bdate / edate 创建 SqlParameter 数组
        /// </summary>
        public static SqlParameter[] BuildReportParams(string name, string bdate, string edate)
        {
            return new[]
            {
                new SqlParameter("@name", (name ?? "").Trim()),
                new SqlParameter("@bdate", ParseDate(bdate)),
                new SqlParameter("@edate", ParseDate(edate))
            };
        }
    }
}
