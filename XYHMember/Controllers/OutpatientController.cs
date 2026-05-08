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

            var patients = new List<HealthExamPatient>();
            var idCards = new List<string>();
            foreach (var item in obj["data"]["content"])
            {
                var idCard = item["idCardNo"]?.Value<string>() ?? "";
                idCards.Add(idCard);
                patients.Add(new HealthExamPatient
                {
                    体检号 = item["studyId"]?.Value<string>() ?? "",
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
                    .ToDictionary(m => m.身份证号, m => m.PID.ToString());

                foreach (var p in patients)
                {
                    if (pidMap.TryGetValue(p.身份证号, out var pid))
                        p.门诊号 = pid;
                }
            }

            return patients;
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
