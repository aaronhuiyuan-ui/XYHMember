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
        /// <summary>药品明细核对：结算折扣率（固定0.6）</summary>
        private const decimal DiscountRate = 0.6m;

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

                // 查询成功，缓存到本地表
                var dataJson = jResp["data"]?.ToString(Formatting.None) ?? "{}";
                try
                {
                    var checkSql = @"SELECT COUNT(*) FROM fghis5..处方结果本地表 WHERE outcfcode = @outcfcode";
                    var exists = db.Database.SqlQuery<int>(checkSql,
                        new SqlParameter("@outcfcode", outcfcode)).FirstOrDefault() > 0;

                    if (exists)
                    {
                        var updSql = @"UPDATE fghis5..处方结果本地表 SET json_data = @json, billdate = @billdate, 查询时间 = GETDATE() WHERE outcfcode = @outcfcode";
                        db.Database.ExecuteSqlCommand(updSql,
                            new SqlParameter("@json", SqlDbType.NVarChar, -1) { Value = dataJson },
                            new SqlParameter("@billdate", billdate ?? ""),
                            new SqlParameter("@outcfcode", outcfcode));
                    }
                    else
                    {
                        var insSql = @"INSERT INTO fghis5..处方结果本地表 (outcfcode, billdate, json_data, 查询时间) VALUES (@outcfcode, @billdate, @json, GETDATE())";
                        db.Database.ExecuteSqlCommand(insSql,
                            new SqlParameter("@outcfcode", outcfcode),
                            new SqlParameter("@billdate", billdate ?? ""),
                            new SqlParameter("@json", dataJson));
                    }
                }
                catch { /* 缓存失败不影响前端显示 */ }

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
        //  药品明细信息核对
        // =====================================================================

        /// <summary>
        /// 药品明细信息核对页面（GET）
        /// 比对系统结算的药品收费明细与处方结果中的发药明细
        /// </summary>
        public ActionResult DrugDetailCompare()
        {
            return View();
        }

        /// <summary>
        /// 查询核对数据（POST）
        /// 获取收费明细 + 发药明细，双向完整展示并比对
        /// </summary>
        [HttpPost]
        public ActionResult GetDrugDetailCompare()
        {
            var bdate = Request["bdatepicker"]?.Trim();
            var edate = Request["edatepicker"]?.Trim();
            if (string.IsNullOrEmpty(bdate)) bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate)) edate = DateTime.Today.ToString("yyyy-MM-dd");

            try
            {
                var result = BuildDrugDetailCompare(bdate, edate);
                return View("DrugDetailCompare", result);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Content("获取数据失败：" + inner.Message, "text/html; charset=utf-8");
            }
        }

        /// <summary>
        /// 药品汇总信息核对页面（GET）
        /// 按药品编码汇总收费/发药合计，一行一个药品
        /// </summary>
        public ActionResult DrugSummaryCompare()
        {
            return View();
        }

        /// <summary>
        /// 查询汇总核对数据（POST）
        /// 复用 BuildDrugDetailCompare 得到明细，再按药品编码汇总
        /// </summary>
        [HttpPost]
        public ActionResult GetDrugSummaryCompare()
        {
            var bdate = Request["bdatepicker"]?.Trim();
            var edate = Request["edatepicker"]?.Trim();
            if (string.IsNullOrEmpty(bdate)) bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate)) edate = DateTime.Today.ToString("yyyy-MM-dd");

            try
            {
                var detail = BuildDrugDetailCompare(bdate, edate);
                var summary = BuildDrugSummary(detail);
                return View("DrugSummaryCompare", summary);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Content("获取数据失败：" + inner.Message, "text/html; charset=utf-8");
            }
        }

        /// <summary>
        /// 构建药品明细核对数据（收费侧 门诊_收费明细表 + 发药侧 处方结果本地表 双向比对）
        /// </summary>
        private List<DrugDetailCompareItem> BuildDrugDetailCompare(string bdate, string edate)
        {
            // 1. 查询 HIS 收费明细（药品：项目类别='3'）
            var billingSql = @"
SELECT CAST(b.处方ID AS INT) AS 处方ID, CONVERT(varchar, b.日期, 23) AS 日期,
       a.姓名 AS 病人姓名,
       CAST(b.项目ID AS NVARCHAR(50)) AS 项目ID, b.项目名称, b.单价, b.数量, b.金额,
       c.注册商标 AS 药品编码
FROM fghis5..门诊_收费发票表 a
JOIN fghis5..门诊_收费明细表 b ON a.结帐ID = b.结帐ID
LEFT JOIN fghis5..代码_药品基本信息表 c ON c.药品ID = b.项目ID
WHERE a.发票状态 = '2'
  AND b.项目类别 = 3
  AND b.日期 BETWEEN @bdate AND @edate
ORDER BY b.处方ID, b.项目名称";

            var billingItems = db.Database.SqlQuery<BillingDrugItem>(billingSql,
                new SqlParameter("@bdate", QueryHelper.ParseDate(bdate)),
                new SqlParameter("@edate", QueryHelper.ParseDate(edate))).ToList();

            // 2. 查询处方结果本地表（HIS处方结果接口数据），作为发药侧
            // 说明：该表由「已发药查询 → 处方结果」写入，存的是 getPrescriptionInfo 返回的 data；
            //       只加载本次收费日期范围内有收费明细的处方ID，避免带入范围外的数据
            var billingPids = new HashSet<int>(billingItems.Select(b => b.处方ID));
            var dispenseMap = new Dictionary<int, DispenseInfo>();
            if (billingPids.Count > 0)
            {
                var pidIn = string.Join(",", billingPids.Select(p => p.ToString()));
                var resultSql = @"SELECT CAST(outcfcode AS INT) AS 处方ID, json_data AS content_json
FROM fghis5..处方结果本地表
WHERE outcfcode IS NOT NULL
  AND CAST(outcfcode AS INT) IN (" + pidIn + ")";
                var resultRecords = db.Database.SqlQuery<DispenseJsonRecord>(resultSql).ToList();
                foreach (var rec in resultRecords)
                {
                    try
                    {
                        var json = JObject.Parse(rec.content_json ?? "{}");
                        var patient = json["patient"]?.Value<string>() ?? "";
                        var agentnum = json["agentnum"]?.Value<int?>();
                        var mxList = json["sellKpMxVos"] as JArray;

                        var items = new List<DispenseDetailItem>();
                        if (mxList != null)
                        {
                            foreach (var mx in mxList)
                            {
                                // 发药单价：sellKpMxVos[].price（兼容 sellprice/goodssprice）
                                decimal? priceVal = null;
                                var priceToken = mx["price"] ?? mx["sellprice"] ?? mx["goodssprice"];
                                if (priceToken != null)
                                {
                                    decimal pv;
                                    if (decimal.TryParse(priceToken.ToString(), out pv)) priceVal = pv;
                                }

                                items.Add(new DispenseDetailItem
                                {
                                    goodsname = mx["goodsname"]?.Value<string>() ?? "",
                                    dosage = mx["dosage"]?.Value<string>() ?? "",
                                    goodsid = mx["goodsid"]?.Value<string>() ?? "",
                                    goodscode = mx["goodscode"]?.Value<string>() ?? "",
                                    goodsspec = mx["goodsspec"]?.Value<string>() ?? "",
                                    goodsunit = mx["goodsunit"]?.Value<string>() ?? "",
                                    price = priceVal
                                });
                            }
                        }

                        dispenseMap[rec.处方ID] = new DispenseInfo
                        {
                            Patient = patient,
                            Agentnum = agentnum,
                            Items = items
                        };
                    }
                    catch { /* 跳过JSON解析失败的记录 */ }
                }
            }

            // 4. 双向比对
            var result = new List<DrugDetailCompareItem>();
            var matchedKeys = new HashSet<string>(); // 记录已匹配的 "处方ID_项目名称"

            // 4a. 遍历收费明细，匹配发药数据
            foreach (var bill in billingItems)
            {
                var compare = new DrugDetailCompareItem
                {
                    来源 = "收费",
                    处方ID = bill.处方ID,
                    日期 = bill.日期,
                    病人姓名 = bill.病人姓名,
                    项目ID = bill.项目ID,
                    收费药品编码 = bill.药品编码,
                    项目名称 = bill.项目名称,
                    单价 = bill.单价,
                    收费数量 = bill.数量,
                    金额 = bill.金额
                };

                if (dispenseMap.TryGetValue(bill.处方ID, out var dispInfo))
                {
                    compare.病人姓名 = compare.病人姓名 ?? dispInfo.Patient;
                    compare.剂数 = dispInfo.Agentnum;
                    // 按编码匹配饮片（收费侧 代码_药品基本信息表.注册商标 = 发药侧 goodscode）
                    var billCode = bill.药品编码?.Trim();
                    var match = dispInfo.Items.FirstOrDefault(i =>
                        !string.IsNullOrEmpty(billCode) &&
                        !string.IsNullOrEmpty(i.goodscode) && i.goodscode.Trim() == billCode);
                    if (match != null)
                    {
                        compare.饮片名称 = match.goodsname;
                        compare.饮片用量 = match.dosage;
                        compare.发药药品编码 = match.goodscode;
                        // 发药单价：取处方结果本地表 sellKpMxVos[].price
                        compare.发药单价 = match.price;
                        matchedKeys.Add(bill.处方ID + "_" + bill.药品编码);

                        decimal dosageVal;
                        if (decimal.TryParse(match.dosage, out dosageVal) && dispInfo.Agentnum.HasValue)
                        {
                            compare.计算数量 = Math.Round(dosageVal * dispInfo.Agentnum.Value, 2);
                            // 处方金额 = 发药单价 × 计算总用量（用量×剂数），保留3位小数、远离零舍入
                            compare.处方金额 = match.price.HasValue ? Math.Round(match.price.Value * compare.计算数量.Value, 3, MidpointRounding.AwayFromZero) : (decimal?)null;
                            // 折扣固定0.6；结算金额 = 发药金额 × 折扣
                            compare.折扣 = DiscountRate;
                            compare.结算金额 = compare.处方金额.HasValue ? Math.Round(compare.处方金额.Value * DiscountRate, 3, MidpointRounding.AwayFromZero) : (decimal?)null;
                            compare.是否一致 = compare.收费数量 == compare.计算数量 ? "一致" : "不一致";
                        }
                        else
                        {
                            compare.是否一致 = "无法比对";
                        }
                    }
                    else
                    {
                        compare.是否一致 = "无对应发药记录";
                    }
                }
                else
                {
                    compare.是否一致 = "无发药信息";
                }

                result.Add(compare);
            }

            // 4b. 遍历发药明细，补充收费中没有的条目
            foreach (var kv in dispenseMap)
            {
                var pid = kv.Key;
                var dispInfo = kv.Value;
                foreach (var item in dispInfo.Items)
                {
                    // 按编码判断是否已被收费侧匹配（goodscode）
                    var keyGcode = pid + "_" + (item.goodscode ?? "");
                    if (matchedKeys.Contains(keyGcode)) continue;

                    // 计算总用量 = 用量 × 剂数；处方金额 = 发药单价 × 计算总用量
                    decimal? dispQty = null;
                    decimal qtyVal;
                    if (decimal.TryParse(item.dosage, out qtyVal) && dispInfo.Agentnum.HasValue)
                    {
                        dispQty = Math.Round(qtyVal * dispInfo.Agentnum.Value, 2);
                    }
                    decimal? dispAmt = (dispQty.HasValue && item.price.HasValue)
                        ? Math.Round(item.price.Value * dispQty.Value, 3, MidpointRounding.AwayFromZero)
                        : (decimal?)null;
                    // 折扣固定0.6；结算金额 = 发药金额 × 折扣
                    decimal? dispSettle = dispAmt.HasValue
                        ? Math.Round(dispAmt.Value * DiscountRate, 3, MidpointRounding.AwayFromZero)
                        : (decimal?)null;

                    result.Add(new DrugDetailCompareItem
                    {
                        来源 = "发药",
                        处方ID = pid,
                        病人姓名 = dispInfo.Patient ?? "",
                        项目ID = !string.IsNullOrEmpty(item.goodsid) ? item.goodsid : item.goodscode,
                        收费药品编码 = "",
                        发药药品编码 = item.goodscode,
                        项目名称 = item.goodsname,
                        饮片名称 = item.goodsname,
                        饮片用量 = item.dosage,
                        剂数 = dispInfo.Agentnum,
                        发药单价 = item.price,
                        计算数量 = dispQty,
                        处方金额 = dispAmt,
                        折扣 = DiscountRate,
                        结算金额 = dispSettle,
                        是否一致 = "无对应收费记录"
                    });
                }
            }

            // 4c. 按处方ID、来源排序
            return result.OrderBy(r => r.处方ID)
                         .ThenBy(r => r.来源 == "收费" ? 0 : 1)
                         .ThenBy(r => r.项目名称)
                         .ToList();
        }

        /// <summary>
        /// 按药品编码汇总明细（一行一个药品）
        /// 一致 = 收费数量合计 == 发药总用量合计
        /// </summary>
        private List<DrugSummaryCompareItem> BuildDrugSummary(List<DrugDetailCompareItem> detail)
        {
            var map = new Dictionary<string, DrugSummaryCompareItem>();
            foreach (var row in detail)
            {
                // 取药品编码：优先收费编码，其次发药编码
                var code = string.IsNullOrEmpty(row.收费药品编码) ? row.发药药品编码 : row.收费药品编码;
                code = (code ?? "").Trim();
                if (string.IsNullOrEmpty(code)) continue;

                if (!map.TryGetValue(code, out var s))
                {
                    s = new DrugSummaryCompareItem
                    {
                        药品编码 = code,
                        收费数量 = 0m,
                        收费金额 = 0m,
                        发药总用量 = 0m,
                        发药金额 = 0m,
                        结算金额 = 0m
                    };
                    map[code] = s;
                }
                if (string.IsNullOrEmpty(s.药品名称) && !string.IsNullOrEmpty(row.项目名称))
                    s.药品名称 = row.项目名称;

                s.收费数量 += row.收费数量 ?? 0m;
                s.收费金额 += row.金额 ?? 0m;
                s.发药总用量 += row.计算数量 ?? 0m;
                s.发药金额 += row.处方金额 ?? 0m;
                s.结算金额 += row.结算金额 ?? 0m;
            }

            foreach (var s in map.Values)
            {
                s.收费数量 = Math.Round(s.收费数量 ?? 0m, 3, MidpointRounding.AwayFromZero);
                s.收费金额 = Math.Round(s.收费金额 ?? 0m, 3, MidpointRounding.AwayFromZero);
                s.发药总用量 = Math.Round(s.发药总用量 ?? 0m, 3, MidpointRounding.AwayFromZero);
                s.发药金额 = Math.Round(s.发药金额 ?? 0m, 3, MidpointRounding.AwayFromZero);
                s.结算金额 = Math.Round(s.结算金额 ?? 0m, 3, MidpointRounding.AwayFromZero);
                s.一致 = s.收费数量 == s.发药总用量 ? "一致" : "不一致";
            }

            return map.Values.OrderBy(s => s.药品编码).ToList();
        }

        // =====================================================================
        //  批量查询处方结果（药品明细信息核对用）
        // =====================================================================

        /// <summary>
        /// 按日期范围批量调用处方结果接口（getPrescriptionInfo），更新处方结果本地表（POST）
        /// 前端「批量查询处方结果」按钮调用
        /// </summary>
        [HttpPost]
        public ActionResult BatchQueryPrescriptionResult()
        {
            var bdate = Request["bdatepicker"]?.Trim();
            var edate = Request["edatepicker"]?.Trim();
            if (string.IsNullOrEmpty(bdate)) bdate = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(edate)) edate = DateTime.Today.ToString("yyyy-MM-dd");

            try
            {
                // 1. 取日期范围内收费的处方ID（与核对页收费侧同口径）
                var pidSql = @"
SELECT DISTINCT CAST(b.处方ID AS INT) AS 处方ID,
       CONVERT(varchar, MIN(b.日期), 23) AS 日期
FROM fghis5..门诊_收费发票表 a
JOIN fghis5..门诊_收费明细表 b ON a.结帐ID = b.结帐ID
WHERE a.发票状态 = '2' AND b.项目类别 = 3
  AND b.日期 BETWEEN @bdate AND @edate
GROUP BY b.处方ID";
                var pidRows = db.Database.SqlQuery<BillingDrugItem>(pidSql,
                    new SqlParameter("@bdate", QueryHelper.ParseDate(bdate)),
                    new SqlParameter("@edate", QueryHelper.ParseDate(edate))).ToList();

                int successCount = 0, failCount = 0;
                var failList = new List<string>();
                foreach (var row in pidRows)
                {
                    var outcfcode = row.处方ID.ToString();
                    try
                    {
                        var result = QueryPrescriptionResultData(outcfcode);
                        if (result.Item1 == null)
                        {
                            failCount++;
                            if (failList.Count < 20) failList.Add(outcfcode + "：" + (result.Item2 ?? "接口无数据"));
                            continue;
                        }
                        var dataJson = result.Item1;
                        // 业务日期：优先取接口返回的 billdate，取不到用收费日期
                        var billdate = row.日期 ?? "";
                        try
                        {
                            var jData = JObject.Parse(dataJson);
                            billdate = jData["billdate"]?.Value<string>() ?? billdate;
                        }
                        catch { /* 保持收费日期 */ }

                        UpsertPrescriptionResultCache(outcfcode, billdate, dataJson);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        if (failList.Count < 20) failList.Add(outcfcode + "：" + ex.Message);
                    }
                    finally
                    {
                        // 每次调用之间留间隔，避免触发外部接口限流
                        System.Threading.Thread.Sleep(300);
                    }
                }

                return Json(new
                {
                    success = true,
                    total = pidRows.Count,
                    successCount,
                    failCount,
                    failList = failList.Take(10).ToList()
                });
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
        /// 调用处方结果查询接口（POST /api/v2/prescription/getPrescriptionInfo）
        /// 返回 Tuple(数据JSON, 失败原因)；Item1 不为 null 表示成功，Item2 为失败原因（成功时为 null）
        /// </summary>
        private Tuple<string, string> QueryPrescriptionResultData(string outcfcode)
        {
            const int maxAttempts = 3; // 首次 + 2 次重试
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var result = CallGetPrescriptionInfo(outcfcode);
                if (result.Item1 != null) return result; // 成功

                // 仅对"接口访问太频繁"限流做重试，其他失败直接返回
                var reason = result.Item2 ?? "";
                var isRateLimit = reason.Contains("频繁") || reason.Contains("稍后再试") || reason.Contains("限流");
                if (isRateLimit && attempt < maxAttempts)
                {
                    System.Threading.Thread.Sleep(1000 * attempt); // 指数退避：1s、2s
                    continue;
                }
                return result;
            }
            return Tuple.Create((string)null, "重试3次后仍被限流");
        }

        /// <summary>
        /// 单次调用处方结果查询接口（不重试）
        /// </summary>
        private Tuple<string, string> CallGetPrescriptionInfo(string outcfcode)
        {
            try
            {
                var requestBody = new JObject
                {
                    ["customercode"] = CustomerCode,
                    ["outcfcode"] = outcfcode,
                    ["checkcode"] = CheckCodeMd5
                };
                var jsonBody = requestBody.ToString(Formatting.None);

                var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
                var sign = ComputeSign(jsonBody, timestamp, AppSecret);

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/v2/prescription/getPrescriptionInfo");
                httpRequest.Headers.Add("appId", AppId);
                httpRequest.Headers.Add("timestamp", timestamp.ToString());
                httpRequest.Headers.Add("sign", sign);
                httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30); // 与单条处方结果查询(GetPrescriptionDetail)一致
                    var response = httpClient.SendAsync(httpRequest).ConfigureAwait(false).GetAwaiter().GetResult();
                    var responseBody = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    var jResp = JObject.Parse(responseBody);

                    var isSuccess = jResp["isSuccess"]?.Value<bool>() ?? false;
                    if (!isSuccess)
                    {
                        var msg = jResp["messageInfos"]?.ToString()
                                  ?? jResp["msg"]?.ToString()
                                  ?? "接口返回失败";
                        return Tuple.Create((string)null, msg);
                    }
                    var data = jResp["data"]?.ToString(Formatting.None);
                    if (string.IsNullOrEmpty(data))
                        return Tuple.Create((string)null, "接口返回成功但 data 为空");
                    return Tuple.Create(data, (string)null);
                }
            }
            catch (TaskCanceledException)
            {
                return Tuple.Create((string)null, "请求超时(30秒)");
            }
            catch (Exception ex)
            {
                var detail = ex.Message;
                if (ex.InnerException != null)
                    detail += " | " + ex.InnerException.Message;
                return Tuple.Create((string)null, "调用异常: " + detail);
            }
        }

        /// <summary>
        /// 写入/更新处方结果本地表（按 outcfcode 存在则UPDATE，否则INSERT）
        /// </summary>
        private void UpsertPrescriptionResultCache(string outcfcode, string billdate, string dataJson)
        {
            var checkSql = @"SELECT COUNT(*) FROM fghis5..处方结果本地表 WHERE outcfcode = @outcfcode";
            var exists = db.Database.SqlQuery<int>(checkSql,
                new SqlParameter("@outcfcode", outcfcode)).FirstOrDefault() > 0;

            if (exists)
            {
                var updSql = @"UPDATE fghis5..处方结果本地表 SET json_data = @json, billdate = @billdate, 查询时间 = GETDATE() WHERE outcfcode = @outcfcode";
                db.Database.ExecuteSqlCommand(updSql,
                    new SqlParameter("@json", SqlDbType.NVarChar, -1) { Value = dataJson },
                    new SqlParameter("@billdate", billdate ?? ""),
                    new SqlParameter("@outcfcode", outcfcode));
            }
            else
            {
                var insSql = @"INSERT INTO fghis5..处方结果本地表 (outcfcode, billdate, json_data, 查询时间) VALUES (@outcfcode, @billdate, @json, GETDATE())";
                db.Database.ExecuteSqlCommand(insSql,
                    new SqlParameter("@outcfcode", outcfcode),
                    new SqlParameter("@billdate", billdate ?? ""),
                    new SqlParameter("@json", dataJson));
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
