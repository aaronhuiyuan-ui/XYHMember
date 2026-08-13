import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  Document, Packer, Paragraph, TextRun, Table, TableCell, TableRow,
  WidthType, BorderStyle, ShadingType, HeadingLevel, AlignmentType,
  HeightRule, VerticalAlign, TableLayoutType, PageNumber, Header, Footer,
} from "docx";

/* ============ 设计常量 ============ */
const YAHEI = "微软雅黑";
const CALIBRI = "Calibri";
const CONSOLAS = "Consolas";
const NAVY = "1F3864";      // 封面深蓝
const NAVY2 = "1F4E79";     // 主标题蓝
const BLUE = "2E74B5";      // 强调蓝
const GREEN = "2E7D32";     // POST 徽章绿
const AMBER = "F0A500";     // 提示橙
const INK = "333333";
const GRAY = "9AA5B1";
const BORDER = "C9D6E4";
const HDR_FILL = "2E74B5";
const ZEBRA = "F2F7FB";
const CODE_FILL = "F5F5F5";
const NOTE_FILL = "FFF8E1";
const CONTENT_W = 9026;     // A4 - 1440*2 边距

const font = (latin) => ({ ascii: latin, hAnsi: latin, eastAsia: YAHEI, cs: latin });
const tr = (text, o = {}) => new TextRun({
  text,
  font: font(o.font || CALIBRI),
  size: o.size ?? 21,
  bold: o.bold,
  italic: o.italic,
  color: o.color || INK,
  shading: o.shading,
});

/* ============ 结构元素 ============ */
const h1 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_1,
  spacing: { before: 320, after: 180, line: 300 },
  indent: { left: 120, right: 120 },
  shading: { fill: "EAF1F8", type: ShadingType.CLEAR, color: "auto" },
  border: { left: { style: BorderStyle.SINGLE, size: 28, color: NAVY2, space: 8 } },
  children: [tr(text, { bold: true, color: NAVY2, size: 28, font: YAHEI })],
});

const h2 = (text) => new Paragraph({
  heading: HeadingLevel.HEADING_2,
  spacing: { before: 200, after: 120, line: 280 },
  border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: "9BB7D4", space: 4 } },
  children: [tr(text, { bold: true, color: BLUE, size: 24, font: YAHEI })],
});

const note = (text) => new Paragraph({
  spacing: { before: 80, after: 140, line: 300 },
  indent: { left: 160, right: 160 },
  shading: { fill: NOTE_FILL, type: ShadingType.CLEAR, color: "auto" },
  border: { left: { style: BorderStyle.SINGLE, size: 18, color: AMBER, space: 6 } },
  children: [tr(text, { color: "8A6D00" })],
});

function code(lines) {
  return lines.map((line) => new Paragraph({
    spacing: { after: 0, line: 240 },
    indent: { left: 160, right: 160 },
    shading: { fill: CODE_FILL, type: ShadingType.CLEAR, color: "auto" },
    border: { left: { style: BorderStyle.SINGLE, size: 18, color: BLUE, space: 6 } },
    children: [new TextRun({ text: line, font: CONSOLAS, size: 18, color: INK })],
  }));
}

const postBadge = () => new TextRun({
  text: " POST ", font: CONSOLAS, size: 18, bold: true, color: "FFFFFF",
  shading: { fill: GREEN, type: ShadingType.CLEAR, color: "auto" },
});

function cell(content, { fill, width, valign = VerticalAlign.CENTER, margins = { top: 80, bottom: 80, left: 130, right: 130 } } = {}) {
  return new TableCell({
    width: { size: width, type: WidthType.DXA },
    shading: fill ? { fill, type: ShadingType.CLEAR, color: "auto" } : undefined,
    verticalAlign: valign,
    margins,
    children: Array.isArray(content) ? content : [content],
  });
}

const headerCell = (text, width) => cell(
  [new Paragraph({ spacing: { after: 20 }, children: [tr(text, { bold: true, color: "FFFFFF", font: YAHEI, size: 20 })] })],
  { fill: HDR_FILL, width },
);

