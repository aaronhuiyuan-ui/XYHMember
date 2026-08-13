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
        /// 请求头：X-Api-Key: &lt;密钥&gt;（兼容 Authorization: Bearer &lt;密钥&gt;）
        /// 可选参数：month=2026-08（查询字符串或表单；省略返回全部月份，按月份倒序）
        /// </summary>
        [HttpPost]
        public ActionResult GetZhenRenTangStats(string month = null)
        {
            var apiKey = ConfigurationManager.AppSettings["OaApiKey"];
            var key = Request.Headers["X-Api-Key"];
            if (string.IsNullOrEmpty(key))
            {
                var auth = Request.Headers["Authorization"];
                if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    key = auth.Substring("Bearer ".Length).Trim();
            }
            if (string.IsNullOrEmpty(key) || key != apiKey)
                return Json(new { success = false, msg = "无效的访问密钥" });

            try
            {
                using (var db = new XYHDbContext())
                {
                    var sql = "SELECT 序号, 月份, 应付加工费, 应付快递费, 导入时间 FROM fghis5..上海真仁堂统计汇总";
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
    }
}
