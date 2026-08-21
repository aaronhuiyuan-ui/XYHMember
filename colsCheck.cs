using System;
using System.Data;
using System.Data.SqlClient;

class ColsCheck
{
    static void Main(string[] args)
    {
        var cs = "data source=172.68.1.11;initial catalog=hisdata;persist security info=True;uid=richhis;password=his123!@#;MultipleActiveResultSets=True";
        var tbl = args.Length > 0 ? args[0] : "门诊_收费明细表";
        try
        {
            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                var sql = "SELECT TOP 0 * FROM fghis5.." + tbl;
                using (var cmd = new SqlCommand(sql, conn))
                using (var rd = cmd.ExecuteReader(CommandBehavior.SchemaOnly))
                {
                    var dt = rd.GetSchemaTable();
                    Console.WriteLine("== " + tbl + " (" + dt.Rows.Count + " cols) ==");
                    foreach (DataRow r in dt.Rows)
                    {
                        var name = Convert.ToString(r["ColumnName"]);
                        var typeName = Convert.ToString(r["DataTypeName"]);
                        var size = r["ColumnSize"];
                        Console.WriteLine(string.Format("{0}  ({1} len={2})", name, typeName, size));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERR: " + ex.Message);
        }
    }
}