const dataCell = (runs, width, fill) => cell(
  [new Paragraph({ spacing: { after: 20, line: 260 }, children: Array.isArray(runs) ? runs : [tr(runs, { size: 20 })] })],
  { fill, width },
);

const mono = (text, size = 20) => new TextRun({ text, font: CONSOLAS, size, color: INK });

function makeTable(colWidths, rows) {
  return new Table({
    width: { size: colWidths.reduce((a, b) => a + b, 0), type: WidthType.DXA },
    columnWidths: colWidths,
    layout: TableLayoutType.FIXED,
    rows,
  });
}

/* ============ 目录表 ============ */
const tocItems = ["基本信息", "鉴权说明", "请求参数", "返回说明", "调用示例", "常见问题", "相关页面"];
const tocRows = tocItems.map((t, i) => new TableRow({
  children: [
    cell([new Paragraph({ children: [tr(String(i + 1).padStart(2, "0"), { bold: true, color: NAVY2, size: 20 })] })], { fill: "F7FAFD", width: 1200 }),
    cell([new Paragraph({ children: [tr(t, { size: 20 })] })], { fill: "F7FAFD", width: 7826 }),
  ],
}));
const tocTable = new Table({
  width: { size: 9026, type: WidthType.DXA },
  columnWidths: [1200, 7826],
  layout: TableLayoutType.FIXED,
  borders: {
    top: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
    bottom: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
    left: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
    right: { style: BorderStyle.SINGLE, size: 4, color: BORDER },
    insideHorizontal: { style: BorderStyle.NONE, size: 0, color: "auto" },
    insideVertical: { style: BorderStyle.NONE, size: 0, color: "auto" },
  },
  rows: tocRows,
});

/* ============ 封面 ============ */
const banner = new Table({
  width: { size: 100, type: WidthType.PERCENTAGE },
  rows: [new TableRow({
    height: { value: 4600, rule: HeightRule.ATLEAST },
    children: [new TableCell({
      shading: { fill: NAVY, type: ShadingType.CLEAR, color: "auto" },
      verticalAlign: VerticalAlign.CENTER,
      margins: { top: 700, bottom: 700, left: 900, right: 900 },
      children: [
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 320 }, children: [tr("INTERFACE  ·  API SPECIFICATION", { color: "8FAADC", size: 18, font: YAHEI })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 140 }, children: [tr("上海真仁堂统计汇总", { color: "FFFFFF", bold: true, size: 56, font: YAHEI })] }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 420 }, children: [tr("查询接口对接文档", { color: "D6E4F5", size: 32, font: YAHEI })] }),
        new Paragraph({
          alignment: AlignmentType.CENTER, spacing: { after: 140 },
          border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: AMBER, space: 8 } },
          children: [],
        }),
        new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 220 }, children: [tr("版本 v1.0　|　编制日期 2026-08-13　|　编制单位：信息科", { color: "B9CBE8", size: 20, font: YAHEI })] }),
      ],
    })],
  })],
});

const accentBar = new Table({
  width: { size: 100, type: WidthType.PERCENTAGE },
  rows: [new TableRow({
    height: { value: 110, rule: HeightRule.EXACT },
    children: [new TableCell({
      shading: { fill: AMBER, type: ShadingType.CLEAR, color: "auto" },
      margins: { top: 0, bottom: 0, left: 0, right: 0 },
      children: [new Paragraph({ spacing: { after: 0 }, children: [] })],
    })],
  })],
});

const coverChildren = [
  new Paragraph({ spacing: { before: 500 }, children: [] }),
  banner,
  accentBar,
  new Paragraph({ spacing: { before: 400 }, children: [] }),
];

/* ============ 页眉 / 页脚 ============ */
const contentHeader = new Header({
  children: [new Paragraph({
    alignment: AlignmentType.RIGHT,
    border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: BORDER, space: 4 } },
    children: [tr("上海真仁堂统计汇总 · 查询接口文档", { size: 16, color: GRAY, font: YAHEI })],
  })],
});

