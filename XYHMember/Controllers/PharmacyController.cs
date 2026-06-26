using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    /// <summary>
    /// 门诊发药管理控制器 — 待发药查询、确认发药、已发药查询、退药、处方结果查询
    /// 所有接口需登录认证（[AuthFilter]）
    /// </summary>
    [AuthFilter]
    public class PharmacyController : Controller
    {
        private XYHDbContext db = new XYHDbContext();

        /// <summary>外部API基础地址</summary>
        private static string ApiBaseUrl { get { return ConfigurationManager.AppSettings["ApiBaseUrl"]; } }
        /// <summary>API接口appId（从Web.config读取）</summary>
        private static string AppId { get { return ConfigurationManager.AppSettings["ApiAppId"]; } }
        /// <summary>API接口appSecret（从Web.config读取，签名用）</summary>
        private static string AppSecret { get { return ConfigurationManager.AppSettings["ApiAppSecret"]; } }
        /// <summary>机构编码（从Web.config读取）</summary>
        private static string OrgCode { get { return ConfigurationManager.AppSettings["ApiOrgCode"]; } }
        /// <summary>客户编码（从Web.config读取）</summary>
        private static string CustomerCode { get { return ConfigurationManager.AppSettings["ApiCustomerCode"]; } }
        /// <summary>校验码原文（从Web.config读取，代码中执行MD5转换）</summary>
        private static string CheckCodeRaw { get { return ConfigurationManager.AppSettings["ApiCheckCode"]; } }
        /// <summary>校验码MD5值（由CheckCodeRaw动态计算）</summary>
        private static string CheckCodeMd5 { get { return ComputeMD5(CheckCodeRaw); } }

        // =====================================================================
        //  待发药查询
        // =====================================================================

        /// <summary>
        /// 门诊待发药查询页面（GET）
        /// </summary>
        public ActionResult DispensingQuery()
        {
            return View();
        }

        /// <summary>
        /// 确认发药独立页面（GET，取代JS弹框）
        /// 多处方连续确认时通过 index 参数切换当前显示第几个
        /// </summary>
        /// <param name="prescriptionIds">逗号分隔的处方ID列表</param>
        /// <param name="index">当前要确认的处方在列表中的索引（从0开始）</param>
        [HttpGet]
        public ActionResult DispensingConfirm(string prescriptionIds, int index = 0)
        {
            Response.ContentType = "text/html; charset=utf-8";
            Response.ContentEncoding = System.Text.Encoding.UTF8;

            if (string.IsNullOrEmpty(prescriptionIds))
                return RedirectToAction("DispensingQuery");

            // 解析逗号分隔的处方ID列表
            var pidList = new List<int>();
            foreach (var s in prescriptionIds.Split(','))
            {
                int id;
                if (int.TryParse(s.Trim(), out id) && id > 0)
                    pidList.Add(id);
            }
            if (pidList.Count == 0 || index < 0 || index >= pidList.Count)
                return RedirectToAction("DispensingQuery");

            ViewBag.PrescriptionIds = string.Join(",", pidList);
            ViewBag.CurrentIndex = index;
            ViewBag.TotalCount = pidList.Count;

            var currentId = pidList[index];

            try
            {
                // 查询处方头（从门诊中药电子处方上传）
                var headerSql = @"SELECT
    CAST(outcfcode AS INT) AS 处方ID,
    CAST(outcfcode AS NVARCHAR(50)) AS outcfcode,
    CAST(outcfsn AS NVARCHAR(50)) AS outcfsn,
    department, jyyq, jynum, zgyq, cftype, agentnum, bags, packagenum,
    patient, CAST(age AS NVARCHAR(50)) AS age, jyplan, sex, ispregnancy, telephone, deliveryaddr,
    client, remark, CAST(billdate AS NVARCHAR(10)) AS billdate,
    doctor, CAST(patientcode AS NVARCHAR(50)) AS patientcode, customername, sendmethod, totalprice, diagnosis,
    CAST(medicalno AS NVARCHAR(50)) AS medicalno, hcysource, expresstradeno,
    CONVERT(varchar, birthdate, 23) AS birthdate, recipelurl,
    recipelurltype, paymethod, medicalhistory, bringbackflag, isurgent, iscopy
FROM fghis5..门诊中药电子处方上传
WHERE outcfcode = @pid";

                var headers = db.Database.SqlQuery<DispenseHeaderResult>(headerSql,
                    new SqlParameter("@pid", currentId)).ToList();

                if (headers.Count == 0)
                    return Content("未查询到处方数据", "text/html; charset=utf-8");

                // 查询处方明细（饮片明细）
                var detailSql = @"SELECT
    处方ID, CAST(dosage AS NVARCHAR(50)) AS dosage,
    goodscode, goodsname, tpyq, goodsspec, goodsunit, manufacturer
FROM fghis5..门诊中药明细电子处方上传
WHERE 处方ID = @pid";

                var details = db.Database.SqlQuery<DispenseDetailResult>(detailSql,
                    new SqlParameter("@pid", currentId)).ToList();

                var model = new DispensingConfirmViewModel
                {
                    Header = headers[0],
                    Details = details
                };

                return View(model);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Content("获取数据失败：" + inner.Message, "text/html; charset=utf-8");
            }
        }

        /// <summary>
        /// 查询待发药列表（GET）
        /// 从医生_处方流水帐按日期、姓名过滤未发药的已收费处方
        /// </summary>
        [HttpGet]
        public ActionResult GetDispensingQuery()
        {
            var name = Request["name"]?.Trim() ?? "";
            var bdate = Request["bdatepicker"]?.Trim();
            var edate = Request["edatepicker"]?.Trim();
            if (string.IsNullOrEmpty(bdate)) bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate)) edate = DateTime.Today.ToString("yyyy-MM-dd");

            string sqlQuery = @"
