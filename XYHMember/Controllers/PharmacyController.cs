using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    [AuthFilter]
    public class PharmacyController : Controller
    {
        private XYHDbContext db = new XYHDbContext();

        //门诊发药查询页面
        public ActionResult DispensingQuery()
        {
            return View();
        }

        [HttpGet]
        public ActionResult GetDispensingQuery()
        {
            var name = Request["name"]?.Trim() ?? "";
            var bdate = Request["bdatepicker"]?.Trim();
            var edate = Request["edatepicker"]?.Trim();

            string sqlQuery = @"
SELECT c.结帐ID, a.处方ID, a.门诊号, a.姓名, a.开方日期, a.开方时间,
       a.医生工号, d.医生姓名, a.草药帖数, a.总金额,
       CASE WHEN c.发票状态=2 THEN N'已收费' ELSE N'已退费' END AS 发票状态
FROM fghis5..医生_处方流水帐 a
JOIN fghis5..门诊_收费处方表 b ON a.处方ID=b.处方ID
JOIN fghis5..门诊_收费发票表 c ON b.结帐ID=c.结帐ID
JOIN fghis5..系统_医生信息表 d ON d.医生工号=a.医生工号
WHERE a.处方类型='2' AND a.状态='3'
  AND c.发票状态='2'
  AND a.处方ID NOT IN (SELECT DISTINCT 处方ID FROM fghis5..门诊_发药信息表)
  AND a.开方日期 BETWEEN @bdate AND @edate
  AND (@name = '' OR a.姓名 = @name)
ORDER BY c.结帐ID DESC, a.处方ID DESC";

            var result = db.Database.SqlQuery<PharmacyDispensing>(sqlQuery,
                QueryHelper.BuildReportParams(name, bdate, edate)).ToList();

            return View("DispensingQuery", result);
        }

        //发药
        [HttpPost]
        public ActionResult Dispense()
        {
            var input = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
            var req = JObject.Parse(input);
            var prescriptionIds = req["prescriptionIds"]?.ToObject<List<string>>();
            var checkoutIds = req["checkoutIds"]?.ToObject<List<string>>();

            if (prescriptionIds == null || prescriptionIds.Count == 0)
                return Json(new { success = false, msg = "未选择处方" });

            var userId = ((int?)Session["UserId"]) ?? 1;

            // 查询当前操作员的工号和姓名
            var user = db.Users.Find(userId);
            var jobNumber = user?.JobNumber ?? "";
            var userName = user?.Name ?? "";

            try
            {
                var count = 0;
                for (int i = 0; i < prescriptionIds.Count; i++)
                {
                    var sql = @"INSERT INTO fghis5..门诊_发药信息表 (处方ID, 发药人工号, 发药人姓名, 发药时间, 发药状态, delete_flag)
                                VALUES (@处方ID, @发药人工号, @发药人姓名, GETDATE(), 2, 0)";

                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@处方ID", prescriptionIds[i]),
                        new SqlParameter("@发药人工号", jobNumber),
                        new SqlParameter("@发药人姓名", userName));
                    count++;
                }

                return Json(new { success = true, msg = $"成功发药{count}条" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }
    }
}