const contentFooter = new Footer({
  children: [new Paragraph({
    alignment: AlignmentType.CENTER,
    border: { top: { style: BorderStyle.SINGLE, size: 4, color: BORDER, space: 4 } },
    children: [
      tr("第 ", { size: 16, color: "808080", font: YAHEI }),
      new TextRun({ children: [PageNumber.CURRENT], font: font(YAHEI), size: 16, color: "808080" }),
      tr(" 页", { size: 16, color: "808080", font: YAHEI }),
    ],
  })],
});

/* ============ 正文 ============ */
const content = [];

// ---- 目录 ----
content.push(new Paragraph({
  heading: HeadingLevel.HEADING_1, alignment: AlignmentType.CENTER,
  spacing: { before: 0, after: 220 }, children: [tr("目　　录", { bold: true, color: NAVY2, size: 32, font: YAHEI })],
}));
content.push(tocTable);
content.push(new Paragraph({ spacing: { after: 0 }, children: [] }));

// ---- 一、基本信息 ----
content.push(h1("一、基本信息"));
content.push(makeTable([2200, 6826], [
  new TableRow({ children: [headerCell("项目", 2200), headerCell("说明", 6826)] }),
  new TableRow({ children: [dataCell("接口地址", 2200), dataCell(mono("http://172.68.1.12:8080/OaApi/GetZhenRenTangStats"), 6826)] }),
  new TableRow({ children: [dataCell("请求方式", 2200, ZEBRA), dataCell([postBadge(), tr("　仅支持 POST，GET 不可用", { size: 20 })], 6826, ZEBRA)] }),
  new TableRow({ children: [dataCell("数据格式", 2200), dataCell("JSON（UTF-8）", 6826)] }),
  new TableRow({ children: [dataCell("鉴权方式", 2200, ZEBRA), dataCell([tr("请求头 ", { size: 20 }), mono("X-Api-Key: <密钥>")], 6826, ZEBRA)] }),
  new TableRow({ children: [dataCell("数据来源", 2200), dataCell([mono("fghis5..上海真仁堂统计汇总"), tr("　表（「加工费及快递费汇总」页面「导入到OA」写入）", { size: 20 })], 6826)] }),
]));
content.push(note("⚠️ 端口为 8080，非 80。"));

// ---- 二、鉴权说明 ----
content.push(h1("二、鉴权说明"));
content.push(new Paragraph({ spacing: { after: 80 }, children: [tr("调用方必须携带访问密钥，以下两种传法任选其一：", { size: 21 })] }));
content.push(new Paragraph({ spacing: { before: 80, after: 60 }, children: [tr("1. 推荐：", { bold: true, color: NAVY2, size: 21 }), tr("自定义请求头", { size: 21 })] }));
content.push(...code(["X-Api-Key: zrt-oa-2026"]));
content.push(new Paragraph({ spacing: { before: 140, after: 60 }, children: [tr("2. 兼容方式：", { bold: true, color: NAVY2, size: 21 }), tr("标准 Bearer 请求头", { size: 21 })] }));
content.push(...code(["Authorization: Bearer zrt-oa-2026"]));
content.push(note("密钥由我方线下提供。密钥错误或缺失时，接口返回 {\"success\":false,\"msg\":\"无效的访问密钥\"}。"));

// ---- 三、请求参数 ----
content.push(h1("三、请求参数"));
content.push(makeTable([1400, 1200, 900, 5526], [
  new TableRow({ children: [headerCell("参数", 1400), headerCell("类型", 1200), headerCell("必填", 900), headerCell("说明", 5526)] }),
  new TableRow({ children: [
    dataCell(mono("month", 20), 1400),
    dataCell("string", 1200),
    dataCell("否", 900),
    dataCell("月份，格式 yyyy-MM，如 2026-08。可放在查询字符串或表单 body 中；省略时返回全部月份（按月份倒序）", 5526),
  ] }),
]));
content.push(new Paragraph({ spacing: { before: 120, after: 60 }, children: [tr("示例请求参数形式（两种等价）：", { bold: true, color: NAVY2, size: 21 })] }));
content.push(...code(["查询字符串：POST /OaApi/GetZhenRenTangStats?month=2026-08"]));
content.push(...code(["表单 body：　POST /OaApi/GetZhenRenTangStats ，body = month=2026-08"]));

