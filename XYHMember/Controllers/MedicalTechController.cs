using ClosedXML.Excel;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
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
            try
            {
                var result = QueryChargeItems(name, bdate, edate);
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
        /// 查询 HIS 已收费的诊疗项目（含登记/执行汇总），供页面查询与导出复用
        /// </summary>
        private List<MedicalTechChargeItem> QueryChargeItems(string name, string bdate, string edate)
        {
            if (string.IsNullOrEmpty(bdate))
                bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate))
                edate = DateTime.Today.ToString("yyyy-MM-dd");

            var sql = @"WITH 支付汇总 AS (
                    SELECT 结帐ID, CAST(SUM(支付金额) AS DECIMAL(28,10)) AS 实收金额
                    FROM fghis5..门诊_收费支付表
                    WHERE 支付方式 != '6'
                    GROUP BY 结帐ID
                ),
                执行汇总 AS (
                    SELECT 登记ID, COUNT(*) AS 已执行次数
                    FROM fghis5..医技执行记录表
                    WHERE delete_flag = 'f'
                    GROUP BY 登记ID
                ),
                执行人汇总 AS (
                    SELECT e.登记ID,
                           STUFF((SELECT DISTINCT ';' + e2.执行人姓名
                                  FROM fghis5..医技执行记录表 e2
                                  WHERE e2.登记ID = e.登记ID AND e2.delete_flag = 'f'
                                  FOR XML PATH('')), 1, 1, '') AS 执行人
                    FROM fghis5..医技执行记录表 e
                    WHERE e.delete_flag = 'f'
                    GROUP BY e.登记ID
                )
                SELECT a.结帐ID, a.门诊号, a.姓名, b.就诊ID, b.处方ID,
                       CONVERT(varchar, b.日期, 23) AS 日期,
                       CONVERT(varchar, b.时间, 8) AS 时间,
                       b.套餐名称, b.项目ID, b.项目名称, b.单价, b.数量, b.金额,
                       ISNULL(p.实收金额 * b.金额 / NULLIF(a.总金额, 0), 0) AS 实收金额,
                       -- 执行人：同一登记下的去重执行人，多个用分号隔开
                       ISNULL(pe.执行人, '') AS 执行人,
                       -- 提成金额：项目金额 × 提成比例（不依赖登记与完成状态，实时按当前比例计算）
                       ROUND(ISNULL(b.金额, 0) * ISNULL(c.提成比例, 0) / 100.0, 2) AS 提成金额,
                       r.登记ID, r.总次数,
                       ISNULL(e.已执行次数, 0) AS 已执行次数
                FROM fghis5..门诊_收费发票表 a
                JOIN fghis5..门诊_收费明细表 b ON a.结帐ID = b.结帐ID
                LEFT JOIN fghis5..医技登记表 r ON r.流水号 = CAST(a.结帐ID AS NVARCHAR) + '_' + CAST(b.处方ID AS NVARCHAR)
                    AND r.项目名称 = b.项目名称
                LEFT JOIN 支付汇总 p ON p.结帐ID = a.结帐ID
                LEFT JOIN 执行汇总 e ON e.登记ID = r.登记ID
                LEFT JOIN 执行人汇总 pe ON pe.登记ID = r.登记ID
                LEFT JOIN (SELECT 项目名称, MAX(提成比例) AS 提成比例
                           FROM fghis5..医技项目操作人员提成表
                           GROUP BY 项目名称) c ON c.项目名称 = b.项目名称
                WHERE a.发票状态 = '2'
                  AND b.项目类别 IN (6, 59)
                  AND b.日期 BETWEEN @bdate AND @edate
                  AND (@name = '' OR a.姓名 LIKE '%' + @name + '%' OR b.项目名称 LIKE '%' + @name + '%')
                ORDER BY b.日期 DESC, b.时间 DESC, b.套餐名称";

            return db.Database.SqlQuery<MedicalTechChargeItem>(sql,
                new SqlParameter("@name", (name ?? "").Trim()),
                new SqlParameter("@bdate", QueryHelper.ParseDate(bdate)),
                new SqlParameter("@edate", QueryHelper.ParseDate(edate))).ToList();
        }

        /// <summary>
        /// 导出医技登记与执行（含执行明细，一对多：主行 + 明细子行）
        /// POST /MedicalTech/ExportToExcel
        /// </summary>
        [HttpPost]
        public ActionResult ExportToExcel(string bdate, string edate, string name, string 项目名称, string 状态)
        {
            try
            {
                var items = QueryChargeItems(name, bdate, edate);

                // 应用表头筛选（与页面筛选一致）
                if (!string.IsNullOrEmpty(项目名称))
                    items = items.Where(i => i.项目名称 == 项目名称).ToList();
                if (!string.IsNullOrEmpty(状态))
                    items = items.Where(i => GetStatus(i) == 状态).ToList();

                // 批量取执行明细（一次性查所有已登记项）
                var regIds = items.Where(i => i.登记ID.HasValue).Select(i => i.登记ID.Value).Distinct().ToList();
                var execMap = new Dictionary<int, List<MedicalTechExecution>>();
                if (regIds.Count > 0)
                {
                    var idList = string.Join(",", regIds);
                    var execSql = @"SELECT 执行ID, 登记ID, 本次次数, 执行时间, 执行人工号, 执行人姓名, 岗位, 备注, delete_flag
                                    FROM fghis5..医技执行记录表
                                    WHERE delete_flag = 'f' AND 登记ID IN (" + idList + @")
                                    ORDER BY 登记ID, 本次次数";
                    var execs = db.Database.SqlQuery<MedicalTechExecution>(execSql).ToList();
                    execMap = execs.GroupBy(e => e.登记ID).ToDictionary(g => g.Key, g => g.ToList());
                }

                // 表头：主列13 + 执行明细列6
                var headers = new List<string>
                {
                    "门诊号", "姓名", "套餐名称", "项目ID", "项目名称", "数量", "项目金额", "实收金额", "收费日期",
                    "状态", "执行进度", "已执行金额", "未执行金额", "执行人", "操作人员提成",
                    "执行次数", "执行时间", "执行人工号", "执行人姓名", "岗位", "备注"
                };
                int mainCols = 15;

                var rows = new List<List<string>>();
                foreach (var d in items)
                {
                    var 项目金额 = d.金额 ?? 0m;
                    var 实收金额 = d.实收金额 ?? 项目金额;
                    var 已执行金额 = 0m;
                    if (d.登记ID.HasValue && (d.总次数 ?? 0) > 0 && (d.已执行次数 ?? 0) > 0)
                        已执行金额 = Math.Round(实收金额 / d.总次数.Value * d.已执行次数.Value, 2);
                    var 未执行金额 = 实收金额 - 已执行金额;

                    var progress = d.登记ID.HasValue ? (d.已执行次数 + "/" + d.总次数 + "次") : "-";

                    // 主行（补足执行明细列，保持与表头同宽 19 列）
                    var mainRow = new List<string>
                    {
                        d.门诊号?.ToString() ?? "",
                        d.姓名 ?? "",
                        d.套餐名称 ?? "",
                        d.项目ID?.ToString() ?? "",
                        d.项目名称 ?? "",
                        d.数量?.ToString("G29") ?? "",
                        d.金额?.ToString("F2") ?? "",
                        实收金额.ToString("F2"),
                        d.日期 ?? "",
                        GetStatus(d),
                        progress,
                        已执行金额.ToString("F2"),
                        未执行金额.ToString("F2"),
                        d.执行人 ?? "",
                        d.提成金额?.ToString("F2") ?? "0.00"
                    };
                    while (mainRow.Count < headers.Count) mainRow.Add(""); // 执行次数/执行时间/执行人工号/执行人姓名/岗位/备注 留空
                    rows.Add(mainRow);

                    // 明细子行（主列留空，只填执行明细列）
                    List<MedicalTechExecution> execs;
                    if (d.登记ID.HasValue && execMap.TryGetValue(d.登记ID.Value, out execs))
                    {
                        foreach (var e in execs)
                        {
                            var subRow = new List<string>();
                            for (int i = 0; i < mainCols; i++) subRow.Add("");
                            subRow.Add("第" + e.本次次数 + "次");
                            subRow.Add(e.执行时间?.ToString("yyyy-MM-dd HH:mm") ?? "");
                            subRow.Add(e.执行人工号 ?? "");
                            subRow.Add(e.执行人姓名 ?? "");
                            subRow.Add(e.岗位 ?? "");
                            subRow.Add(e.备注 ?? "");
                            rows.Add(subRow);
                        }
                    }
                }

                if (rows.Count == 0)
                    return Json(new { success = false, msg = "没有数据可导出" });

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Sheet1");

                    // 表头
                    for (int i = 0; i < headers.Count; i++)
                        ws.Cell(1, i + 1).Value = headers[i];
                    var hdrRange = ws.Range(1, 1, 1, headers.Count);
                    hdrRange.Style.Font.Bold = true;
                    hdrRange.Style.Font.FontColor = XLColor.White;
                    hdrRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E74B5");
                    hdrRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // 数据
                    for (int r = 0; r < rows.Count; r++)
                    {
                        var isSub = !string.IsNullOrEmpty(rows[r][mainCols]); // 第14列非空 = 明细子行
                        for (int c = 0; c < rows[r].Count; c++)
                        {
                            var cell = ws.Cell(r + 2, c + 1);
                            cell.Value = rows[r][c];
                        }
                        if (isSub)
                        {
                            for (int c = 0; c < headers.Count; c++)
                                ws.Cell(r + 2, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F7FB");
                        }
                    }

                    ws.Columns().AdjustToContents();
                    using (var ms = new MemoryStream())
                    {
                        workbook.SaveAs(ms);
                        return File(ms.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "医技登记与执行.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        /// <summary>
        /// 计算项目当前状态（与页面一致）
        /// </summary>
        private string GetStatus(MedicalTechChargeItem d)
        {
            if (!d.登记ID.HasValue) return "未登记";
            return (d.已执行次数 ?? 0) < (d.总次数 ?? 0) ? "进行中" : "已完成";
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

                // 检查是否已存在相同流水号+项目名称的记录
                var checkSql = @"SELECT COUNT(*) FROM fghis5..医技登记表
                                WHERE 流水号 = @流水号 AND 项目名称 = @项目名称";
                var exists = db.Database.SqlQuery<int>(checkSql,
                    new SqlParameter("@流水号", 流水号),
                    new SqlParameter("@项目名称", 项目名称 ?? "")
                ).FirstOrDefault() > 0;

                if (exists)
                    return Json(new { success = false, msg = "该项目已登记，请勿重复登记" });

                // 提成金额 = 项目收费金额 × 提成比例（不依赖完成状态，登记时即核算）
                var 金额 = db.Database.SqlQuery<decimal?>(
                    @"SELECT TOP 1 b.金额 FROM fghis5..门诊_收费明细表 b
                      WHERE b.结帐ID = @结帐ID AND b.处方ID = @处方ID AND b.项目名称 = @项目名称",
                    new SqlParameter("@结帐ID", 结帐ID),
                    new SqlParameter("@处方ID", 处方ID),
                    new SqlParameter("@项目名称", 项目名称 ?? "")).FirstOrDefault();

                var 比例 = db.Database.SqlQuery<decimal?>(
                    @"SELECT TOP 1 提成比例 FROM fghis5..医技项目操作人员提成表 WHERE 项目名称 = @项目名称",
                    new SqlParameter("@项目名称", 项目名称 ?? "")).FirstOrDefault();

                var 提成金额 = Math.Round((金额 ?? 0) * (比例 ?? 0) / 100m, 2);

                var sql = @"INSERT INTO fghis5..医技登记表 (流水号, 门诊号, 就诊ID, 病人姓名, 项目名称, 总次数, 登记时间, 登记人工号, 提成金额)
                            VALUES (@流水号, @门诊号, @就诊ID, @病人姓名, @项目名称, @总次数, GETDATE(), @登记人工号, @提成金额);
                            SELECT CAST(SCOPE_IDENTITY() AS INT)";

                var 登记ID = db.Database.SqlQuery<int>(sql,
                    new SqlParameter("@流水号", 流水号),
                    new SqlParameter("@门诊号", 门诊号),
                    new SqlParameter("@就诊ID", 就诊ID),
                    new SqlParameter("@病人姓名", 病人姓名 ?? ""),
                    new SqlParameter("@项目名称", 项目名称 ?? ""),
                    new SqlParameter("@总次数", 总次数),
                    new SqlParameter("@登记人工号", 登记人工号 ?? ""),
                    new SqlParameter("@提成金额", 提成金额)
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
        public ActionResult Execute(int 登记ID, string 执行时间, int 执行次数, string 执行人工号, string 执行人姓名, string 岗位, string 备注)
        {
            try
            {
                if (登记ID <= 0)
                    return Json(new { success = false, msg = "登记ID无效" });

                if (执行次数 <= 0) 执行次数 = 1;

                // 查询登记信息（只需总次数）
                var totalSql = @"SELECT 总次数 FROM fghis5..医技登记表 WHERE 登记ID = @登记ID";
                var totalCount = db.Database.SqlQuery<int?>(totalSql,
                    new SqlParameter("@登记ID", 登记ID)).FirstOrDefault();

                if (totalCount == null)
                    return Json(new { success = false, msg = "登记记录不存在" });

                var 总次数 = totalCount.Value;

                // 获取当前最大执行次数（排除已取消的）
                var maxSql = @"SELECT ISNULL(MAX(本次次数), 0) FROM fghis5..医技执行记录表 WHERE 登记ID = @登记ID AND delete_flag = 'f'";
                var maxCount = db.Database.SqlQuery<int>(maxSql,
                    new SqlParameter("@登记ID", 登记ID)).FirstOrDefault();

                if (maxCount >= 总次数)
                    return Json(new { success = false, msg = "已到达总次数，无需再执行" });

                // 检查本次要执行的次数是否超出剩余次数
                var 剩余次数 = 总次数 - maxCount;
                if (执行次数 > 剩余次数)
                    执行次数 = 剩余次数;

                // 解析执行时间
                DateTime parsedExecTime;
                if (!DateTime.TryParse(执行时间 ?? "", out parsedExecTime))
                    parsedExecTime = DateTime.Now;

                // 批量插入执行记录
                var execSql = @"INSERT INTO fghis5..医技执行记录表 (登记ID, 本次次数, 执行时间, 执行人工号, 执行人姓名, 岗位, 备注)
                                VALUES (@登记ID, @本次次数, @执行时间, @执行人工号, @执行人姓名, @岗位, @备注)";

                for (int i = 1; i <= 执行次数; i++)
                {
                    db.Database.ExecuteSqlCommand(execSql,
                        new SqlParameter("@登记ID", 登记ID),
                        new SqlParameter("@本次次数", maxCount + i),
                        new SqlParameter("@执行时间", parsedExecTime),
                        new SqlParameter("@执行人工号", 执行人工号 ?? ""),
                        new SqlParameter("@执行人姓名", 执行人姓名 ?? ""),
                        new SqlParameter("@岗位", 岗位 ?? ""),
                        new SqlParameter("@备注", 备注 ?? ""));
                }

                var isCompleted = (maxCount + 执行次数) >= 总次数;

                // 提成金额在登记时已按「项目金额 × 提成比例」核算，执行阶段不再处理

                return Json(new
                {
                    success = true,
                    msg = "执行成功",
                    本次执行次数 = 执行次数,
                    当前最大次数 = maxCount + 执行次数,
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
                var regSql = @"SELECT r.登记ID, r.流水号, r.门诊号, r.就诊ID, r.病人姓名, r.项目名称, r.总次数, r.登记时间, r.登记人工号,
                                       ROUND(ISNULL(b.金额, 0) * ISNULL(c.提成比例, 0) / 100.0, 2) AS 提成金额
                                FROM fghis5..医技登记表 r
                                LEFT JOIN fghis5..门诊_收费明细表 b ON CAST(b.结帐ID AS NVARCHAR) + '_' + CAST(b.处方ID AS NVARCHAR) = r.流水号
                                    AND b.项目名称 = r.项目名称
                                LEFT JOIN (SELECT 项目名称, MAX(提成比例) AS 提成比例
                                           FROM fghis5..医技项目操作人员提成表
                                           GROUP BY 项目名称) c ON c.项目名称 = r.项目名称
                                WHERE r.登记ID = @登记ID";
                var reg = db.Database.SqlQuery<MedicalTechRegistration>(regSql,
                    new SqlParameter("@登记ID", 登记ID)).FirstOrDefault();

                if (reg == null)
                    return Json(new { success = false, msg = "登记记录不存在" }, JsonRequestBehavior.AllowGet);

                var execSql = @"SELECT 执行ID, 登记ID, 本次次数, 执行时间, 执行人工号, 执行人姓名, 岗位, 备注, delete_flag
                                FROM fghis5..医技执行记录表
                                WHERE 登记ID = @登记ID AND delete_flag = 'f'
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
                    提成金额 = reg.提成金额,
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

        // ========== 医技执行人员信息维护 ==========

        /// <summary>
        /// 人员信息维护页面
        /// </summary>
        public ActionResult StaffList()
        {
            return View();
        }

        /// <summary>
        /// 查询人员列表
        /// </summary>
        [HttpGet]
        public ActionResult GetStaffList(string name)
        {
            try
            {
                var sql = @"SELECT * FROM fghis5..医技执行人员信息表
                            WHERE @name = '' OR 姓名 LIKE '%' + @name + '%' OR 工号 LIKE '%' + @name + '%'
                            ORDER BY 序号 ASC";

                var result = db.Database.SqlQuery<MedicalTechStaff>(sql,
                    new SqlParameter("@name", (name ?? "").Trim())).ToList();

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
        /// 新增/修改人员
        /// </summary>
        [HttpPost]
        public ActionResult SaveStaff(int? 序号, string 工号, string 姓名, string 岗位, string 备注)
        {
            try
            {
                if (string.IsNullOrEmpty(工号))
                    return Json(new { success = false, msg = "工号不能为空" });
                if (string.IsNullOrEmpty(姓名))
                    return Json(new { success = false, msg = "姓名不能为空" });

                if (序号.HasValue)
                {
                    // 修改
                    var sql = @"UPDATE fghis5..医技执行人员信息表
                                SET 工号 = @工号, 姓名 = @姓名, 岗位 = @岗位, 备注 = @备注
                                WHERE 序号 = @序号";
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@序号", 序号.Value),
                        new SqlParameter("@工号", 工号 ?? ""),
                        new SqlParameter("@姓名", 姓名 ?? ""),
                        new SqlParameter("@岗位", 岗位 ?? ""),
                        new SqlParameter("@备注", 备注 ?? ""));
                }
                else
                {
                    // 新增
                    var sql = @"INSERT INTO fghis5..医技执行人员信息表 (工号, 姓名, 岗位, 备注)
                                VALUES (@工号, @姓名, @岗位, @备注)";
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@工号", 工号 ?? ""),
                        new SqlParameter("@姓名", 姓名 ?? ""),
                        new SqlParameter("@岗位", 岗位 ?? ""),
                        new SqlParameter("@备注", 备注 ?? ""));
                }

                return Json(new { success = true, msg = "保存成功" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        /// <summary>
        /// 删除人员
        /// </summary>
        [HttpPost]
        public ActionResult DeleteStaff(int 序号)
        {
            try
            {
                var sql = @"DELETE FROM fghis5..医技执行人员信息表 WHERE 序号 = @序号";
                db.Database.ExecuteSqlCommand(sql, new SqlParameter("@序号", 序号));
                return Json(new { success = true, msg = "删除成功" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        // ========== 医技项目默认次数 ==========

        /// <summary>
        /// 项目默认次数页面
        /// </summary>
        public ActionResult DefaultCount()
        {
            return View();
        }

        /// <summary>
        /// 查询项目默认次数列表（按项目ID匹配；name 仅用于维护页按名称搜索）
        /// </summary>
        [HttpGet]
        public ActionResult GetDefaultCountList(string name, string 项目ID)
        {
            try
            {
                var sql = @"SELECT * FROM fghis5..医技项目默认次数表
                            WHERE (@name = '' OR 项目名称 = @name)
                              AND (@项目ID = '' OR 项目ID = @项目ID)
                            ORDER BY 序号 ASC";

                var result = db.Database.SqlQuery<MedicalTechDefaultCount>(sql,
                    new SqlParameter("@name", (name ?? "").Trim()),
                    new SqlParameter("@项目ID", (项目ID ?? "").Trim())).ToList();

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
        /// 新增/修改项目默认次数
        /// </summary>
        [HttpPost]
        public ActionResult SaveDefaultCount(int? 序号, string 项目名称, int 默认总次数, string 项目ID)
        {
            try
            {
                if (string.IsNullOrEmpty(项目名称))
                    return Json(new { success = false, msg = "项目名称不能为空" });
                if (默认总次数 <= 0)
                    return Json(new { success = false, msg = "默认总次数必须大于0" });

                项目ID = (项目ID ?? "").Trim();
                object 项目IDValue = string.IsNullOrEmpty(项目ID) ? (object)DBNull.Value : 项目ID;

                if (序号.HasValue)
                {
                    var sql = @"UPDATE fghis5..医技项目默认次数表
                                SET 项目ID = @项目ID, 项目名称 = @项目名称, 默认总次数 = @默认总次数
                                WHERE 序号 = @序号";
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@序号", 序号.Value),
                        new SqlParameter("@项目ID", 项目IDValue),
                        new SqlParameter("@项目名称", 项目名称 ?? ""),
                        new SqlParameter("@默认总次数", 默认总次数));
                }
                else
                {
                    var sql = @"INSERT INTO fghis5..医技项目默认次数表 (项目ID, 项目名称, 默认总次数)
                                VALUES (@项目ID, @项目名称, @默认总次数)";
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@项目ID", 项目IDValue),
                        new SqlParameter("@项目名称", 项目名称 ?? ""),
                        new SqlParameter("@默认总次数", 默认总次数));
                }

                return Json(new { success = true, msg = "保存成功" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        /// <summary>
        /// 删除项目默认次数
        /// </summary>
        [HttpPost]
        public ActionResult DeleteDefaultCount(int 序号)
        {
            try
            {
                var sql = @"DELETE FROM fghis5..医技项目默认次数表 WHERE 序号 = @序号";
                db.Database.ExecuteSqlCommand(sql, new SqlParameter("@序号", 序号));
                return Json(new { success = true, msg = "删除成功" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        // ========== 医技项目操作人员提成维护 ==========

        /// <summary>
        /// 医技项目操作人员提成维护页面
        /// </summary>
        public ActionResult Commission()
        {
            return View();
        }

        /// <summary>
        /// 查询提成配置列表
        /// </summary>
        [HttpGet]
        public ActionResult GetCommissionList(string name)
        {
            try
            {
                var sql = @"SELECT * FROM fghis5..医技项目操作人员提成表
                            WHERE @name = '' OR 项目名称 LIKE '%' + @name + '%'
                            ORDER BY 序号 ASC";

                var result = db.Database.SqlQuery<MedicalTechCommission>(sql,
                    new SqlParameter("@name", (name ?? "").Trim())).ToList();

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
        /// 新增/修改提成配置
        /// </summary>
        [HttpPost]
        public ActionResult SaveCommission(int? 序号, string 项目名称, string 岗位, decimal 提成比例)
        {
            try
            {
                if (string.IsNullOrEmpty(项目名称))
                    return Json(new { success = false, msg = "项目名称不能为空" });
                if (string.IsNullOrEmpty(岗位))
                    return Json(new { success = false, msg = "岗位不能为空" });
                if (提成比例 <= 0)
                    return Json(new { success = false, msg = "提成比例必须大于0" });

                // 查重：同一项目名称 + 岗位不允许重复（排除自身）
                var checkSql = @"SELECT COUNT(*) FROM fghis5..医技项目操作人员提成表
                                WHERE 项目名称 = @项目名称 AND 岗位 = @岗位 AND (@序号 IS NULL OR 序号 != @序号)";
                var exists = db.Database.SqlQuery<int>(checkSql,
                    new SqlParameter("@项目名称", 项目名称 ?? ""),
                    new SqlParameter("@岗位", 岗位 ?? ""),
                    new SqlParameter("@序号", (object)序号 ?? DBNull.Value)
                ).FirstOrDefault() > 0;

                if (exists)
                    return Json(new { success = false, msg = "该项目在该岗位下已配置提成比例，请勿重复" });

                if (序号.HasValue)
                {
                    var sql = @"UPDATE fghis5..医技项目操作人员提成表
                                SET 项目名称 = @项目名称, 岗位 = @岗位, 提成比例 = @提成比例
                                WHERE 序号 = @序号";
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@序号", 序号.Value),
                        new SqlParameter("@项目名称", 项目名称 ?? ""),
                        new SqlParameter("@岗位", 岗位 ?? ""),
                        new SqlParameter("@提成比例", 提成比例));
                }
                else
                {
                    var sql = @"INSERT INTO fghis5..医技项目操作人员提成表 (项目名称, 岗位, 提成比例)
                                VALUES (@项目名称, @岗位, @提成比例)";
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@项目名称", 项目名称 ?? ""),
                        new SqlParameter("@岗位", 岗位 ?? ""),
                        new SqlParameter("@提成比例", 提成比例));
                }

                return Json(new { success = true, msg = "保存成功" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        /// <summary>
        /// 删除提成配置
        /// </summary>
        [HttpPost]
        public ActionResult DeleteCommission(int 序号)
        {
            try
            {
                var sql = @"DELETE FROM fghis5..医技项目操作人员提成表 WHERE 序号 = @序号";
                db.Database.ExecuteSqlCommand(sql, new SqlParameter("@序号", 序号));
                return Json(new { success = true, msg = "删除成功" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        // ========== 取消执行 ==========

        /// <summary>
        /// 取消最新的一条执行记录（仅可取消当前最新且未取消的）
        /// </summary>
        [HttpPost]
        public ActionResult CancelExecution(int 登记ID, int 本次次数)
        {
            try
            {
                // 验证只能取消最新的一条
                var maxSql = @"SELECT ISNULL(MAX(本次次数), 0) FROM fghis5..医技执行记录表
                               WHERE 登记ID = @登记ID AND delete_flag = 'f'";
                var maxCount = db.Database.SqlQuery<int>(maxSql,
                    new SqlParameter("@登记ID", 登记ID)).FirstOrDefault();

                if (本次次数 != maxCount)
                    return Json(new { success = false, msg = "只能取消最新的一条执行记录" });
                if (maxCount <= 0)
                    return Json(new { success = false, msg = "没有可取消的执行记录" });

                var jobNumber = GetCurrentJobNumber();
                var now = DateTime.Now;

                var sql = @"UPDATE fghis5..医技执行记录表
                            SET delete_flag = 't', 执行人工号 = @取消人工号, 执行时间 = @取消时间
                            WHERE 登记ID = @登记ID AND 本次次数 = @本次次数 AND delete_flag = 'f'";

                var affected = db.Database.ExecuteSqlCommand(sql,
                    new SqlParameter("@登记ID", 登记ID),
                    new SqlParameter("@本次次数", 本次次数),
                    new SqlParameter("@取消人工号", jobNumber ?? ""),
                    new SqlParameter("@取消时间", now));

                if (affected <= 0)
                    return Json(new { success = false, msg = "取消失败，记录不存在或已被取消" });

                // 提成金额不依赖完成状态（登记时已按「项目金额 × 提成比例」核算），取消执行不影响提成

                return Json(new { success = true, msg = "取消成功" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        // ========== 医技执行记录查询 ==========

        /// <summary>
        /// 执行记录查询页面
        /// </summary>
        public ActionResult ExecutionRecords()
        {
            return View();
        }

        /// <summary>
        /// 查询执行记录
        /// </summary>
        [HttpGet]
        public ActionResult GetExecutionRecords(string bdate, string edate, string name)
        {
            if (string.IsNullOrEmpty(bdate))
                bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate))
                edate = DateTime.Today.ToString("yyyy-MM-dd");

            try
            {
                var sql = @"WITH 支付汇总 AS (
                    SELECT 结帐ID, CAST(SUM(支付金额) AS DECIMAL(28,10)) AS 实收金额
                    FROM fghis5..门诊_收费支付表
                    WHERE 支付方式 != '6'
                    GROUP BY 结帐ID
                )
                SELECT r.登记ID,
                       CONVERT(varchar, MAX(e.执行时间), 20) AS 执行时间,
                       r.病人姓名, r.项目名称,
                       MAX(b.套餐名称) AS 套餐名称,
                       COUNT(*) AS 本次执行次数,
                       SUM(ISNULL(CAST(p.实收金额 AS DECIMAL(28,10)) * CAST(b.金额 AS DECIMAL(28,10)) / NULLIF(CAST(a.总金额 AS DECIMAL(28,10)) * r.总次数, 0), 0)) AS 本次执行金额,
                       MAX(r.提成金额) AS 操作人提成,
                       MAX(b.数量) AS 数量,
                       ISNULL(MAX(dc.默认总次数), 1) AS 默认次数,
                       r.总次数,
                       MAX(e.本次次数) AS 最新本次次数,
                       MAX(e.执行人姓名) AS 执行人姓名,
                       MAX(e.岗位) AS 岗位,
                       MAX(e.备注) AS 备注
                FROM fghis5..医技执行记录表 e
                JOIN fghis5..医技登记表 r ON e.登记ID = r.登记ID
                LEFT JOIN fghis5..门诊_收费明细表 b ON CAST(b.结帐ID AS NVARCHAR) + '_' + CAST(b.处方ID AS NVARCHAR) = r.流水号
                    AND b.项目名称 = r.项目名称
                LEFT JOIN fghis5..门诊_收费发票表 a ON a.结帐ID = b.结帐ID
                LEFT JOIN 支付汇总 p ON p.结帐ID = a.结帐ID
                LEFT JOIN fghis5..医技项目默认次数表 dc ON dc.项目ID = CAST(b.项目ID AS NVARCHAR)
                WHERE e.delete_flag = 'f'
                  AND CONVERT(date, e.执行时间) BETWEEN @bdate AND @edate
                  AND (@name = '' OR r.病人姓名 LIKE '%' + @name + '%' OR r.项目名称 LIKE '%' + @name + '%')
                GROUP BY r.登记ID,e.执行时间, r.病人姓名, r.项目名称, r.总次数
                ORDER BY MAX(e.执行时间) DESC";

                var result = db.Database.SqlQuery<ExecutionRecordQuery>(sql,
                    new SqlParameter("@bdate", QueryHelper.ParseDate(bdate)),
                    new SqlParameter("@edate", QueryHelper.ParseDate(edate)),
                    new SqlParameter("@name", (name ?? "").Trim())).ToList();

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
        /// 修改总次数
        /// </summary>
        [HttpPost]
        public ActionResult UpdateTotalCount(List<int> 登记IDs, int 新总次数)
        {
            try
            {
                if (登记IDs == null || 登记IDs.Count == 0)
                    return Json(new { success = false, msg = "请选择要修改的记录" });
                if (新总次数 <= 0)
                    return Json(new { success = false, msg = "总次数必须大于0" });

                var sql = @"UPDATE fghis5..医技登记表 SET 总次数 = @总次数 WHERE 登记ID = @登记ID";
                foreach (var id in 登记IDs)
                {
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@总次数", 新总次数),
                        new SqlParameter("@登记ID", id));
                }

                return Json(new { success = true, msg = "修改成功，共修改 " + 登记IDs.Count + " 条记录" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        /// <summary>
        /// 修改备注
        /// </summary>
        [HttpPost]
        public ActionResult UpdateRemark(List<int> 登记IDs, string 新备注)
        {
            try
            {
                if (登记IDs == null || 登记IDs.Count == 0)
                    return Json(new { success = false, msg = "请选择要修改的记录" });

                var sql = @"UPDATE fghis5..医技执行记录表 SET 备注 = @备注 WHERE 登记ID = @登记ID AND delete_flag = 'f'";
                foreach (var id in 登记IDs)
                {
                    db.Database.ExecuteSqlCommand(sql,
                        new SqlParameter("@备注", 新备注 ?? ""),
                        new SqlParameter("@登记ID", id));
                }

                return Json(new { success = true, msg = "修改成功，共修改 " + 登记IDs.Count + " 条记录" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        // GET: /MedicalTech/MaterialInbound
        public ActionResult MaterialInbound()
        {
            return View();
        }

        /// <summary>
        /// 耗材入库查询（跨Oracle库：金蝶EAS）
        /// GET /MedicalTech/GetMaterialInbound?bdate=2026-08-01&edate=2026-08-31&kw=物料编码/名称/拼音首字母
        /// </summary>
        [HttpGet]
        public ActionResult GetMaterialInbound(string bdate, string edate, string kw)
        {
            try
            {
                if (string.IsNullOrEmpty(bdate)) bdate = DateTime.Today.ToString("yyyy-MM-dd");
                if (string.IsNullOrEmpty(edate)) edate = DateTime.Today.ToString("yyyy-MM-dd");
                var b = DateTime.Parse(bdate);
                var e = DateTime.Parse(edate);

                var sql = @"SELECT a.faudittime, a.fnumber, g.fname_l2, c.fnumber, c.fname_l2, c.FModel,
                                   d.fnumber, d.FNAME_L2, b2.flot, b2.fexp, f.fname_l2, b2.FQty, p.fname_l2,
                                   lGroup.fname_l2
                            FROM kingdee.t_im_materialreqbill a
                                 INNER JOIN kingdee.t_im_materialreqbillentry b2 ON a.fid = b2.fparentid
                                 INNER JOIN kingdee.t_bd_material c ON c.fid = b2.fmaterialid
                                 INNER JOIN kingdee.T_BD_AsstAttrValue d ON d.fid = b2.fassistpropertyid
                                 INNER JOIN kingdee.T_BD_MeasureUnit f ON b2.funitid = f.fid
                                 INNER JOIN kingdee.T_DB_WAREHOUSE g ON g.fid = b2.fwarehouseid
                                 INNER JOIN kingdee.t_pm_user p ON a.fcreatorid = p.fid
                                 INNER JOIN kingdee.T_BD_MaterialGroup lGroup ON lGroup.fid = c.FMATERIALGROUPID
                            WHERE a.fstorageorgunitid = 'MsoAAAGNfOPM567U'
                              AND a.fcostcenterorgunitid = 'MsoAAASRHsPM567U'
                              AND a.faudittime >= :bdate
                              AND a.faudittime < :edate + 1
                              AND lGroup.fname_l2 = '医用耗材'
                            ORDER BY a.faudittime";
                var cs = ConfigurationManager.ConnectionStrings["EAS_Oracle"].ConnectionString;
                var list = new List<MaterialInboundItem>();
                using (var conn = new OracleConnection(cs))
                {
                    conn.Open();
                    using (var cmd = new OracleCommand(sql, conn))
                    {
                        cmd.Parameters.Add("bdate", OracleDbType.Date).Value = b;
                        cmd.Parameters.Add("edate", OracleDbType.Date).Value = e;
                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                list.Add(new MaterialInboundItem
                                {
                                    ckrq = rd.IsDBNull(0) ? null : rd.GetDateTime(0).ToString("yyyy-MM-dd HH:mm"),
                                    ckdh = rd.IsDBNull(1) ? null : rd.GetString(1),
                                    ckmc = rd.IsDBNull(2) ? null : rd.GetString(2),
                                    wlbm = rd.IsDBNull(3) ? null : rd.GetString(3),
                                    wlmc = rd.IsDBNull(4) ? null : rd.GetString(4),
                                    gg = rd.IsDBNull(5) ? null : rd.GetString(5),
                                    cdbm = rd.IsDBNull(6) ? null : rd.GetString(6),
                                    cdmc = rd.IsDBNull(7) ? null : rd.GetString(7),
                                    ph = rd.IsDBNull(8) ? null : rd.GetString(8),
                                    xq = rd.IsDBNull(9) ? null : rd.GetDateTime(9).ToString("yyyy-MM-dd"),
                                    dw = rd.IsDBNull(10) ? null : rd.GetString(10),
                                    sl = rd.IsDBNull(11) ? (decimal?)null : rd.GetDecimal(11),
                                    ckr = rd.IsDBNull(12) ? null : rd.GetString(12),
                                    wllb = rd.IsDBNull(13) ? null : rd.GetString(13),
                                });
                            }
                        }
                    }
                }

                // 关键字过滤：物料编码 / 物料名称 / 物料名称拼音首字母
                if (!string.IsNullOrWhiteSpace(kw))
                {
                    var k = kw.Trim();
                    var upper = k.ToUpperInvariant();
                    list = list.Where(x =>
                        (x.wlbm != null && x.wlbm.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (x.wlmc != null && x.wlmc.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        GetPinyinInitials(x.wlmc).IndexOf(upper, StringComparison.Ordinal) >= 0
                    ).ToList();
                }
                // 按入库日期倒序
                list = list.OrderByDescending(x => x.ckrq).ToList();

                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// 取中文串的拼音首字母（大写）。如“一次性使用” -> “YXC”；
        /// 英文/数字原样保留大写，其余非汉字字符忽略。
        /// </summary>
        private static string GetPinyinInitials(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            var gb = Encoding.GetEncoding("gb2312");
            foreach (char c in text)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    sb.Append(char.ToUpperInvariant(c));
                    continue;
                }
                if (c < 128) continue; // 数字/标点等忽略
                var bytes = gb.GetBytes(c.ToString());
                if (bytes.Length != 2) continue; // 非GB2312汉字，忽略
                var code = (bytes[0] << 8) | bytes[1];
                var initial = GetPinyinInitial(code);
                if (initial != '\0') sb.Append(initial);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 按 GB2312 区位码边界表取汉字拼音首字母（A~Z，不含 I/U/V）
        /// </summary>
        private static char GetPinyinInitial(int code)
        {
            var bounds = new[]
            {
                0xB0A1, 0xB0C5, 0xB2C1, 0xB4EE, 0xB6EA, 0xB7A2, 0xB8C1, 0xB9FE,
                0xBBF7, 0xBFA6, 0xC0AC, 0xC2E8, 0xC4C3, 0xC5B6, 0xC5BE, 0xC6DA,
                0xC8BB, 0xC8F6, 0xCBFA, 0xCDDA, 0xCEF4, 0xD1B9, 0xD4D1
            };
            const string initials = "ABCDEFGHJKLMNOPQRSTWXYZ";
            if (code < bounds[0]) return '\0';
            for (int i = 0; i < bounds.Length - 1; i++)
            {
                if (code >= bounds[i] && code < bounds[i + 1]) return initials[i];
            }
            return 'Z';
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
