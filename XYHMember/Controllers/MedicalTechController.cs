using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    [AuthFilter]
    public class MedicalTechController : Controller
    {
        private XYHDbContext db = new XYHDbContext();

        // GET: /MedicalTech/Index
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 查询 HIS 已收费的诊疗项目，关联登记状态
        /// </summary>
        [HttpGet]
        public ActionResult GetQuery(string name, string bdate, string edate)
        {
            if (string.IsNullOrEmpty(bdate))
                bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate))
                edate = DateTime.Today.ToString("yyyy-MM-dd");

            try
            {
                var sql = @"SELECT a.结帐ID, a.门诊号, a.姓名, b.就诊ID, b.处方ID,
                                   CONVERT(varchar, b.日期, 23) AS 日期,
                                   CONVERT(varchar, b.时间, 8) AS 时间,
                                   b.项目名称, b.单价, b.数量, b.金额,
                                   r.登记ID, r.总次数,
                                   (SELECT COUNT(*) FROM fghis5..医技执行记录表 e WHERE e.登记ID = r.登记ID) AS 已执行次数
                            FROM fghis5..门诊_收费发票表 a
                            JOIN fghis5..门诊_收费明细表 b ON a.结帐ID = b.结帐ID
                            LEFT JOIN fghis5..医技登记表 r ON r.流水号 = CAST(a.结帐ID AS NVARCHAR) + '_' + CAST(b.处方ID AS NVARCHAR)
                                                 AND r.项目名称 = b.项目名称
                            WHERE a.发票状态 = '2'
                              AND b.项目类别 IN (6, 59)
                              AND b.日期 BETWEEN @bdate AND @edate
                              AND (@name = '' OR a.姓名 LIKE '%' + @name + '%' OR b.项目名称 LIKE '%' + @name + '%')
                            ORDER BY b.日期 DESC, b.时间 DESC";

                var result = db.Database.SqlQuery<MedicalTechChargeItem>(sql,
                    new SqlParameter("@name", (name ?? "").Trim()),
                    new SqlParameter("@bdate", QueryHelper.ParseDate(bdate)),
                    new SqlParameter("@edate", QueryHelper.ParseDate(edate))).ToList();

                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// 登记医技（新疗程）
        /// </summary>
        [HttpPost]
        public ActionResult Register(int 结帐ID, int 处方ID, int 就诊ID, string 病人姓名, int 门诊号, string 项目名称, int 总次数)
        {
            try
            {
                if (总次数 <= 0)
                    return Json(new { success = false, msg = "总次数必须大于0" });

                var 流水号 = 结帐ID + "_" + 处方ID;
                var 登记人工号 = GetCurrentJobNumber();

                var sql = @"INSERT INTO fghis5..医技登记表 (流水号, 门诊号, 就诊ID, 病人姓名, 项目名称, 总次数, 登记时间, 登记人工号)
                            VALUES (@流水号, @门诊号, @就诊ID, @病人姓名, @项目名称, @总次数, GETDATE(), @登记人工号);
                            SELECT CAST(SCOPE_IDENTITY() AS INT)";

                var 登记ID = db.Database.SqlQuery<int>(sql,
                    new SqlParameter("@流水号", 流水号),
                    new SqlParameter("@门诊号", 门诊号),
                    new SqlParameter("@就诊ID", 就诊ID),
                    new SqlParameter("@病人姓名", 病人姓名 ?? ""),
                    new SqlParameter("@项目名称", 项目名称 ?? ""),
                    new SqlParameter("@总次数", 总次数),
                    new SqlParameter("@登记人工号", 登记人工号 ?? "")
                ).FirstOrDefault();

                return Json(new { success = true, msg = "登记成功", 登记ID = 登记ID });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        /// <summary>
        /// 执行一次医技
        /// </summary>
        [HttpPost]
        public ActionResult Execute(int 登记ID, string 执行时间, string 岗位, string 备注)
        {
            try
            {
                if (登记ID <= 0)
                    return Json(new { success = false, msg = "登记ID无效" });

                // 查询登记信息（只需总次数）
                var totalSql = @"SELECT 总次数 FROM fghis5..医技登记表 WHERE 登记ID = @登记ID";
                var totalCount = db.Database.SqlQuery<int?>(totalSql,
                    new SqlParameter("@登记ID", 登记ID)).FirstOrDefault();

                if (totalCount == null)
                    return Json(new { success = false, msg = "登记记录不存在" });

                var 总次数 = totalCount.Value;

                // 获取当前最大执行次数
                var maxSql = @"SELECT ISNULL(MAX(本次次数), 0) FROM fghis5..医技执行记录表 WHERE 登记ID = @登记ID";
                var maxCount = db.Database.SqlQuery<int>(maxSql,
                    new SqlParameter("@登记ID", 登记ID)).FirstOrDefault();

                if (maxCount >= 总次数)
                    return Json(new { success = false, msg = "已到达总次数，无需再执行" });

                var jobNumber = GetCurrentJobNumber();
                var userName = GetCurrentUserName();

                // 解析执行时间
                DateTime parsedExecTime;
                if (!DateTime.TryParse(执行时间 ?? "", out parsedExecTime))
                    parsedExecTime = DateTime.Now;

                // 插入执行记录
                var execSql = @"INSERT INTO fghis5..医技执行记录表 (登记ID, 本次次数, 执行时间, 执行人工号, 执行人姓名, 岗位, 备注)
                                VALUES (@登记ID, @本次次数, @执行时间, @执行人工号, @执行人姓名, @岗位, @备注)";

                db.Database.ExecuteSqlCommand(execSql,
                    new SqlParameter("@登记ID", 登记ID),
                    new SqlParameter("@本次次数", maxCount + 1),
                    new SqlParameter("@执行时间", parsedExecTime),
                    new SqlParameter("@执行人工号", jobNumber ?? ""),
                    new SqlParameter("@执行人姓名", userName ?? ""),
                    new SqlParameter("@岗位", 岗位 ?? ""),
                    new SqlParameter("@备注", 备注 ?? ""));

                var isCompleted = (maxCount + 1) >= 总次数;

                return Json(new
                {
                    success = true,
                    msg = "执行成功",
                    本次次数 = maxCount + 1,
                    总次数 = 总次数,
                    已完成 = isCompleted
                });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        /// <summary>
        /// 查看执行历史
        /// </summary>
        [HttpGet]
        public ActionResult GetHistory(int 登记ID)
        {
            try
            {
                var regSql = @"SELECT 登记ID, 流水号, 门诊号, 就诊ID, 病人姓名, 项目名称, 总次数, 登记时间, 登记人工号
                                FROM fghis5..医技登记表 WHERE 登记ID = @登记ID";
                var reg = db.Database.SqlQuery<MedicalTechRegistration>(regSql,
                    new SqlParameter("@登记ID", 登记ID)).FirstOrDefault();

                if (reg == null)
                    return Json(new { success = false, msg = "登记记录不存在" }, JsonRequestBehavior.AllowGet);

                var execSql = @"SELECT 执行ID, 登记ID, 本次次数, 执行时间, 执行人工号, 执行人姓名, 岗位, 备注
                                FROM fghis5..医技执行记录表
                                WHERE 登记ID = @登记ID
                                ORDER BY 本次次数 ASC";

                var records = db.Database.SqlQuery<MedicalTechExecution>(execSql,
                    new SqlParameter("@登记ID", 登记ID)).ToList();

                // 格式化执行时间，避免 JSON 返回 /Date(ticks)/
                var formattedRecords = records.Select(r => new
                {
                    r.执行ID,
                    r.登记ID,
                    r.本次次数,
                    执行时间 = r.执行时间?.ToString("yyyy-MM-dd HH:mm"),
                    r.执行人工号,
                    r.执行人姓名,
                    r.岗位,
                    r.备注
                }).ToList();

                return Json(new
                {
                    success = true,
                    病人姓名 = reg.病人姓名,
                    项目名称 = reg.项目名称,
                    总次数 = reg.总次数,
                    已执行次数 = records.Count,
                    records = formattedRecords
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// 获取当前登录用户信息（供执行弹窗使用）
        /// </summary>
        [HttpGet]
        public ActionResult GetCurrentUser()
        {
            var userId = ((int?)Session["UserId"]) ?? 1;
            var user = db.Users.Find(userId);
            return Json(new
            {
                jobNumber = user?.JobNumber ?? "",
                userName = user?.Name ?? ""
            }, JsonRequestBehavior.AllowGet);
        }

        private string GetCurrentJobNumber()
        {
            var userId = ((int?)Session["UserId"]) ?? 1;
            var user = db.Users.Find(userId);
            return user?.JobNumber ?? "";
        }

        private string GetCurrentUserName()
        {
            var userId = ((int?)Session["UserId"]) ?? 1;
            var user = db.Users.Find(userId);
            return user?.Name ?? "";
        }
    }
}