// ---- 四、返回说明 ----
content.push(h1("四、返回说明"));
content.push(h2("4.1 成功返回示例"));
content.push(...code([
  "{",
  '  "success": true,',
  '  "data": [',
  "    {",
  '      "序号": 4,',
  '      "月份": "2026-07",',
  '      "应付加工费": 4257.18,',
  '      "应付快递费": 470.00,',
  '      "导入时间": "\\/Date(1786609765923)\\/"',
  "    }",
  "  ]",
  "}",
]));
content.push(h2("4.2 字段说明"));
content.push(makeTable([2500, 1200, 5326], [
  new TableRow({ children: [headerCell("字段", 2500), headerCell("类型", 1200), headerCell("说明", 5326)] }),
  new TableRow({ children: [dataCell(mono("success"), 2500), dataCell("boolean", 1200), dataCell("是否成功", 5326)] }),
  new TableRow({ children: [dataCell(mono("data"), 2500, ZEBRA), dataCell("array", 1200, ZEBRA), dataCell("数据列表，按月份倒序排列", 5326, ZEBRA)] }),
  new TableRow({ children: [dataCell(mono("data[].序号"), 2500), dataCell("number", 1200), dataCell("自增主键", 5326)] }),
  new TableRow({ children: [dataCell(mono("data[].月份"), 2500, ZEBRA), dataCell("string", 1200, ZEBRA), dataCell("统计月份，格式 yyyy-MM", 5326, ZEBRA)] }),
  new TableRow({ children: [dataCell(mono("data[].应付加工费"), 2500), dataCell("number", 1200), dataCell("该月应付加工费（合计）", 5326)] }),
  new TableRow({ children: [dataCell(mono("data[].应付快递费"), 2500, ZEBRA), dataCell("number", 1200, ZEBRA), dataCell("该月应付快递费（合计）", 5326, ZEBRA)] }),
  new TableRow({ children: [dataCell(mono("data[].导入时间"), 2500), dataCell("string", 1200), dataCell("导入时间（微软 JSON 日期格式，形如 \\/Date(毫秒时间戳)\\/）", 5326)] }),
]));
content.push(note("导入时间为 .NET 序列化的日期格式，如需普通时间可解析括号内毫秒时间戳：new Date(1786609765923)。若某月份尚未导入，data 为空数组 []，success 仍为 true。"));
content.push(h2("4.3 失败返回"));
content.push(makeTable([2800, 6226], [
  new TableRow({ children: [headerCell("场景", 2800), headerCell("返回", 6226)] }),
  new TableRow({ children: [dataCell("密钥缺失或错误", 2800), dataCell(mono('{"success":false,"msg":"无效的访问密钥"}', 19), 6226)] }),
  new TableRow({ children: [dataCell("服务异常", 2800, ZEBRA), dataCell(mono('{"success":false,"msg":"<异常信息>"}', 19), 6226, ZEBRA)] }),
]));

