using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    [AuthFilter]
    public class OutpatientController : Controller
    {
        private XYHDbContext db = new XYHDbContext();
        private static readonly HttpClient _client = new HttpClient();
        private static string _token = null;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        private static Dictionary<string, JToken> _rawDataCache = new Dictionary<string, JToken>();

        private const string BaseUrl = "http://api.richtj.com/cross/v2";
        private const string Username = "GT_ZYMZ";
        private const string Password = "Rich123";

        //体检病人信息查询页面
        public ActionResult PhysicalExamQuery()
        {
            return View();
        }

        //按姓名或身份证号搜索体检病人信息
        [HttpGet]
        public ActionResult GetPhysicalExamQuery()
        {
            var keyword = Request["name"].Trim();

            var result = SearchPatients(keyword);

            return View("PhysicalExamQuery", result);
        }

        private List<HealthExamPatient> SearchPatients(string keyword)
        {
            var token = GetToken();
            if (token == null)
                return new List<HealthExamPatient>();

            var url = $"{BaseUrl}/report/getReport?version=1";

            var body = new JObject
            {
                ["type"] = "1",
                ["page"] = 1,
                ["size"] = 100
            };

            //判断输入是身份证号还是姓名
            if (!string.IsNullOrEmpty(keyword))
            {
                if (keyword.Length >= 7 && System.Text.RegularExpressions.Regex.IsMatch(keyword, @"^[\dXx]+$"))
                    body["idCardNo"] = keyword;
                else
                    body["name"] = keyword;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", token);

            var response = _client.SendAsync(request).Result;
            var json = response.Content.ReadAsStringAsync().Result;
            var obj = JObject.Parse(json);

            if (obj["code"].Value<int>() != 200)
                return new List<HealthExamPatient>();

            _rawDataCache.Clear();
            var patients = new List<HealthExamPatient>();
            var idCards = new List<string>();
            foreach (var item in obj["data"]["content"])
            {
                var studyId = item["studyId"]?.Value<string>() ?? "";
                var idCard = item["idCardNo"]?.Value<string>() ?? "";
                idCards.Add(idCard);
                _rawDataCache[studyId] = item;
                patients.Add(new HealthExamPatient
                {
                    体检号 = studyId,
                    姓名 = item["name"]?.Value<string>() ?? "",
                    身份证号 = idCard,
                    电话 = item["telephone"]?.Value<string>() ?? "",
                    住址 = item["address"]?.Value<string>() ?? ""
                });
            }

            //批量查询本地数据库获取PID
            if (idCards.Count > 0)
            {
                var idCardParams = new List<SqlParameter>();
                var inClauses = new List<string>();
                for (int i = 0; i < idCards.Count; i++)
                {
                    var paramName = $"@id{i}";
                    inClauses.Add(paramName);
                    idCardParams.Add(new SqlParameter(paramName, idCards[i]));
                }

                var sql = $"SELECT 身份证号, PID FROM fghis5..系统_病人基本信息表 WHERE 身份证号 IN ({string.Join(",", inClauses)})";
                var pidMap = db.Database.SqlQuery<PidMapping>(sql, idCardParams.ToArray())
                    .Where(m => !string.IsNullOrEmpty(m.身份证号))
                    .GroupBy(m => m.身份证号)
                    .ToDictionary(g => g.Key, g => g.First().PID.ToString());

                foreach (var p in patients)
                {
                    if (pidMap.TryGetValue(p.身份证号, out var pid))
                        p.门诊号 = pid;
                }
            }

            return patients;
        }

        //保存选中病人到门诊系统(建档)
        [HttpPost]
        public ActionResult SaveToOutpatient()
        {
            var input = new System.IO.StreamReader(Request.InputStream).ReadToEnd();
            var req = JObject.Parse(input);
            var studyIds = req["studyIds"].ToObject<List<string>>();

            if (studyIds == null || studyIds.Count == 0)
                return Json(new { success = false, msg = "未选择数据" });

            //查询默认记帐代码对应的病人标识
            var brbsSql = @"SELECT TOP 1 ISNULL(病人标识, '0') as 病人标识 FROM fghis5..代码_病人记帐对照表 WHERE 记帐代码 = '10'";
            var brbs = db.Database.SqlQuery<string>(brbsSql).FirstOrDefault() ?? "0";

            //1.病人基本信息表
            var insertPatientSql = @"INSERT INTO fghis5..系统_病人基本信息表
([门诊号],[默认卡信息ID],[默认卡号],[姓],[名],[姓名],[性别],[出生日期],[出生时间],
[身份证号],[国籍],[民族],[血型],[婚姻],[职业],[健康卡号],[联系电话],[联系手机],
[联系邮箱],[联系邮编],[联系地址],[纳税人识别号],[其他联系方式],[使用语言],[宗教信仰],
[个人偏好],[过敏信息],[备注事项],[首次就诊日期],[最近就诊日期],[信息来源],[保密级别],
[创建时间],[创建人ID],[单位代码],[单位名称],[照片],[EMPI],[输入码1],[输入码2],
[出生地],[外部编号],[PID],[省],[市],[县],[身份证照片])
VALUES (@mzh, @cardInfoId, @cardNo, N'', N'', @name, @sex, @birthday, '000000',
@idCardNo, 0, 0, 0, 0, '', '', '', @telephone,
'', '', @address, '', '', '0', 0,
'', '0', '', CONVERT(VARCHAR(8), GETDATE(), 112), CONVERT(VARCHAR(8), GETDATE(), 112), '', 0,
GETDATE(), @userId, '', '', '', '',fghis5.dbo.FB_GetChineseSpell(@name), '', 
NULL, NULL, @pid, NULL, NULL, NULL, NULL)";

            //2.病人卡信息表(建档必须创建的默认门诊卡)
            var insertCardSql = @"INSERT INTO fghis5..系统_病人卡信息表
([卡信息ID],[卡号],[卡类型],[门诊号],[记帐代码],[病人标识],[参保类型],
[卡交易类型],[交易排序],[凭证号],[有效日期],[状态],[操作员ID],[操作时间],[IC卡标志])
VALUES (@cardInfoId, @cardNo, 1, @mzh, '10', @brbs, 0,
1, 1, @cardNo, '1900-01-01', 1, @userId, GETDATE(), 0)";

            try
            {
            var userId = ((int?)Session["UserId"])?.ToString() ?? "1";
            var saved = 0;
            foreach (var studyId in studyIds)
            {
                if (!_rawDataCache.TryGetValue(studyId, out var raw))
                    continue;

                var name = raw["name"]?.Value<string>() ?? "";
                var idCardNo = raw["idCardNo"]?.Value<string>() ?? "";
                //根据身份证号判断性别: 0未知 1男 2女
                var sex = "0";
                if (!string.IsNullOrEmpty(idCardNo) && idCardNo.Length >= 15)
                {
                    char sexChar = idCardNo.Length == 18 ? idCardNo[16] : idCardNo[14];
                    if (char.IsDigit(sexChar))
                        sex = (int.Parse(sexChar.ToString()) % 2 == 1) ? "1" : "2";
                }
                var birthdayRaw = raw["birthday"]?.Value<string>() ?? "";
                var birthday = birthdayRaw.Length >= 10 ? birthdayRaw.Substring(0, 10).Replace("-", "") : "";
                var telephone = raw["telephone"]?.Value<string>() ?? "";
                var address = raw["address"]?.Value<string>() ?? "";
                //门诊号: OUTPUT INSERTED 原子递增并返回新值
                var curMzh = db.Database.SqlQuery<int>(
                    @"UPDATE fghis5..系统_编码流水号 SET 流水号 = 流水号 + 1
OUTPUT INSERTED.流水号
WHERE 分类 = '系统' AND 名称 = N'门诊号'").FirstOrDefault();

                //卡信息ID取系统编码流水号
                var curCardInfoId = db.Database.SqlQuery<int>(
                    @"UPDATE fghis5..系统_编码流水号 SET 流水号 = 流水号 + 1
OUTPUT INSERTED.流水号
WHERE 分类 = '系统' AND 名称 = N'卡信息ID'").FirstOrDefault();

                //卡号/凭证号/PID = 99 + 门诊号
                var curCardNo = "99" + curMzh;
                var curPid = long.Parse(curCardNo);

                //写入病人基本信息
                db.Database.ExecuteSqlCommand(insertPatientSql, new SqlParameter[]
                {
                    new SqlParameter("@mzh", curMzh),
                    new SqlParameter("@cardInfoId", curCardInfoId),
                    new SqlParameter("@cardNo", curCardNo),
                    new SqlParameter("@name", name),
                    new SqlParameter("@sex", sex),
                    new SqlParameter("@birthday", birthday),
                    new SqlParameter("@idCardNo", idCardNo),
                    new SqlParameter("@telephone", telephone),
                    new SqlParameter("@address", address),
                    new SqlParameter("@pid", curPid),
                    new SqlParameter("@userId", userId)
                });

                //写入病人卡信息
                db.Database.ExecuteSqlCommand(insertCardSql, new SqlParameter[]
                {
                    new SqlParameter("@cardInfoId", curCardInfoId),
                    new SqlParameter("@cardNo", curCardNo),
                    new SqlParameter("@mzh", curMzh),
                    new SqlParameter("@brbs", brbs),
                    new SqlParameter("@userId", userId)
                });

                saved++;
            }

            return Json(new { success = true, msg = $"成功保存{saved}条记录" });
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                return Json(new { success = false, msg = inner.Message });
            }
        }

        private string GetToken()
        {
            //token未过期直接复用
            if (_token != null && DateTime.Now < _tokenExpiry)
                return _token;

            var url = $"{BaseUrl}/auth/idmLoginForInterface";

            var body = new JObject
            {
                ["username"] = Username,
                ["password"] = Password
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json")
            };

            var response = _client.SendAsync(request).Result;
            var json = response.Content.ReadAsStringAsync().Result;
            var obj = JObject.Parse(json);

            if (obj["code"].Value<int>() != 200)
                return null;

            _token = obj["data"]["token"].Value<string>();

            //token提前5分钟过期
            _tokenExpiry = DateTime.Now.AddHours(2).AddMinutes(-5);

            return _token;
        }
    }
}