SELECT c.结帐ID, a.处方ID, a.门诊号, a.姓名, a.开方日期, a.开方时间,
       CAST(a.医生工号 AS NVARCHAR(50)) AS 医生工号, d.医生姓名, a.草药帖数, a.总金额,
       CASE WHEN c.发票状态=2 THEN N'已收费' ELSE N'已退费' END AS 发票状态
FROM fghis5..医生_处方流水帐 a
JOIN fghis5..门诊_收费处方表 b ON a.处方ID=b.处方ID
JOIN fghis5..门诊_收费发票表 c ON b.结帐ID=c.结帐ID
JOIN fghis5..系统_医生信息表 d ON d.医生工号=a.医生工号
WHERE a.处方类型='2' AND a.状态='3'
  AND c.发票状态='2'
  AND a.处方ID NOT IN (SELECT DISTINCT 处方ID FROM fghis5..门诊_发药信息表)
  AND a.处方ID NOT IN (SELECT DISTINCT 处方ID FROM fghis5..门诊_收费明细表 WHERE 项目名称 LIKE '△%')
  AND a.开方日期 BETWEEN @bdate AND @edate
  AND (@name = '' OR a.姓名 = @name)
ORDER BY c.结帐ID DESC, a.处方ID DESC";

            var result = db.Database.SqlQuery<PharmacyDispensing>(sqlQuery,
                QueryHelper.BuildReportParams(name, bdate, edate)).ToList();

            return View("DispensingQuery", result);
        }

        // =====================================================================
        //  确认发药
        // =====================================================================

        /// <summary>
        /// 确认发药（POST）
        /// 1.查询处方头+明细 → 2.构建API完整JSON → 3.调用uploadPrescription接口 →
        /// 4.API成功则保存发药记录到门诊_发药信息表，失败则返回API错误信息
        /// </summary>
        [HttpPost]
        public ActionResult ConfirmDispense()
        {
            // 分步追踪：找出卡在哪一步
            var step = 0;

            var input = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
            var req = JObject.Parse(input);
            var prescriptionId = req["prescriptionId"]?.Value<int>() ?? 0;
            var fields = req["fields"] as JObject;

            if (prescriptionId <= 0)
                return Json(new { success = false, msg = "处方ID无效" });

            step = 1;
            // 获取当前操作用户信息
            var userId = ((int?)Session["UserId"]) ?? 1;
            var user = db.Users.Find(userId);
            var jobNumber = user?.JobNumber ?? "";
            var userName = user?.Name ?? "";

            try
            {
                step = 2;
                // 1. 查询处方头
                var headerSql = @"SELECT
    CAST(outcfcode AS INT) AS 处方ID,
    CAST(outcfcode AS NVARCHAR(50)) AS outcfcode,
    CAST(outcfsn AS NVARCHAR(50)) AS outcfsn,
    department, jyyq, jynum, zgyq, cftype, agentnum, bags, packagenum,
    patient, CAST(age AS NVARCHAR(50)) AS age, jyplan, sex, ispregnancy, telephone, deliveryaddr,
    client, remark, CAST(billdate AS NVARCHAR(10)) AS billdate,
    doctor, CAST(patientcode AS NVARCHAR(50)) AS patientcode, customername, sendmethod, totalprice, diagnosis,
    CAST(medicalno AS NVARCHAR(50)) AS medicalno, hcysource, expresstradeno,
    CONVERT(varchar, birthdate, 23) AS birthdate, recipelurl,
    recipelurltype, paymethod, medicalhistory, bringbackflag, isurgent, iscopy
FROM fghis5..门诊中药电子处方上传
WHERE outcfcode = @pid";

                var headers = db.Database.SqlQuery<DispenseHeaderResult>(headerSql,
                    new SqlParameter("@pid", prescriptionId)).ToList();

                if (headers.Count == 0)
                    return Json(new { success = false, msg = "未查询到处方数据", step = step });

                var header = headers[0];
                // 使用固定值覆盖数据库值（接口提供方要求）
                header.orgcode = OrgCode;
                header.checkcode = CheckCodeMd5;
                header.customercode = CustomerCode;

                step = 3;
                // 2. 查询处方明细
                var detailSql = @"SELECT
    处方ID, CAST(dosage AS NVARCHAR(50)) AS dosage,
    goodscode, goodsname, tpyq, goodsspec, goodsunit, manufacturer
FROM fghis5..门诊中药明细电子处方上传
WHERE 处方ID = @pid";

                var details = db.Database.SqlQuery<DispenseDetailResult>(detailSql,
                    new SqlParameter("@pid", prescriptionId)).ToList();

                // 3. 构建API完整JSON
                var jHeader = JObject.FromObject(header);
                jHeader.Remove("处方ID");

                // 4. 合并前端修改的字段
                if (fields != null)
                {
                    foreach (var prop in fields.Properties())
                    {
                        jHeader[prop.Name] = prop.Value;
                    }
                }

                // totalprice 转为数字（API要求json中为number类型）
                if (jHeader["totalprice"] != null)
                {
                    decimal totalpriceVal;
                    if (decimal.TryParse(jHeader["totalprice"]?.ToString(), out totalpriceVal))
                        jHeader["totalprice"] = totalpriceVal;
                }

                // 5. 添加明细列表
                var mxList = new JArray();
                foreach (var d in details)
                {
                    var jd = JObject.FromObject(d);
                    jd.Remove("处方ID");
                    mxList.Add(jd);
                }
                jHeader["hisSellKpMxList"] = mxList;

                var jsonContent = jHeader.ToString(Newtonsoft.Json.Formatting.None);

                // 6. 调用外部上传接口
                step = 5;
                var apiResult = CallUploadApi(jsonContent);

                if (!apiResult.Item1)
                    return Json(new { success = false, msg = apiResult.Item2, apiResponse = apiResult.Item3, step = step });

                // 7. API成功，保存到本地数据库
                step = 6;
                var sql = @"INSERT INTO fghis5..门诊_发药信息表
                            (处方ID, 发药人工号, 发药人姓名, 发药时间, 发药状态, delete_flag, content_json)
                            VALUES (@处方ID, @发药人工号, @发药人姓名, GETDATE(), 3, 0, @content_json)";

                db.Database.ExecuteSqlCommand(sql,
                    new SqlParameter("@处方ID", prescriptionId),
                    new SqlParameter("@发药人工号", SqlDbType.NVarChar, 50) { Value = jobNumber ?? "" },
                    new SqlParameter("@发药人姓名", SqlDbType.NVarChar, 50) { Value = userName ?? "" },
                    new SqlParameter("@content_json", SqlDbType.NVarChar, -1) { Value = jsonContent ?? "" });

                return Json(new { success = true, msg = apiResult.Item2 });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message, step = step });
            }
        }

        // =====================================================================
        //  已发药查询
        // =====================================================================

        /// <summary>
        /// 已发药查询页面（GET）
        /// </summary>
        public ActionResult DispensedQuery()
        {
            return View();
        }

        /// <summary>
        /// 查询已发药列表（POST）
        /// 从门诊_发药信息表按日期范围查询，通过fn_JsonExtract从content_json提取病人姓名、处方日期、原始outcfcode
        /// </summary>
        [HttpPost]
        public ActionResult GetDispensedQuery()
        {
            var name = Request["name"]?.Trim() ?? "";
            var bdate = Request["bdatepicker"]?.Trim();
            var edate = Request["edatepicker"]?.Trim();

            if (string.IsNullOrEmpty(bdate)) bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate)) edate = DateTime.Today.ToString("yyyy-MM-dd");

            var sql = @"SELECT
    CAST(a.处方ID AS INT) AS 处方ID,
    fghis5.dbo.fn_JsonExtract(content_json, 'patient') AS 病人姓名,
    fghis5.dbo.fn_JsonExtract(content_json, 'billdate') AS 处方日期,
    fghis5.dbo.fn_JsonExtract(content_json, 'outcfcode') AS outcfcode_original,
    fghis5.dbo.fn_JsonExtract(content_json, 'client') AS 收货人,
    fghis5.dbo.fn_JsonExtract(content_json, 'telephone') AS 收货电话,
    fghis5.dbo.fn_JsonExtract(content_json, 'deliveryaddr') AS 收货地址,
    CAST(发药人工号 AS NVARCHAR(50)) AS 发药人工号, 发药人姓名, 发药时间, 发药状态,
    CASE WHEN c.发票状态=2 THEN '已收费' ELSE '已退费' END AS 发票状态,
    CONVERT(varchar, 发药时间, 23) AS 发药日期
