using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using XYHMember.Context;

namespace XYHMember
{
    /// <summary>套餐自动扣减结果摘要</summary>
    public class PackageDeductSummary
    {
        public int 总事件 { get; set; }
        public int 已扣减单数 { get; set; }
        public int 已扣减明细行数 { get; set; }
        public int 未维护 { get; set; }
        public int 未配置耗材 { get; set; }
        public int 重复 { get; set; }
        public int 库存不足 { get; set; }
        public int 失败 { get; set; }
        public List<string> 库存不足明细 { get; set; } = new List<string>();
        public List<string> 失败明细 { get; set; } = new List<string>();
    }

    /// <summary>套餐使用事件（门诊_收费明细表 按 结帐ID+处方ID+套餐名称 聚合）</summary>
    public class PackageUsageEvent
    {
        public int 结帐ID { get; set; }
        public int 处方ID { get; set; }
        public int 就诊ID { get; set; }
        public string 套餐名称 { get; set; }
        public string 日期 { get; set; }   // yyyyMMdd
        public string 姓名 { get; set; }
    }

    /// <summary>
    /// 套餐耗材自动扣减核心逻辑。
    /// 扫描 HIS 门诊_收费明细表里某日期区间的套餐使用事件，按「套餐耗材明细」整单扣减
    /// 耗材入库表.剩余数量，并生成耗材出库单（备注/来源类型=套餐自动扣减）。
    /// 幂等：来源标识(结帐ID_处方ID_套餐名称) 唯一索引保证不重复扣；库存不足整单跳过。
    /// </summary>
    public static class PackageAutoDeductService
    {
        public static PackageDeductSummary Run(DateTime bdate, DateTime edate, string 登记人)
        {
            var sum = new PackageDeductSummary();
            using (var db = new XYHDbContext())
            {
                var b = bdate.ToString("yyyyMMdd");
                var e = edate.ToString("yyyyMMdd");

                var events = db.Database.SqlQuery<PackageUsageEvent>(
                    @"SELECT b.结帐ID, b.处方ID, b.就诊ID, LTRIM(RTRIM(b.套餐名称)) AS 套餐名称, b.日期, MAX(a.姓名) AS 姓名
                      FROM fghis5..门诊_收费明细表 b
                      JOIN fghis5..门诊_收费发票表 a ON a.结帐ID = b.结帐ID
                      WHERE b.套餐名称 IS NOT NULL AND b.套餐名称 <> ''
                        AND b.日期 BETWEEN @bdate AND @edate
                      GROUP BY b.结帐ID, b.处方ID, b.就诊ID, b.套餐名称, b.日期
                      ORDER BY b.日期",
                    new SqlParameter("@bdate", b),
                    new SqlParameter("@edate", e)).ToList();

                sum.总事件 = events.Count;

                var 单号前缀 = "TC" + DateTime.Now.ToString("yyyyMMddHHmmss");
                var 序号 = 0;

                foreach (var ev in events)
                {
                    try
                    {
                        var 套餐名称 = (ev.套餐名称 ?? "").Trim();
                        if (套餐名称 == "") { sum.未维护++; continue; }

                        var pkg = db.Database.SqlQuery<PackageItem>(
                            @"SELECT 序号, 套餐名称, 备注 FROM fghis5..套餐表 WHERE LTRIM(RTRIM(套餐名称)) = @name",
                            new SqlParameter("@name", 套餐名称)).FirstOrDefault();
                        if (pkg == null) { sum.未维护++; continue; }

                        var 来源标识 = ev.结帐ID + "_" + ev.处方ID + "_" + 套餐名称;
                        var dup = db.Database.SqlQuery<int>(
                            "SELECT COUNT(*) FROM fghis5..耗材出库单 WHERE 来源标识 = @标识",
                            new SqlParameter("@标识", 来源标识)).FirstOrDefault();
                        if (dup > 0) { sum.重复++; continue; }

                        var lines = db.Database.SqlQuery<PackageMaterial>(
                            @"SELECT 序号, 套餐ID, 物料编码, 耗材名称, 规格型号, 单位, 数量
                              FROM fghis5..套餐耗材明细 WHERE 套餐ID = @套餐ID",
                            new SqlParameter("@套餐ID", pkg.序号)).ToList();
                        if (lines.Count == 0) { sum.未配置耗材++; continue; }

                        // 库存校验（不足 → 整单跳过）
                        var shortage = new List<string>();
                        foreach (var l in lines)
                        {
                            var need = l.数量 ?? 0;
                            if (need <= 0) continue;
                            var avail = db.Database.SqlQuery<decimal?>(
                                "SELECT SUM(剩余数量) FROM fghis5..耗材入库表 WHERE 状态='已审核' AND 物料编码=@编码 AND 规格=@规格 AND 剩余数量 > 0",
                                new SqlParameter("@编码", (object)l.物料编码 ?? DBNull.Value),
                                new SqlParameter("@规格", (object)l.规格型号 ?? DBNull.Value)).FirstOrDefault() ?? 0m;
                            if (avail < need)
                                shortage.Add((l.耗材名称 ?? "") + "（" + (l.规格型号 ?? "") + "）需 " + need + " " + (l.单位 ?? "") + "，仅剩 " + avail);
                        }
                        if (shortage.Count > 0)
                        {
                            sum.库存不足++;
                            sum.库存不足明细.Add("套餐《" + 套餐名称 + "》就诊ID " + ev.就诊ID + "：" + string.Join("；", shortage));
                            continue;
                        }

                        // 事务内：插出库单 + 逐耗材按 FEFO 跨批扣减
                        using (var tx = db.Database.BeginTransaction())
                        {
                            try
                            {
                                序号++;
                                var 单号 = 单号前缀 + 序号.ToString("D2");
                                var 出库D = DateTime.ParseExact(ev.日期, "yyyyMMdd", null);

                                db.Database.ExecuteSqlCommand(
                                    @"INSERT INTO fghis5..耗材出库单 (出库单号, 出库日期, 领用人, 发料人签字, 登记人, 登记时间, 备注, 来源类型, 来源标识)
                                      VALUES (@出库单号, @出库日期, @领用人, '系统自动', @登记人, GETDATE(), '套餐自动扣减', '套餐自动扣减', @来源标识)",
                                    new SqlParameter("@出库单号", 单号),
                                    new SqlParameter("@出库日期", 出库D),
                                    new SqlParameter("@领用人", (object)ev.姓名 ?? DBNull.Value),
                                    new SqlParameter("@登记人", 登记人 ?? ""),
                                    new SqlParameter("@来源标识", 来源标识));

                                foreach (var l in lines)
                                {
                                    var need = l.数量 ?? 0;
                                    if (need <= 0) continue;

                                    var batches = db.Database.SqlQuery<LocalMaterialInbound>(
                                        @"SELECT 序号, CONVERT(varchar(19), 入库日期, 120) AS 入库日期,
                                                 单号, 仓库, 物料编码, 物料名称, 规格, 产地编码, 产地名称, 批号,
                                                 CONVERT(varchar(10), 有效期, 120) AS 有效期, 单位, 数量, 入库人, 物料类别, 审核时间, 审核人, 状态, 剩余数量
                                          FROM fghis5..耗材入库表
                                          WHERE 状态='已审核' AND 物料编码=@编码 AND 规格=@规格 AND 剩余数量 > 0
                                          ORDER BY ISNULL(有效期, '9999-12-31'), 序号",
                                        new SqlParameter("@编码", (object)l.物料编码 ?? DBNull.Value),
                                        new SqlParameter("@规格", (object)l.规格型号 ?? DBNull.Value)).ToList();

                                    var remaining = need;
                                    foreach (var b2 in batches)
                                    {
                                        if (remaining <= 0) break;
                                        var take = Math.Min(remaining, b2.剩余数量 ?? 0);
                                        if (take <= 0) continue;

                                        db.Database.ExecuteSqlCommand(
                                            @"INSERT INTO fghis5..耗材出库明细 (出库单号, 关联入库序号, 物料编码, 耗材名称, 规格型号, 单位, 批号, 领用数量, 申领日期, 到库日期, 保质期, 备注)
                                              VALUES (@出库单号, @关联入库序号, @物料编码, @耗材名称, @规格型号, @单位, @批号, @领用数量, @申领日期, NULL, @保质期, @备注)",
                                            new SqlParameter("@出库单号", 单号),
                                            new SqlParameter("@关联入库序号", b2.序号),
                                            new SqlParameter("@物料编码", (object)b2.物料编码 ?? DBNull.Value),
                                            new SqlParameter("@耗材名称", (object)b2.物料名称 ?? DBNull.Value),
                                            new SqlParameter("@规格型号", (object)b2.规格 ?? DBNull.Value),
                                            new SqlParameter("@单位", (object)b2.单位 ?? DBNull.Value),
                                            new SqlParameter("@批号", (object)b2.批号 ?? DBNull.Value),
                                            new SqlParameter("@领用数量", take),
                                            new SqlParameter("@申领日期", 出库D),
                                            new SqlParameter("@保质期", (object)ParseDate(b2.有效期) ?? DBNull.Value),
                                            new SqlParameter("@备注", (object)(string.IsNullOrWhiteSpace(l.备注) ? "固定消耗" : l.备注) ?? DBNull.Value));

                                        db.Database.ExecuteSqlCommand(
                                            "UPDATE fghis5..耗材入库表 SET 剩余数量 = 剩余数量 - @qty WHERE 序号 = @id",
                                            new SqlParameter("@qty", take),
                                            new SqlParameter("@id", b2.序号));

                                        remaining -= take;
                                        sum.已扣减明细行数++;
                                    }
                                    if (remaining > 0)
                                        throw new Exception("扣减时库存不足（" + (l.耗材名称 ?? "") + " 差 " + remaining + "）");
                                }

                                tx.Commit();
                                sum.已扣减单数++;
                            }
                            catch (Exception ex)
                            {
                                tx.Rollback();
                                sum.失败++;
                                sum.失败明细.Add("套餐《" + 套餐名称 + "》就诊ID " + ev.就诊ID + "：" + ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sum.失败++;
                        sum.失败明细.Add("套餐《" + (ev.套餐名称 ?? "") + "》就诊ID " + ev.就诊ID + "：" + ex.Message);
                    }
                }
            }
            return sum;
        }

        private static DateTime? ParseDate(string s)
        {
            DateTime d;
            if (DateTime.TryParse(s, out d)) return d;
            return null;
        }
    }
}
