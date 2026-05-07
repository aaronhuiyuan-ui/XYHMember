using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    [AuthFilter]
    public class ExportController : Controller
    {
        public class ExportData
        {
            public List<string> Headers { get; set; } = new List<string>();
            public List<List<string>> Rows { get; set; } = new List<List<string>>();
        }

        [HttpPost]
        public ActionResult ExportToExcel()
        {
            // 直接读取原始请求体
            Request.InputStream.Position = 0;
            string raw;
            using (var sr = new StreamReader(Request.InputStream))
            {
                raw = sr.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(raw))
                return new HttpStatusCodeResult(400, "请求体为空");

            ExportData exportData;
            try
            {
                exportData = Newtonsoft.Json.JsonConvert.DeserializeObject<ExportData>(raw);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(400, "JSON 反序列化失败: " + ex.Message);
            }

            if (exportData == null || exportData.Headers == null || exportData.Rows == null)
                return new HttpStatusCodeResult(400, "反序列化后为空");

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Sheet1");

                // 写入表头
                for (int i = 0; i < exportData.Headers.Count; i++)
                    ws.Cell(1, i + 1).Value = exportData.Headers[i];

                // 写入数据并强制类型
                for (int r = 0; r < exportData.Rows.Count; r++)
                {
                    for (int c = 0; c < exportData.Rows[r].Count; c++)
                    {
                        var cell = ws.Cell(r + 2, c + 1);

                        string value = exportData.Rows[r][c];

                        // 第 3 和 第 6 列强制文本格式（特别处理身份证）
                        if (c + 1 == 3 || c + 1 == 6)
                        {
                            cell.Value = "'" + value;  // ← 加前置单引号，Excel 会当做纯文本
                            cell.SetDataType(XLDataType.Text);
                        }
                        else
                        {
                            cell.Value = value;
                        }
                    }
                }


                // 可选：自动列宽
                ws.Columns().AdjustToContents();

                using (var ms = new MemoryStream())
                {
                    workbook.SaveAs(ms);
                    return File(ms.ToArray(),
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                "export.xlsx");
                }
            }
        }



    }
}