FROM fghis5..门诊_发药信息表 a
JOIN fghis5..门诊_收费处方表 b ON a.处方ID=b.处方ID
JOIN fghis5..门诊_收费发票表 c ON b.结帐ID=c.结帐ID
WHERE 发药状态 = 3
  AND CONVERT(date, 发药时间) >= @bdate AND CONVERT(date, 发药时间) <= @edate
  AND delete_flag = 0
  AND (@name = '' OR fghis5.dbo.fn_JsonExtract(content_json, 'patient') LIKE '%' + @name + '%')
  AND a.处方ID NOT IN (SELECT DISTINCT 处方ID FROM fghis5..门诊_收费明细表 WHERE 项目名称 LIKE '△%')
ORDER BY 发药时间 DESC";

            var result = db.Database.SqlQuery<DispensedRecord>(sql,
                new SqlParameter("@bdate", bdate),
                new SqlParameter("@edate", edate),
                new SqlParameter("@name", name)).ToList();

            return View("DispensedQuery", result);
        }

        // =====================================================================
        //  已退药信息查询
        // =====================================================================

        /// <summary>
        /// 已退药查询页面（GET）
        /// </summary>
        public ActionResult CancelledQuery()
        {
            return View();
        }

        /// <summary>
        /// 查询已退药列表（POST）
        /// 查询发药状态=9（已退药）的记录
        /// </summary>
        [HttpPost]
        public ActionResult GetCancelledQuery()
        {
            var name = Request["name"]?.Trim() ?? "";
            var bdate = Request["bdatepicker"]?.Trim();
            var edate = Request["edatepicker"]?.Trim();

            if (string.IsNullOrEmpty(bdate)) bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate)) edate = DateTime.Today.ToString("yyyy-MM-dd");

            var sql = @"SELECT
    CAST(a.处方ID AS INT) AS 处方ID,
    fghis5.dbo.fn_JsonExtract(content_json, 'patient') AS 病人姓名,
    fghis5.dbo.fn_JsonExtract(content_json, 'billdate') AS 处方日期,
    fghis5.dbo.fn_JsonExtract(content_json, 'outcfcode') AS outcfcode_original,
    fghis5.dbo.fn_JsonExtract(content_json, 'client') AS 收货人,
    fghis5.dbo.fn_JsonExtract(content_json, 'telephone') AS 收货电话,
    fghis5.dbo.fn_JsonExtract(content_json, 'deliveryaddr') AS 收货地址,
    CAST(发药人工号 AS NVARCHAR(50)) AS 发药人工号, 发药人姓名, 发药时间, 发药状态,
    CASE WHEN c.发票状态=2 THEN '已收费' ELSE '已退费' END AS 发票状态,
    CONVERT(varchar, 发药时间, 23) AS 发药日期