// ---- 五、调用示例 ----
content.push(h1("五、调用示例"));
content.push(h2("5.1 curl"));
content.push(...code([
  "# 查询指定月份",
  'curl -X POST "http://172.68.1.12:8080/OaApi/GetZhenRenTangStats" \\',
  '  -H "X-Api-Key: zrt-oa-2026" \\',
  '  -d "month=2026-07"',
  "# 查询全部月份",
  'curl -X POST "http://172.68.1.12:8080/OaApi/GetZhenRenTangStats" \\',
  '  -H "X-Api-Key: zrt-oa-2026" \\',
  '  -d ""',
]));
content.push(note("⚠️ 注意：POST 请求必须带 body（哪怕为空 -d \"\"），否则 IIS 会返回 HTTP 411「Length Required」。"));
content.push(h2("5.2 JavaScript（fetch）"));
content.push(...code([
  "const res = await fetch('http://172.68.1.12:8080/OaApi/GetZhenRenTangStats', {",
  "  method: 'POST',",
  "  headers: { 'Content-Type': 'application/x-www-form-urlencoded',",
  "            'X-Api-Key': 'zrt-oa-2026' },",
  "  body: 'month=2026-07'",
  "});",
  "const json = await res.json();",
  "if (json.success) {",
  "  console.log(json.data);",
  "} else {",
  "  console.error(json.msg);",
  "}",
]));
content.push(h2("5.3 C#（HttpClient）"));
content.push(...code([
  "using (var client = new HttpClient())",
  "{",
  '    client.DefaultRequestHeaders.Add("X-Api-Key", "zrt-oa-2026");',
  "    var content = new FormUrlEncodedContent(new[] {",
  '        new KeyValuePair<string, string>("month", "2026-07")',
  "    });",
  "    var resp = await client.PostAsync(",
  '        "http://172.68.1.12:8080/OaApi/GetZhenRenTangStats", content);',
  "    var json = await resp.Content.ReadAsStringAsync();",
  "}",
]));
content.push(h2("5.4 Java（OkHttp）"));
content.push(...code([
  "OkHttpClient client = new OkHttpClient();",
  "RequestBody body = new FormBody.Builder()",
  '        .add("month", "2026-07")',
  "        .build();",
  "Request request = new Request.Builder()",
  '        .url("http://172.68.1.12:8080/OaApi/GetZhenRenTangStats")',
  "        .post(body)",
  '        .header("X-Api-Key", "zrt-oa-2026")',
  "        .build();",
  "try (Response response = client.newCall(request).execute()) {",
  "    System.out.println(response.body().string());",
  "}",
]));

// ---- 六、常见问题 ----
content.push(h1("六、常见问题"));
const faqs = [
  ["1. 为什么报 411？", "　服务器为 IIS，POST 请求必须包含 body（Content-Length 不能为空）。用 curl 时请加 -d \"\" 或实际参数。"],
  ["2. 返回「无效的访问密钥」？", "　请确认请求头名称 X-Api-Key 拼写正确、密钥与我方提供的一致。"],
  ["3. 查不到某月数据？", "　该月尚未在「加工费及快递费汇总」页面执行「导入到OA」。需足月查询（整月 1 日~月末）后才能导入。"],
  ["4. 同一月份可以导入多次吗？", "　不可以。重复导入会提示「该月份已导入」，需先删除表中该月数据。"],
];
faqs.forEach(([q, a]) => content.push(new Paragraph({
  spacing: { after: 100 }, children: [tr(q, { bold: true, color: NAVY2, size: 21 }), tr(a, { size: 21 })],
})));

// ---- 七、相关页面 ----
content.push(h1("七、相关页面"));
content.push(makeTable([2500, 6526], [
  new TableRow({ children: [headerCell("功能", 2500), headerCell("位置", 6526)] }),
  new TableRow({ children: [dataCell("数据来源（导入）", 2500), dataCell("医院信息系统 → 药品信息核对 → 加工费及快递费汇总 → 「导入到OA」", 6526)] }),
  new TableRow({ children: [dataCell("数据表", 2500, ZEBRA), dataCell([mono("fghis5..上海真仁堂统计汇总"), tr("（字段：序号、月份、应付加工费、应付快递费、导入时间）", { size: 20 })], 6526, ZEBRA)] }),
]));
content.push(new Paragraph({
  spacing: { before: 400 }, alignment: AlignmentType.CENTER,
  children: [tr("— 文档结束，如有疑问请联系信息科 —", { italic: true, color: GRAY, size: 20, font: YAHEI })],
}));

/* ============ 组装文档 ============ */
const page = {
  size: { width: 11906, height: 16838 },
  margin: { top: 1440, right: 1440, bottom: 1440, left: 1440, header: 720, footer: 720 },
};

const doc = new Document({
  creator: "信息科",
  title: "上海真仁堂统计汇总 查询接口文档",
  description: "第三方接口对接文档",
  styles: {
    default: {
      document: { run: { font: font(CALIBRI), size: 21, color: INK } },
    },
  },
  sections: [
    { properties: { page }, children: coverChildren },
    {
      properties: { page },
      headers: { default: contentHeader },
      footers: { default: contentFooter },
      children: content,
    },
  ],
});

const out = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "上海真仁堂统计汇总-接口文档.docx");
const buffer = await Packer.toBuffer(doc);
fs.writeFileSync(out, buffer);
console.log("生成成功:", out, buffer.length, "字节");
