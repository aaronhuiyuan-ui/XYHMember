using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    // 第三方（OA）查询接口 —— 免登录，用 Web.config 的 OaApiKey 校验
    // 注意：本控制器不挂 [AuthFilter]，故不在登录会话内也能访问
    public class OaApiController : Controller
    {
        /// <summary>
        /// 查询上海真仁堂统计汇总
        /// POST /OaApi/GetZhenRenTangStats
        /// 鉴权（任选其一）：
        ///   1. 请求头 X-Api-Key: &lt;密钥&gt;（兼容 Authorization: Bearer &lt;密钥&gt;）
        ///   2. 查询字符串或表单 key=&lt;密钥&gt;（兼容 OA 代理不便传 Header 的场景）
        /// 可选参数：month=2026-08（查询字符串或表单；省略返回全部月份，按月份倒序）
        /// </summary>
        [HttpPost]
        public ActionResult GetZhenRenTangStats(string month = null, string key = null)
        {
            var apiKey = ConfigurationManager.AppSettings["OaApiKey"];
            var k = Request.Headers["X-Api-Key"];
            if (string.IsNullOrEmpty(k))
            {
                var auth = Request.Headers["Authorization"];
                if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    k = auth.Substring("Bearer ".Length).Trim();
            }
            // 兼容：密钥放查询字符串或表单（?key=... 或 body key=...）
            if (string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(key))
                k = key.Trim();
            if (string.IsNullOrEmpty(k) || k != apiKey)
                return Json(new { success = false, msg = "无效的访问密钥" });

            try
            {
                using (var db = new XYHDbContext())
                {
                    var sql = @"SELECT 序号, 月份, 开始日期, 结束日期,
                                       应付加工费, 应付快递费, 应付药品费, 导入时间, 应付总金额
                                FROM fghis5..上海真仁堂统计汇总";
                    var prms = new List<SqlParameter>();
                    if (!string.IsNullOrEmpty(month))
                    {
                        sql += " WHERE 月份 = @month";
                        prms.Add(new SqlParameter("@month", month));
                    }
                    sql += " ORDER BY 月份 DESC";
                    var data = db.Database.SqlQuery<ZhenRenTangStat>(sql, prms.ToArray()).ToList();
                    return Json(new { success = true, data });
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
        /// 套餐耗材每日自动扣减（供 Windows 计划任务调用，免登录）
        /// POST /OaApi/RunPackageAutoDeduct
        /// 鉴权（任选其一）：1. 请求头 X-Api-Key / Authorization: Bearer；2. 查询字符串或表单 key=
        /// 可选参数：bdate、edate（yyyy-MM-dd）；省略默认扣减昨天，显式传区间可补跑。
        /// </summary>
        [HttpPost]
        public ActionResult RunPackageAutoDeduct(string bdate = null, string edate = null, string key = null)
        {
            var apiKey = ConfigurationManager.AppSettings["OaApiKey"];
            var k = Request.Headers["X-Api-Key"];
            if (string.IsNullOrEmpty(k))
            {
                var auth = Request.Headers["Authorization"];
                if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    k = auth.Substring("Bearer ".Length).Trim();
            }
            if (string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(key))
                k = key.Trim();
            if (string.IsNullOrEmpty(k) || k != apiKey)
                return Json(new { success = false, msg = "无效的访问密钥" });

            try
            {
                var yesterday = DateTime.Today.AddDays(-1);
                var b = string.IsNullOrWhiteSpace(bdate) ? yesterday : DateTime.Parse(bdate);
                var e = string.IsNullOrWhiteSpace(edate) ? yesterday : DateTime.Parse(edate);
                var sum = PackageAutoDeductService.Run(b, e, "系统");
                return Json(new { success = true, data = sum });
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