FROM fghis5..门诊_发药信息表 a
JOIN fghis5..门诊_收费处方表 b ON a.处方ID=b.处方ID
JOIN fghis5..门诊_收费发票表 c ON b.结帐ID=c.结帐ID
WHERE 发药状态 = 9
  AND CONVERT(date, 发药时间) >= @bdate AND CONVERT(date, 发药时间) <= @edate
  AND delete_flag = 0
  AND (@name = '' OR fghis5.dbo.fn_JsonExtract(content_json, 'patient') LIKE '%' + @name + '%')
  AND a.处方ID NOT IN (SELECT DISTINCT 处方ID FROM fghis5..门诊_收费明细表 WHERE 项目名称 LIKE '△%')
ORDER BY 发药时间 DESC";

            var result = db.Database.SqlQuery<DispensedRecord>(sql,
                new SqlParameter("@bdate", bdate),
                new SqlParameter("@edate", edate),
                new SqlParameter("@name", name)).ToList();

            return View("CancelledQuery", result);
        }

        // =====================================================================
        //  退药
        // =====================================================================

        /// <summary>
        /// 退药（POST）
        /// 调用cancelPrescription接口撤销处方，成功后将发药状态更新为9
        /// </summary>
        [HttpPost]
        public ActionResult CancelDispensedPrescription()
        {
            var input = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
            var req = JObject.Parse(input);
            var prescriptionId = req["prescriptionId"]?.Value<int>() ?? 0;
            var outcfcode = req["outcfcode"]?.Value<string>() ?? "";
            var billdate = req["billdate"]?.Value<string>() ?? "";

            if (prescriptionId <= 0 || string.IsNullOrEmpty(outcfcode))
                return Json(new { success = false, msg = "处方ID无效" });

            try
            {
                // 构建退药请求参数（outcfcode和billdate由前端从content_json中提取后传入）
                var cancelBody = new JObject
                {
                    ["custcode"] = CustomerCode,
                    ["outcfcode"] = outcfcode,
                    ["checkcode"] = CheckCodeMd5,
                    ["billdate"] = billdate
                };

                // 调用退药接口
                var apiResult = CallCancelApi(cancelBody.ToString(Formatting.None));

                if (!apiResult.Item1)
                    return Json(new { success = false, msg = apiResult.Item2, apiResponse = apiResult.Item3 });

                // 退药成功，更新发药状态为9
                db.Database.ExecuteSqlCommand(
                    "UPDATE fghis5..门诊_发药信息表 SET 发药状态 = 9 WHERE 处方ID = @pid",
                    new SqlParameter("@pid", prescriptionId));

                return Json(new { success = true, msg = apiResult.Item2 });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        // =====================================================================
        //  处方结果查询
        // =====================================================================

        /// <summary>
        /// 查询处方结果信息（POST）
        /// 调用getPrescriptionInfo接口，根据前端传递的outcfcode获取API中的处方数据
        /// </summary>
        [HttpPost]
        public ActionResult GetPrescriptionDetail()
        {
            var input = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
            var req = JObject.Parse(input);
            var outcfcode = req["outcfcode"]?.Value<string>() ?? "";
            var billdate = req["billdate"]?.Value<string>() ?? "";

            if (string.IsNullOrEmpty(outcfcode))
                return Json(new { success = false, msg = "处方ID无效" });

            try
            {
                // 构建请求体（固定值）
                var requestBody = new JObject
                {
                    ["customercode"] = CustomerCode,
                    ["outcfcode"] = outcfcode,
                    ["checkcode"] = CheckCodeMd5
                };
                var jsonBody = requestBody.ToString(Formatting.None);

                // 计算签名
                var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                var sign = ComputeSign(jsonBody, timestamp, AppSecret);

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                // 调用处方结果查询接口
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/v2/prescription/getPrescriptionInfo");
                httpRequest.Headers.Add("appId", AppId);
                httpRequest.Headers.Add("timestamp", timestamp.ToString());
                httpRequest.Headers.Add("sign", sign);
                httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                var response = httpClient.SendAsync(httpRequest).ConfigureAwait(false).GetAwaiter().GetResult();
                var responseBody = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                var jResp = JObject.Parse(responseBody);

                var isSuccess = jResp["isSuccess"]?.Value<bool>() ?? false;

                if (!isSuccess)
                    return Json(new { success = false, msg = jResp["messageInfos"]?.ToString() ?? "查询失败", apiResponse = jResp.ToString() });

                var respObj = new { success = true, data = jResp["data"] };
                return Content(JsonConvert.SerializeObject(respObj), "application/json");
            }
            catch (TaskCanceledException)
            {
                return Json(new { success = false, msg = "查询处方结果API请求超时（30秒），请检查服务器网络是否能访问 " + ApiBaseUrl });
            }
            catch (HttpRequestException ex)
            {
                var detail = ex.Message;
                if (ex.InnerException != null)
                    detail += " | " + ex.InnerException.Message;
                return Json(new { success = false, msg = "查询处方结果网络请求失败: " + detail });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        // =====================================================================
        //  私有方法（API调用、加密）
        // =====================================================================

        /// <summary>
        /// 调用外部发药上传接口（POST /api/v2/prescription/uploadPrescription）
        /// </summary>
        /// <param name="jsonBody">请求JSON体</param>
        /// <returns>Tuple(isSuccess, messageInfos, fullResponse)</returns>
        private Tuple<bool, string, string> CallUploadApi(string jsonBody)
        {
            try
            {
                var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                var sign = ComputeSign(jsonBody, timestamp, AppSecret);

                // 支持TLS 1.2（HTTPS需要）
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/v2/prescription/uploadPrescription");
                request.Headers.Add("appId", AppId);
                request.Headers.Add("timestamp", timestamp.ToString());
                request.Headers.Add("sign", sign);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30); // 30秒超时，防止一直挂起
                var response = httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
                var responseBody = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                var jResp = JObject.Parse(responseBody);

                var isSuccess = jResp["isSuccess"]?.Value<bool>() ?? false;
                var msg = jResp["messageInfos"]?.ToString() ?? responseBody;

                return Tuple.Create(isSuccess, msg, jResp.ToString());
            }
            catch (TaskCanceledException)
            {
                return Tuple.Create(false, "API请求超时（30秒），请检查服务器网络是否能访问 " + ApiBaseUrl, "");
            }
            catch (HttpRequestException ex)
            {
                var detail = ex.Message;
                if (ex.InnerException != null)
                    detail += " | " + ex.InnerException.Message;
                return Tuple.Create(false, "网络请求失败: " + detail, "");
            }
            catch (Exception ex)
            {
                var detail = ex.Message;
                if (ex.InnerException != null)
                    detail += " | Inner: " + ex.InnerException.Message;
                return Tuple.Create(false, detail, "");
            }
        }

        /// <summary>
        /// 调用外部退药接口（POST /api/v2/prescription/cancelPrescription）
        /// </summary>
        /// <param name="jsonBody">请求JSON体</param>
        /// <returns>Tuple(isSuccess, messageInfos, fullResponse)</returns>
        private Tuple<bool, string, string> CallCancelApi(string jsonBody)
        {
            try
            {
                var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                var sign = ComputeSign(jsonBody, timestamp, AppSecret);

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/v2/prescription/cancelPrescription");
                request.Headers.Add("appId", AppId);
                request.Headers.Add("timestamp", timestamp.ToString());
                request.Headers.Add("sign", sign);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30); // 30秒超时，防止一直挂起
                var response = httpClient.SendAsync(request).ConfigureAwait(false).GetAwaiter().GetResult();
                var responseBody = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                var jResp = JObject.Parse(responseBody);

                var isSuccess = jResp["isSuccess"]?.Value<bool>() ?? false;
                var msg = jResp["messageInfos"]?.ToString() ?? responseBody;

                return Tuple.Create(isSuccess, msg, jResp.ToString());
            }
            catch (TaskCanceledException)
            {
                return Tuple.Create(false, "退药API请求超时（30秒），请检查服务器网络是否能访问 " + ApiBaseUrl, "");
            }
            catch (HttpRequestException ex)
            {
                var detail = ex.Message;
                if (ex.InnerException != null)
                    detail += " | " + ex.InnerException.Message;
                return Tuple.Create(false, "退药网络请求失败: " + detail, "");
            }
            catch (Exception ex)
            {
                var detail = ex.Message;
                if (ex.InnerException != null)
                    detail += " | Inner: " + ex.InnerException.Message;
                return Tuple.Create(false, detail, "");
            }
        }

        /// <summary>
        /// 计算MD5（32位小写）
        /// </summary>
        /// <param name="input">原始字符串</param>
        /// <returns>32位小写MD5</returns>
        private static string ComputeMD5(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// 计算SHA256签名（大写HEX）
        /// 签名规则：SHA256(jsonBody + timestamp + appSecret) → 大写
        /// </summary>
        /// <param name="jsonBody">请求JSON体</param>
        /// <param name="timestamp">13位时间戳</param>
        /// <param name="appSecret">接口密钥</param>
        /// <returns>大写SHA256签名</returns>
        private static string ComputeSign(string jsonBody, long timestamp, string appSecret)
        {
            var raw = jsonBody + timestamp + appSecret;
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

    }
}
