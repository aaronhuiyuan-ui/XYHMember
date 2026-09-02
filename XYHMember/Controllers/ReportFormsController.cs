using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    [AuthFilter]
    public class ReportFormsController : Controller
    {
        private XYHDbContext db = new XYHDbContext();


        //日报表查询
        public ActionResult DailyReport()
        {
            return View(); 
        }

        //between @bdate and @edate --and (@pid='' or 交易卡号=@pid or 姓名 = @pid)
        public ActionResult GetDailyReport()
        {
            var name = Request["name"].Trim();
            var bdate = Request["bdatepicker"];
            var edate = Request["edatepicker"];


            string sqlQuery = @"SET ARITHABORT ON; 
                       SET ANSI_WARNINGS ON;
                       SET ANSI_NULLS ON;
                       SET ANSI_PADDING ON;
                       SET CONCAT_NULL_YIELDS_NULL ON;
WITH sfrbb AS (
	SELECT
		COALESCE(m.就诊id, m2.就诊id) AS 就诊id,
		a.PID,
		a.门诊号,
		a.姓名,
		a.出生日期,
		a.身份证号,
		b.结帐ID,
		b.结帐日期,
		b.结帐时间,
		b.发票状态,
		b.操作工号,
		b.工作组号 AS 关联销售,
		STUFF((
        SELECT DISTINCT ';' + c2.医生姓名
        FROM (
            SELECT DISTINCT
                a1.结帐ID,
                d1.医生姓名
            FROM
                fghis5..门诊_收费明细表 a1
                LEFT JOIN fghis5..门诊_收费处方表 f1 ON f1.结帐ID = a1.结帐ID AND a1.处方id = f1.处方id
                LEFT JOIN fghis5..系统_医生信息表 d1 ON f1.医生ID = d1.医生ID
            WHERE a1.结帐ID = ABS(b.结帐ID)
        ) c2
        WHERE c2.医生姓名 IS NOT NULL
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') as 接诊医生,
		
		b.总金额,
		SUM ( CASE WHEN c.类别名称 = '诊查费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 诊查费,
		SUM ( CASE WHEN c.类别名称 = '材料费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 材料费,
		SUM ( CASE WHEN c.类别名称 = '检查费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 检查费,
		SUM ( CASE WHEN c.类别名称 = '检验费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 检验费,
		SUM ( CASE WHEN c.类别名称 = '治疗费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 治疗费,
		SUM ( CASE WHEN c.类别名称 = '中草药' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 中草药,
		SUM ( CASE WHEN c.类别名称 = '工艺品' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 工艺品,
		SUM ( CASE WHEN c.类别名称 = '理疗费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 理疗费,
		SUM ( CASE WHEN c.类别名称 = '加工费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 加工费,
        SUM ( CASE WHEN c.类别名称 = '辨证论治费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END ) AS 辨证论治费,
		'收费' AS 类别,
		ISNULL( d.现金, 0 ) AS 现金,
		ISNULL( d.POS, 0 ) AS POS,
		ISNULL( d.储值卡, 0 ) AS 储值卡,
		ISNULL( d.折扣, 0 ) AS 折扣,
		ISNULL( d.微信, 0 ) AS 微信,
		ISNULL( d.支付宝, 0 ) AS 支付宝,
        ISNULL( d.现金, 0 ) + ISNULL( d.POS, 0 ) + ISNULL( d.储值卡, 0 ) + 
	    ISNULL( d.微信, 0 ) + ISNULL( d.支付宝, 0 ) AS 实际支付,
        d.折扣比例,d.备注
        
	FROM
		fghis5..系统_病人基本信息表 a
		LEFT JOIN fghis5..门诊_收费发票表 b ON a.门诊号 = b.门诊号
		LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_收费发票表_结帐ID GROUP BY 结帐ID) m ON b.结帐ID = m.结帐ID
		LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_收费发票表_结帐ID GROUP BY 结帐ID) m2 ON m2.结帐ID = ABS(b.结帐ID)
		LEFT JOIN (
		SELECT
			a.结帐ID,
			b.类别名称,
			SUM ( a.金额 ) AS 金额 
		FROM
			fghis5..门诊_收费明细表 a
			LEFT JOIN fghis5..代码_项目基本类别表 b ON a.项目类别 = b.项目类别 
		GROUP BY
			a.结帐ID,
			b.类别名称 
		) c ON ABS(b.结帐ID) = c.结帐ID
		LEFT JOIN (
		SELECT
			结帐ID,
			SUM ( CASE WHEN 支付方式 = 0 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 现金,
			SUM ( CASE WHEN 支付方式 = 1 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS POS,
			SUM ( CASE WHEN 支付方式 = 4 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 储值卡,
			SUM ( CASE WHEN 支付方式 = 6 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 折扣,
			SUM ( CASE WHEN 支付方式 = 31 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 微信,
			SUM ( CASE WHEN 支付方式 = 32 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 支付宝,
            max(fghis5.dbo.ExtractFieldValue(备注, '备注')) AS 备注,
            max(fghis5.dbo.ExtractFieldValue(备注, '折扣比例')) AS 折扣比例
		FROM
			fghis5..门诊_收费支付表 
		WHERE
			支付方式 IN ( 0, 1, 4, 6, 31, 32 ) 
		GROUP BY
			结帐ID
		) d ON b.结帐ID = d.结帐ID 
	WHERE
		(b.操作工号 != '6666' OR b.发票状态 IN ('0','1')) 
		AND b.结帐日期 BETWEEN @bdate AND @edate 
		AND ( @name = '' OR a.姓名 = @name ) 
		AND b.发票状态 IN ('2','1','0') 
		AND a.姓名 NOT LIKE '%测试%' 
	GROUP BY
		COALESCE(m.就诊id, m2.就诊id),
		a.PID,
		a.门诊号,
		a.姓名,
		a.出生日期,
		a.身份证号,
		b.结帐ID,
		b.结帐日期,
		b.结帐时间,
		b.发票状态,
		b.操作工号,
		b.工作组号,
		b.总金额,
		d.现金,
		d.POS,
		d.储值卡,
		d.折扣,
		d.微信,
		d.支付宝,d.折扣比例,d.备注
		UNION ALL
	SELECT
		COALESCE(gm.就诊id, gm2.就诊id) AS 就诊id,
		a.PID,
		a.门诊号,
		a.姓名,
		a.出生日期,
		a.身份证号,
		b.结帐ID,
		b.结帐日期,
		b.结帐时间,
		b.发票状态,
		b.操作工号,
		'' AS 关联销售,
		f.接诊医生姓名 as 接诊医生,
		b.总金额,
		b.总金额 AS 诊查费,
		0 AS 材料费,
		0 AS 检查费,
		0 AS 检验费,
		0 AS 治疗费,
		0 AS 中草药,
		0 AS 工艺品,
		0 AS 理疗费,
		0 AS 加工费,
        0 as 辨证论治费,
		'挂号' AS 类别,
		ISNULL( d.现金, 0 ) AS 现金,
		ISNULL( d.POS, 0 ) AS POS,
		ISNULL( d.储值卡, 0 ) AS 储值卡,
		ISNULL( d.折扣, 0 ) AS 折扣,
		ISNULL( d.微信, 0 ) AS 微信,
		ISNULL( d.支付宝, 0 ) AS 支付宝,
        ISNULL( d.现金, 0 ) + ISNULL( d.POS, 0 ) + ISNULL( d.储值卡, 0 ) + 
	    ISNULL( d.微信, 0 ) + ISNULL( d.支付宝, 0 ) AS 实际支付,
        d.折扣比例,d.备注
	FROM
		fghis5..系统_病人基本信息表 a
	LEFT JOIN fghis5..门诊_挂号发票表 b ON a.门诊号 = b.门诊号
		LEFT JOIN (
		SELECT
			结帐ID,
			SUM ( CASE WHEN 支付方式 = 0 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 现金,
			SUM ( CASE WHEN 支付方式 = 1 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS POS,
			SUM ( CASE WHEN 支付方式 = 4 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 储值卡,
			SUM ( CASE WHEN 支付方式 = 6 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 折扣,
			SUM ( CASE WHEN 支付方式 = 31 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 微信,
			SUM ( CASE WHEN 支付方式 = 32 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 支付宝,
            max(fghis5.dbo.ExtractFieldValue(备注, '备注')) AS 备注,
            max(fghis5.dbo.ExtractFieldValue(备注, '折扣比例')) AS 折扣比例
		FROM
			fghis5..门诊_挂号支付表 
		WHERE
			支付方式 IN ( 0, 1, 4, 6, 31, 32 ) 
		GROUP BY
			结帐ID
		) d ON b.结帐ID = d.结帐ID 
	LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_挂号明细表 GROUP BY 结帐ID) gm ON b.结帐ID = gm.结帐ID
	LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_挂号明细表 GROUP BY 结帐ID) gm2 ON gm2.结帐ID = ABS(b.结帐ID)
	left join fghis5..门诊_挂号信息表 f on f.就诊ID = COALESCE(gm.就诊id, gm2.就诊id)
	WHERE
	(b.操作工号 != '6666' OR b.发票状态 IN ('0','1')) 
	AND b.结帐日期 BETWEEN @bdate AND @edate 
	AND ( @name = '' OR a.姓名 = @name ) 
	AND b.发票状态 IN ('2','1','0') 
	AND a.姓名 NOT LIKE '%测试%' 
	) 

SELECT
	* 
FROM
	sfrbb
UNION ALL
SELECT
	null AS 就诊id,
	null AS PID,
	null AS 门诊号,
	'' AS 姓名,
	'' AS 出生日期,
	'' AS 身份证号,
	null AS 结帐ID,
	'' AS 结帐日期,
	'' AS 结帐时间,
	null AS 发票状态,
	'' AS 操作工号,
	'' AS 关联销售,
	'' as 接诊医生,
	SUM ( 总金额 ) AS 总金额,
	SUM ( 诊查费 ) AS 诊查费,
	SUM ( 材料费 ) AS 材料费,
	SUM ( 检查费 ) AS 检查费,
	SUM ( 检验费 ) AS 检验费,
	SUM ( 治疗费 ) AS 治疗费,
	SUM ( 中草药 ) AS 中草药,
	SUM ( 工艺品 ) AS 工艺品,
	SUM ( 理疗费 ) AS 理疗费,
	SUM ( 加工费 ) AS 加工费,
    SUM ( 辨证论治费 ) AS 辨证论治费,
	'' AS 类别,
	SUM ( 现金 ) AS 现金,
	SUM ( POS ) AS POS,
	SUM ( 储值卡 ) AS 储值卡,
	SUM ( 折扣 ) AS 折扣,
	SUM ( 微信 ) AS 微信,
	SUM ( 支付宝 ) AS 支付宝,
	SUM ( 现金 ) + SUM ( POS ) + SUM ( 储值卡 ) + 
    SUM ( 微信 ) + SUM ( 支付宝 ) AS 实际支付,
     '' 折扣比例,'' 备注
FROM
	sfrbb";

            var result = db.Database.SqlQuery<DailyReports>(sqlQuery, QueryHelper.BuildReportParams(name, bdate, edate)).ToList();

            return View("DailyReport", result);

        }

        //各科室查询
        public ActionResult Department()
        {
            return View();
        }

        public ActionResult GetDepartment()
        {
            var name = Request["name"].Trim();
            var bdate = Request["bdatepicker"];
            var edate = Request["edatepicker"];

            string sqlQuery = @"
with ksys as 
(select f.结帐ID, p.处方ID, p.就诊ID, f.结帐日期, p.医生工号, d.医生姓名, c.科室名称,
       SIGN(f.结帐ID) * x.金额 AS 金额 
from fghis5..门诊_收费发票表 f 
left join fghis5..门诊_收费处方表 p ON p.结帐ID = ABS(f.结帐ID)
left join (
select 结帐ID, 处方ID, sum(金额) as 金额 from  fghis5..门诊_收费明细表  where 结帐ID in 
(select ABS(结帐ID) from fghis5..门诊_收费发票表  where 发票状态 in ('2','1','0') and 结帐日期 between @bdate and @edate ) 
group by 结帐ID,处方ID
) x ON x.结帐ID = ABS(f.结帐ID) AND x.处方ID = p.处方ID
left join fghis5..代码_科室信息表 c on p.医生科室 = c.科室ID
left join fghis5..系统_医生信息表 d on p.医生ID = d.医生ID
left join fghis5..系统_病人基本信息表 pat ON pat.门诊号 = f.门诊号
where f.发票状态 in ('2','1','0') 
  AND (f.操作工号 != '6666' OR f.发票状态 IN ('0','1')) 
  AND f.结帐日期 between @bdate and @edate 
  AND ISNULL(pat.姓名,'') NOT LIKE '%测试%' 
)

select 结帐日期,科室名称,sum(金额) as 科室总金额 from ksys where   (@name='' or 科室名称 = @name) group by 结帐日期,科室名称";

            var result = db.Database.SqlQuery<Departments>(sqlQuery, QueryHelper.BuildReportParams(name, bdate, edate)).ToList();

            return View("Department", result);
        }

        // =====================================================================
        //  处方对应关系修正（发票状态2 但 处方结果本地表 无记录 → 用退费原处方补录）
        // =====================================================================

        /// <summary>
        /// 处方对应关系修正页面（GET）
        /// </summary>
        public ActionResult PrescriptionFix()
        {
            return View();
        }

        /// <summary>
        /// 待修处方列表：发票状态2、含加工费/快递费(类别99/91)、且 处方结果本地表 无记录。
        /// 自动推荐「候选原处方」：同门诊号、已退费(状态1)、处方结果本地表有记录、日期最近的一条。
        /// GET /ReportForms/GetUnmatchedPrescriptions?bdate=&edate=
        /// </summary>
        [HttpGet]
        public ActionResult GetUnmatchedPrescriptions(string bdate, string edate)
        {
            try
            {
                var b = QueryHelper.ParseDate(string.IsNullOrWhiteSpace(bdate) ? DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd") : bdate.Trim());
                var e = QueryHelper.ParseDate(string.IsNullOrWhiteSpace(edate) ? DateTime.Today.ToString("yyyy-MM-dd") : edate.Trim());

                var list = db.Database.SqlQuery<UnmatchedPrescription>(
                    @"SELECT b.处方ID,
                             CONVERT(varchar(10), CONVERT(date, MAX(b.日期)), 23) AS 日期,
                             MAX(a.姓名) AS 姓名, MAX(a.门诊号) AS 门诊号,
                             SUM(CASE WHEN b.项目类别=99 THEN b.金额 ELSE 0 END) AS 收费加工费,
                             SUM(CASE WHEN b.项目类别=91 THEN b.金额 ELSE 0 END) AS 收费快递费
                      FROM fghis5..门诊_收费明细表 b
                      JOIN fghis5..门诊_收费发票表 a ON a.结帐ID = b.结帐ID
                      WHERE a.发票状态 = '2'
                        AND b.日期 BETWEEN @b AND @e
                        AND EXISTS (SELECT 1 FROM fghis5..门诊_收费明细表 d WHERE d.处方ID = b.处方ID AND d.项目类别 IN (99,91))
                        AND NOT EXISTS (SELECT 1 FROM fghis5..处方结果本地表 r
                                        WHERE r.outcfcode IS NOT NULL AND CAST(r.outcfcode AS INT) = b.处方ID
                                          AND ISNULL(r.json_data,'') <> '')
                      GROUP BY b.处方ID
                      ORDER BY 日期 DESC, b.处方ID",
                    new SqlParameter("@b", b),
                    new SqlParameter("@e", e)).ToList();

                // 逐条推荐候选原处方（同门诊号、发票状态1、处方结果本地表有记录、日期最近）
                foreach (var p in list)
                {
                    var cand = db.Database.SqlQuery<UnmatchedPrescription>(
                        @"SELECT TOP 1 b2.处方ID AS 候选原处方ID,
                                  CONVERT(varchar(10), CONVERT(date, MAX(b2.日期)), 23) AS 候选原处方日期
                           FROM fghis5..门诊_收费明细表 b2
                           JOIN fghis5..门诊_收费发票表 a2 ON a2.结帐ID = b2.结帐ID
                           WHERE a2.发票状态 = '1' AND a2.门诊号 = @门诊号 AND b2.处方ID <> @处方ID
                             AND EXISTS (SELECT 1 FROM fghis5..处方结果本地表 r
                                         WHERE r.outcfcode IS NOT NULL AND CAST(r.outcfcode AS INT) = b2.处方ID
                                           AND ISNULL(r.json_data,'') <> '')
                           GROUP BY b2.处方ID
                           ORDER BY MAX(b2.日期) DESC, b2.处方ID",
                        new SqlParameter("@门诊号", (object)p.门诊号 ?? DBNull.Value),
                        new SqlParameter("@处方ID", p.处方ID)).FirstOrDefault();
                    if (cand != null)
                    {
                        p.候选原处方ID = cand.候选原处方ID;
                        p.候选原处方日期 = cand.候选原处方日期;
                    }
                }

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
        /// 替换：把「当前处方ID」用「原处方ID」在 处方结果本地表 补一条记录（复制原方 json，改 outcfcode/billdate）。
        /// POST /ReportForms/ReplacePrescription
        /// </summary>
        [HttpPost]
        public ActionResult ReplacePrescription(int 当前处方ID, int 原处方ID)
        {
            try
            {
                if (当前处方ID <= 0 || 原处方ID <= 0 || 当前处方ID == 原处方ID)
                    return Json(new { success = false, msg = "参数不正确" });

                // 1. 原处方必须有处方结果记录
                var src = db.Database.SqlQuery<DispenseJsonRecord>(
                    @"SELECT CAST(outcfcode AS INT) AS 处方ID, json_data AS content_json
                      FROM fghis5..处方结果本地表 WHERE outcfcode = @原",
                    new SqlParameter("@原", 原处方ID.ToString())).FirstOrDefault();
                if (src == null || string.IsNullOrWhiteSpace(src.content_json))
                    return Json(new { success = false, msg = "原处方 " + 原处方ID + " 在处方结果本地表没有记录，无法复制" });

                // 2. 当前处方不能已有记录（避免重复补）
                var exists = db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM fghis5..处方结果本地表 WHERE outcfcode = @当前",
                    new SqlParameter("@当前", 当前处方ID.ToString())).FirstOrDefault();
                if (exists > 0)
                    return Json(new { success = false, msg = "处方 " + 当前处方ID + " 已有处方结果记录，无需替换" });

                // 3. 当前处方的收费日期（作为 billdate）
                var 日期 = db.Database.SqlQuery<string>(
                    @"SELECT TOP 1 CONVERT(varchar(10), CONVERT(date, b.日期), 23)
                      FROM fghis5..门诊_收费明细表 b WHERE b.处方ID = @处方ID",
                    new SqlParameter("@处方ID", 当前处方ID)).FirstOrDefault();
                if (string.IsNullOrEmpty(日期))
                    日期 = DateTime.Today.ToString("yyyy-MM-dd");

                // 4. 复制 json：outcfcode 换成当前处方ID，billdate 换成当前日期
                var 新json = src.content_json;
                新json = Regex.Replace(新json, "\"outcfcode\"\\s*:\\s*\"[^\"]*\"", "\"outcfcode\":\"" + 当前处方ID + "\"");
                新json = Regex.Replace(新json, "\"billdate\"\\s*:\\s*\"[^\"]*\"", "\"billdate\":\"" + 日期 + "\"");

                // 5. 插入
                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO fghis5..处方结果本地表 (outcfcode, billdate, json_data, 查询时间)
                      VALUES (@outcfcode, @billdate, @json_data, GETDATE())",
                    new SqlParameter("@outcfcode", 当前处方ID.ToString()),
                    new SqlParameter("@billdate", 日期),
                    new SqlParameter("@json_data", 新json));

                var expr = ExtractExpressNumber(新json);
                return Json(new { success = true, msg = "补录成功：处方 " + 当前处方ID +
                    " 已复制自 " + 原处方ID +
                    (string.IsNullOrEmpty(expr) ? "" : "，快递单号 " + expr) +
                    "（billdate " + 日期 + "）" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = "替换失败：" + inner.Message });
            }
        }

        /// <summary>
        /// 从处方结果 JSON 提取快递单号 expressnumber
        /// </summary>
        private string ExtractExpressNumber(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            var m = Regex.Match(json, "\"expressnumber\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }


        //各医生查询
        public ActionResult DoctorInfo()
        {
            return View();
        }

        public ActionResult GetDoctorInfo()
        {
            var name = Request["name"].Trim();
            var bdate = Request["bdatepicker"];
            var edate = Request["edatepicker"];


            string sqlQuery = @"SET ARITHABORT ON; 
                       SET ANSI_WARNINGS ON;
                       SET ANSI_NULLS ON;
                       SET ANSI_PADDING ON;
                       SET CONCAT_NULL_YIELDS_NULL ON;
WITH sfrbb AS (
	SELECT
    COALESCE(m.就诊id, m2.就诊id) AS 就诊id,
    a.PID,
    a.门诊号,
    a.姓名,
    a.出生日期,
    a.身份证号,
    b.结帐ID,
    b.结帐日期,
    b.结帐时间,
    b.发票状态,
    b.操作工号,
    b.工作组号 AS 关联销售,
    STUFF((
        SELECT DISTINCT ';' + c2.医生姓名
        FROM (
            SELECT DISTINCT
                a1.结帐ID,
                d1.医生姓名
            FROM
                fghis5..门诊_收费明细表 a1
                LEFT JOIN fghis5..门诊_收费处方表 f1 ON f1.结帐ID = a1.结帐ID AND a1.处方id = f1.处方id
                LEFT JOIN fghis5..系统_医生信息表 d1 ON f1.医生ID = d1.医生ID
            WHERE a1.结帐ID = ABS(b.结帐ID)
        ) c2
        WHERE c2.医生姓名 IS NOT NULL
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') as 接诊医生,
    b.总金额,
    SUM(CASE WHEN c.类别名称 = '诊查费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 诊查费,
    SUM(CASE WHEN c.类别名称 = '材料费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 材料费,
    SUM(CASE WHEN c.类别名称 = '检查费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 检查费,
    SUM(CASE WHEN c.类别名称 = '检验费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 检验费,
    SUM(CASE WHEN c.类别名称 = '治疗费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 治疗费,
    SUM(CASE WHEN c.类别名称 = '中草药' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 中草药,
    SUM(CASE WHEN c.类别名称 = '工艺品' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 工艺品,
    SUM(CASE WHEN c.类别名称 = '理疗费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 理疗费,
    SUM(CASE WHEN c.类别名称 = '加工费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 加工费,
    SUM(CASE WHEN c.类别名称 = '辨证论治费' THEN SIGN(b.结帐ID)*c.金额 ELSE 0 END) AS 辨证论治费,
    '收费' AS 类别,
    ISNULL(d.现金, 0) AS 现金,
    ISNULL(d.POS, 0) AS POS,
    ISNULL(d.储值卡, 0) AS 储值卡,
    ISNULL(d.折扣, 0) AS 折扣,
    ISNULL(d.微信, 0) AS 微信,
    ISNULL(d.支付宝, 0) AS 支付宝, 
    -- 新增实际支付字段
    ISNULL(d.现金, 0) + ISNULL(d.POS, 0) + ISNULL(d.储值卡, 0) + 
    ISNULL(d.微信, 0) + ISNULL(d.支付宝, 0) AS 实际支付
FROM
    fghis5..系统_病人基本信息表 a
    LEFT JOIN fghis5..门诊_收费发票表 b ON a.门诊号 = b.门诊号
    LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_收费发票表_结帐ID GROUP BY 结帐ID) m ON b.结帐ID = m.结帐ID
    LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_收费发票表_结帐ID GROUP BY 结帐ID) m2 ON m2.结帐ID = ABS(b.结帐ID)
    LEFT JOIN (
        SELECT
            a.结帐ID,
            b.类别名称,
            SUM(a.金额) AS 金额 
        FROM
            fghis5..门诊_收费明细表 a
            LEFT JOIN fghis5..代码_项目基本类别表 b ON a.项目类别 = b.项目类别 
        GROUP BY
            a.结帐ID,
            b.类别名称
    ) c ON ABS(b.结帐ID) = c.结帐ID
    LEFT JOIN (
        SELECT
            结帐ID,
            SUM(CASE WHEN 支付方式 = 0 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END) AS 现金,
            SUM(CASE WHEN 支付方式 = 1 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END) AS POS,
            SUM(CASE WHEN 支付方式 = 4 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END) AS 储值卡,
            SUM(CASE WHEN 支付方式 = 6 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END) AS 折扣,
            SUM(CASE WHEN 支付方式 = 31 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END) AS 微信,
            SUM(CASE WHEN 支付方式 = 32 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END) AS 支付宝 
        FROM
            fghis5..门诊_收费支付表 
        WHERE
            支付方式 IN (0, 1, 4, 6, 31, 32) 
        GROUP BY
            结帐ID 
    ) d ON b.结帐ID = d.结帐ID 
WHERE
    (b.操作工号 != '6666' OR b.发票状态 IN ('0','1')) 
    AND b.结帐日期 BETWEEN @bdate AND @edate 
    AND ( @name = '' OR a.姓名 = @name ) 
    AND b.发票状态 IN ('2','1','0') 
    AND a.姓名 NOT LIKE '%测试%' 
    GROUP BY
    COALESCE(m.就诊id, m2.就诊id),
    a.PID,
    a.门诊号,
    a.姓名,
    a.出生日期,
    a.身份证号,
    b.结帐ID,
    b.结帐日期,
    b.结帐时间,
    b.发票状态,
    b.操作工号,
    b.工作组号,
    b.总金额,
    d.现金,
    d.POS,
    d.储值卡,
    d.折扣,
    d.微信,
    d.支付宝 
		UNION ALL
	SELECT
		COALESCE(gm.就诊id, gm2.就诊id) AS 就诊id,
		a.PID,
		a.门诊号,
		a.姓名,
		a.出生日期,
		a.身份证号,
		b.结帐ID,
		b.结帐日期,
		b.结帐时间,
		b.发票状态,
		b.操作工号,
		'' AS 关联销售,
	 COALESCE(f.接诊医生姓名,'') as 接诊医生,
		b.总金额,
		b.总金额 AS 诊查费,
		0 AS 材料费,
		0 AS 检查费,
		0 AS 检验费,
		0 AS 治疗费,
		0 AS 中草药,
		0 AS 工艺品,
		0 AS 理疗费,
		0 AS 加工费,
		0 as 辨证论治费,
		'挂号' AS 类别,
		ISNULL( d.现金, 0 ) AS 现金,
		ISNULL( d.POS, 0 ) AS POS,
		ISNULL( d.储值卡, 0 ) AS 储值卡,
		ISNULL( d.折扣, 0 ) AS 折扣,
		ISNULL( d.微信, 0 ) AS 微信,
		ISNULL( d.支付宝, 0 ) AS 支付宝, 
		
		ISNULL( d.现金, 0 ) + ISNULL( d.POS, 0 ) + ISNULL( d.储值卡, 0 ) + 
    ISNULL( d.微信, 0 ) + ISNULL( d.支付宝, 0 ) AS 实际支付
		
	FROM
		fghis5..系统_病人基本信息表 a
	LEFT JOIN fghis5..门诊_挂号发票表 b ON a.门诊号 = b.门诊号
		LEFT JOIN (
		SELECT
			结帐ID,
			SUM ( CASE WHEN 支付方式 = 0 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 现金,
			SUM ( CASE WHEN 支付方式 = 1 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS POS,
			SUM ( CASE WHEN 支付方式 = 4 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 储值卡,
			SUM ( CASE WHEN 支付方式 = 6 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 折扣,
			SUM ( CASE WHEN 支付方式 = 31 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 微信,
			SUM ( CASE WHEN 支付方式 = 32 AND ABS(支付金额) >= 0.01 THEN 支付金额 ELSE 0 END ) AS 支付宝 
		FROM
			fghis5..门诊_挂号支付表 
		WHERE
			支付方式 IN ( 0, 1, 4, 6, 31, 32 ) 
		GROUP BY
			结帐ID 
		) d ON b.结帐ID = d.结帐ID 
	LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_挂号明细表 GROUP BY 结帐ID) gm ON b.结帐ID = gm.结帐ID
	LEFT JOIN (SELECT 结帐ID, MAX(就诊id) AS 就诊id FROM fghis5..门诊_挂号明细表 GROUP BY 结帐ID) gm2 ON gm2.结帐ID = ABS(b.结帐ID)
	left join fghis5..门诊_挂号信息表 f on f.就诊ID = COALESCE(gm.就诊id, gm2.就诊id)
	WHERE
	(b.操作工号 != '6666' OR b.发票状态 IN ('0','1')) 
	AND b.结帐日期 BETWEEN @bdate AND @edate 
	AND ( @name = '' OR a.姓名 = @name ) 
	AND b.发票状态 IN ('2','1','0') 
	AND a.姓名 NOT LIKE '%测试%' 
	) 
	
SELECT * FROM (

  SELECT
    结帐日期,
    COALESCE(接诊医生,'') 接诊医生,
    sum(总金额) 总金额,
    sum(诊查费) 诊查费,
    sum(材料费) 材料费,
    sum(检查费) 检查费,
    sum(检验费) 检验费,
    sum(治疗费) 治疗费,
    sum(中草药) 中草药,
    sum(工艺品) 工艺品,
    sum(理疗费) 理疗费,
    sum(加工费) 加工费,
    sum(辨证论治费) 辨证论治费,
    sum(现金) 现金,
    sum(POS) POS,
    sum(储值卡) 储值卡,
    sum(折扣) 折扣,
    sum(微信) 微信,
    sum(支付宝) 支付宝,
    sum(实际支付) 实际支付
  FROM sfrbb 
  GROUP BY 结帐日期, COALESCE(接诊医生,'')
  
  UNION ALL
  
  SELECT
    '总计' as 结帐日期,
    '' as 接诊医生,
    sum(总金额) 总金额,
    sum(诊查费) 诊查费,
    sum(材料费) 材料费,
    sum(检查费) 检查费,
    sum(检验费) 检验费,
    sum(治疗费) 治疗费,
    sum(中草药) 中草药,
    sum(工艺品) 工艺品,
    sum(理疗费) 理疗费,
    sum(加工费) 加工费,
    sum(辨证论治费) 辨证论治费,
    sum(现金) 现金,
    sum(POS) POS,
    sum(储值卡) 储值卡,
    sum(折扣) 折扣,
    sum(微信) 微信,
    sum(支付宝) 支付宝,
    sum(实际支付) 实际支付
  FROM sfrbb
) tt 
WHERE 总金额 != 0
ORDER BY 
  CASE WHEN 结帐日期 = '总计' THEN 1 ELSE 0 END, 
  结帐日期 DESC";

            var result = db.Database.SqlQuery<DoctorInfos>(sqlQuery, QueryHelper.BuildReportParams(name, bdate, edate)).ToList();

            return View("DoctorInfo", result);
        }


        //药品
        public ActionResult Medicament()
        {
            return View();
        }

        public ActionResult GetMedicament()
        {
            var name = Request["name"].Trim();
            var bdate = Request["bdatepicker"];
            var edate = Request["edatepicker"];


            string sqlQuery = @"SELECT 
    c.药品ID,
    c.药品通用ID,
    c.药品通用名,
    c.药品名称,
		c.注册商标 as 商品编号,
		c.药品条码 as 新医保编码,
    CASE WHEN RIGHT(c.药品名称, 1) = N'△' THEN N'80%' ELSE N'100%' END as 折后比例,
    c.基本零售价,
    c.常规批发价,
    CAST(d.采购价 AS DECIMAL(18,4)) as 采购价,
    SUM(b.数量) as 数量,
    SUM(b.金额) as 总售价,
    CAST(c.常规批发价 * SUM(b.数量) AS DECIMAL(18,4)) as 总批发价,
    CAST(CAST(d.采购价 AS DECIMAL(18,4)) * SUM(b.数量) AS DECIMAL(18,4)) as 总成本
FROM fghis5..门诊_收费发票表 a 
JOIN fghis5..门诊_收费明细表 b ON a.结帐ID = b.结帐ID
LEFT JOIN fghis5..代码_药品基本信息表 c ON b.项目ID = c.药品ID
LEFT JOIN fghis5..中药采购价 d ON d.药品通用ID = c.药品通用ID
WHERE a.发票状态 = '2' 
    AND b.项目类别 IN (3)
    and a.结帐日期 between @bdate and @edate
		and a.操作工号 != '6666'
		and (@name='' or  c.药品名称 = @name)	
GROUP BY 
    c.药品ID,
    c.药品通用ID,
    c.药品通用名,
    c.药品名称,
		c.注册商标,
		c.药品条码,
    c.基本零售价,
    c.常规批发价,
    d.采购价

union all 
select c.项目ID as 药品ID,c.项目ID  药品通用ID,c.项目名称 as 药品通用名,c.项目名称 as 药品名称,'' 商品编号,'' 新医保编码,''折后比列,c.单价 as 基本零售价,0 常规批发价,0 as 采购价,sum(b.数量) as 数量,sum(b.金额) as 总售价,0 as 总批发价,0 as 总成本
from fghis5..门诊_收费发票表 a 
left join fghis5..门诊_收费明细表 b on a.结帐ID=b.结帐ID
left join fghis5..代码_收费项目表 c on b.项目ID=c.项目ID
where a.发票状态='2' and b.项目类别 in (99)
and a.结帐日期 between @bdate and @edate
and a.操作工号 != '6666'
and (@name='' or c.项目名称 = @name)
group by c.项目ID ,c.项目代码,c.项目名称,c.单价,c.成本价";

            var result = db.Database.SqlQuery<Medicaments>(sqlQuery, QueryHelper.BuildReportParams(name, bdate, edate)).ToList();

            return View("Medicament", result);
        }


        // 接诊明细查询
        public ActionResult Reception()
        {
            return View();
        }


        //输入PID或姓名，按日期查询接诊明细
        [HttpGet]
        public ActionResult GetReception()
        {
            var PID = Request["PID"].Trim();
            var bdate = Request["bdatepicker"];
            var edate = Request["edatepicker"];

            string sqlQuery = @"select 就诊ID,交易卡号,姓名,接诊医生工号,接诊医生姓名 from fghis5..门诊_挂号信息表
where 挂号日期 between @bdate and @edate
and 操作员工号 !='6666'
and (@name='' or 交易卡号=@name or 姓名 = @name)
order by 挂号时间 desc  ";

            var result = db.Database.SqlQuery<JZMX>(sqlQuery, QueryHelper.BuildReportParams(PID, bdate, edate)).ToList();

                return View("Reception", result);
                
        }

        // 项目报表
        public ActionResult MannerInfo()
        {
            return View();
        }

        [HttpGet]
        public ActionResult GetMannerInfo()
        {
            var name = Request["name"].Trim();
            var bdate = Request["bdatepicker"];
            var edate = Request["edatepicker"];

            string sqlQuery = @"select a.项目名称,a.单价,sum(a.数量) 数量,sum(a.金额) 金额
from fghis5..门诊_收费明细表 a
left join fghis5..代码_收费项目表 b on a.项目id=b.项目id
left join fghis5..门诊_收费发票表 c on a.结帐id=c.结帐id
where b.项目id < 242
and c.发票状态='2' and c.结帐日期 between @bdate and @edate
and (@name='' or a.项目名称 = @name)
group by a.项目名称,a.单价";

            var result = db.Database.SqlQuery<Manners>(sqlQuery, QueryHelper.BuildReportParams(name, bdate, edate)).ToList();

            return View("MannerInfo", result);
        }


    }


}