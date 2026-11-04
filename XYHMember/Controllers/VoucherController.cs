using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Web;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    /// <summary>
    /// 抵扣券管理：350 元锁客券的发放、维护和费用明细。
    /// 350 元独立记账，不写入储值卡 MS_BRZH / MS_SZMX。
    /// </summary>
    [AuthFilter]
    public class VoucherController : Controller
    {
        private XYHDbContext db = new XYHDbContext();
        private const decimal VoucherAmount = 350m;

        // ===== 发券 =====

        // 发券页：按 PID / 姓名搜索病人
        public ActionResult Issue()
        {
            return View(QueryPatients((Request["pid"] ?? "").Trim(), (Request["name"] ?? "").Trim()));
        }

        [HttpGet]
        public ActionResult GetPatientsForIssue()
        {
            return View("Issue", QueryPatients((Request["pid"] ?? "").Trim(), (Request["name"] ?? "").Trim()));
        }

        private List<VoucherPatient> QueryPatients(string pidText, string name)
        {
            int pid = 0;
            if (pidText.Length > 0 && !int.TryParse(pidText, out pid))
                return new List<VoucherPatient>();

            string sql = @"
select top 100 a.PID,
       cast(a.门诊号 as varchar(12)) as 门诊号,
       a.姓名,
       case when a.性别 = 1 then '男' else '女' end as 性别,
       a.身份证号,
       a.联系手机,
       case when exists (select 1 from hisdata..抵扣券表 v
                         where v.PID = a.PID and v.状态 in (N'未使用', N'已使用'))
            then '已发券' else '未发券' end as 是否已发券
from fghis5..系统_病人基本信息表 a
where (@pid = 0 or a.PID = @pid)
  and (@name = '' or a.姓名 = @name)
order by a.创建时间 desc";

            return db.Database.SqlQuery<VoucherPatient>(sql,
                new SqlParameter("@pid", pid),
                new SqlParameter("@name", name)).ToList();
        }

        // 发券弹窗
        [HttpGet]
        public ActionResult IssueForm(int pid)
        {
            var patient = QueryPatient(pid);
            if (patient == null)
                return HttpNotFound();

            ViewBag.券面额 = VoucherAmount;
            return View(patient);
        }

        private VoucherPatient QueryPatient(int pid)
        {
            string sql = @"
select a.PID,
       cast(a.门诊号 as varchar(12)) as 门诊号,
       a.姓名,
       case when a.性别 = 1 then '男' else '女' end as 性别,
       a.身份证号,
       a.联系手机,
       '' as 是否已发券
from fghis5..系统_病人基本信息表 a
where a.PID = @pid";

            return db.Database.SqlQuery<VoucherPatient>(sql, new SqlParameter("@pid", pid)).SingleOrDefault();
        }

        // 发券落库：事务内保证同一病人只有一张有效券
        [HttpPost]
        public ActionResult IssueVoucher(int pid)
        {
            var payWay = (Request["payway"] ?? "").Trim();
            var remark = (Request["remark"] ?? "").Trim();

            if (payWay.Length == 0)
                return AlertAndBack("请填写支付方式");

            var patient = QueryPatient(pid);
            if (patient == null)
                return Content("<script>alert('病人不存在');window.close();</script>");

            var jobNumber = GetCurrentJobNumber();

            using (var tran = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    // 锁住该病人的有效券范围，避免两个窗口同时发券
                    var exists = db.Database.SqlQuery<int>(
                        @"select count(1) from hisdata..抵扣券表 with (UPDLOCK, HOLDLOCK)
                          where PID = @pid and 状态 in (N'未使用', N'已使用')",
                        new SqlParameter("@pid", pid)).Single();

                    if (exists > 0)
                    {
                        tran.Rollback();
                        return AlertAndBack("该病人已有有效抵扣券，不能重复发券");
                    }

                    var code = GenerateUniqueCode();

                    string sqlInsert = @"
insert into hisdata..抵扣券表
    (券码, PID, 门诊号, 姓名, 手机, 券面额, 已用金额, 状态, 支付方式, 发券工号, 发券时间, 备注)
values
    (@code, @pid, @mzhm, @name, @phone, @amount, 0, N'未使用', @pay, @job, GETDATE(), @remark);
select cast(scope_identity() as int);";

                    var voucherId = db.Database.SqlQuery<int>(sqlInsert,
                        new SqlParameter("@code", code),
                        new SqlParameter("@pid", pid),
                        new SqlParameter("@mzhm", patient.门诊号 ?? ""),
                        new SqlParameter("@name", patient.姓名 ?? ""),
                        new SqlParameter("@phone", patient.联系手机 ?? ""),
                        new SqlParameter("@amount", VoucherAmount),
                        new SqlParameter("@pay", payWay),
                        new SqlParameter("@job", jobNumber),
                        new SqlParameter("@remark", remark)).Single();

                    db.Database.ExecuteSqlCommand(@"
insert into hisdata..抵扣券流水表
    (券序号, 类型, 发生金额, 券余额, 操作工号, 操作时间, 备注)
values
    (@id, N'发券', @amount, @amount, @job, GETDATE(), @remark)",
                        new SqlParameter("@id", voucherId),
                        new SqlParameter("@amount", VoucherAmount),
                        new SqlParameter("@job", jobNumber),
                        new SqlParameter("@remark", remark));

                    tran.Commit();

                    // 发券成功后直接把券码展示给操作员
                    return Content("<script>alert('发券成功，券码：" + code + "');window.close(); window.opener.location.reload();</script>");
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return AlertAndBack("发券失败：" + ex.Message);
                }
            }
        }

        // DKQ + 8 位随机数字；券码唯一索引兜底
        private string GenerateUniqueCode()
        {
            for (int i = 0; i < 10; i++)
            {
                string code;
                using (var rng = new RNGCryptoServiceProvider())
                {
                    var bytes = new byte[4];
                    rng.GetBytes(bytes);
                    var number = BitConverter.ToUInt32(bytes, 0) % 100000000u;
                    code = "DKQ" + number.ToString("D8");
                }

                var count = db.Database.SqlQuery<int>(
                    "select count(1) from hisdata..抵扣券表 where 券码 = @code",
                    new SqlParameter("@code", code)).Single();

                if (count == 0)
                    return code;
            }

            throw new InvalidOperationException("券码生成失败，请重试");
        }

        // ===== 抵扣券维护 =====

        public ActionResult VoucherList()
        {
            return View(QueryVouchers("", "", "", ""));
        }

        [HttpGet]
        public ActionResult GetVoucherList()
        {
            return View("VoucherList", QueryVouchers(
                (Request["name"] ?? "").Trim(),
                (Request["code"] ?? "").Trim(),
                (Request["phone"] ?? "").Trim(),
                (Request["status"] ?? "").Trim()));
        }

        private List<DeductionVoucher> QueryVouchers(string name, string code, string phone, string status)
        {
            string sql = @"
select 序号, 券码, PID, 门诊号, 姓名, 手机, 券面额, 已用金额, 状态,
       支付方式, 发券工号, 发券时间, 作废工号, 作废时间, 备注
from hisdata..抵扣券表
where (@name = '' or 姓名 = @name)
  and (@code = '' or 券码 = @code)
  and (@phone = '' or 手机 = @phone)
  and (@status = '' or 状态 = @status)
order by 序号 desc";

            return db.Database.SqlQuery<DeductionVoucher>(sql,
                new SqlParameter("@name", name),
                new SqlParameter("@code", code),
                new SqlParameter("@phone", phone),
                new SqlParameter("@status", status)).ToList();
        }

        [HttpGet]
        public ActionResult Edit(int id)
        {
            var voucher = GetVoucher(id);
            if (voucher == null)
                return HttpNotFound();

            return View(voucher);
        }

        private DeductionVoucher GetVoucher(int id)
        {
            string sql = @"
select 序号, 券码, PID, 门诊号, 姓名, 手机, 券面额, 已用金额, 状态,
       支付方式, 发券工号, 发券时间, 作废工号, 作废时间, 备注
from hisdata..抵扣券表
where 序号 = @id";

            return db.Database.SqlQuery<DeductionVoucher>(sql, new SqlParameter("@id", id)).SingleOrDefault();
        }

        // 维护：只允许改手机、支付方式、备注
        [HttpPost]
        public ActionResult UpdateVoucher(int id)
        {
            var phone = (Request["phone"] ?? "").Trim();
            var payWay = (Request["payway"] ?? "").Trim();
            var remark = (Request["remark"] ?? "").Trim();

            int rows = db.Database.ExecuteSqlCommand(@"
update hisdata..抵扣券表
set 手机 = @phone, 支付方式 = @pay, 备注 = @remark
where 序号 = @id and 状态 <> N'已作废'",
                new SqlParameter("@phone", phone),
                new SqlParameter("@pay", payWay),
                new SqlParameter("@remark", remark),
                new SqlParameter("@id", id));

            if (rows == 0)
                return AlertAndBack("保存失败：抵扣券不存在或已作废");

            return Content("<script>window.close(); window.opener.location.reload();</script>");
        }

        // 作废：只允许未使用的券
        [HttpPost]
        public ActionResult VoidVoucher(int id)
        {
            var reason = (Request["reason"] ?? "").Trim();
            var jobNumber = GetCurrentJobNumber();

            using (var tran = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    var voucher = db.Database.SqlQuery<DeductionVoucher>(@"
select 序号, 券码, PID, 门诊号, 姓名, 手机, 券面额, 已用金额, 状态,
       支付方式, 发券工号, 发券时间, 作废工号, 作废时间, 备注
from hisdata..抵扣券表 with (UPDLOCK, HOLDLOCK)
where 序号 = @id",
                        new SqlParameter("@id", id)).SingleOrDefault();

                    if (voucher == null)
                    {
                        tran.Rollback();
                        return AlertAndBack("抵扣券不存在");
                    }

                    if (voucher.状态 != "未使用")
                    {
                        tran.Rollback();
                        return AlertAndBack("只有未使用的抵扣券可以作废");
                    }

                    db.Database.ExecuteSqlCommand(@"
update hisdata..抵扣券表
set 状态 = N'已作废', 作废工号 = @job, 作废时间 = GETDATE()
where 序号 = @id and 状态 = N'未使用'",
                        new SqlParameter("@job", jobNumber),
                        new SqlParameter("@id", id));

                    db.Database.ExecuteSqlCommand(@"
insert into hisdata..抵扣券流水表
    (券序号, 类型, 发生金额, 券余额, 操作工号, 操作时间, 备注)
values
    (@id, N'作废', @amount, 0, @job, GETDATE(), @remark)",
                        new SqlParameter("@id", id),
                        new SqlParameter("@amount", -voucher.券面额),
                        new SqlParameter("@job", jobNumber),
                        new SqlParameter("@remark", reason));

                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return AlertAndBack("作废失败：" + ex.Message);
                }
            }

            return RedirectToAction("VoucherList");
        }

        // ===== 费用明细 =====

        public ActionResult Detail()
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            return ShowDetail(today, today, "", "");
        }

        [HttpGet]
        public ActionResult GetVoucherDetail()
        {
            var bdate = (Request["bdatepicker"] ?? DateTime.Today.ToString("yyyy-MM-dd")).Trim();
            var edate = (Request["edatepicker"] ?? DateTime.Today.ToString("yyyy-MM-dd")).Trim();
            var name = (Request["name"] ?? "").Trim();
            var type = (Request["type"] ?? "").Trim();

            return ShowDetail(bdate, edate, name, type);
        }

        private ActionResult ShowDetail(string bdate, string edate, string name, string type)
        {
            var begin = bdate + " 00:00:00";
            var end = edate + " 23:59:59";

            var summary = db.Database.SqlQuery<VoucherSummary>(@"
select
    isnull(sum(case when m.类型 = N'发券' then 1 else 0 end), 0) as 发券张数,
    isnull(sum(case when m.类型 = N'发券' then m.发生金额 else 0 end), 0) as 发券金额,
    isnull(sum(case when m.类型 = N'作废' then 1 else 0 end), 0) as 作废张数,
    isnull(sum(case when m.类型 = N'作废' then m.发生金额 else 0 end), 0) as 作废金额,
    (select count(1) from hisdata..抵扣券表 where 状态 = N'未使用') as 未使用张数,
    (select isnull(sum(券面额 - 已用金额), 0) from hisdata..抵扣券表 where 状态 = N'未使用') as 未使用金额
from hisdata..抵扣券流水表 m
left join hisdata..抵扣券表 v on m.券序号 = v.序号
where m.操作时间 between @bdate and @edate
  and (@name = '' or v.姓名 = @name)
  and (@type = '' or m.类型 = @type)",
                new SqlParameter("@bdate", begin),
                new SqlParameter("@edate", end),
                new SqlParameter("@name", name),
                new SqlParameter("@type", type)).Single();

            ViewBag.发券张数 = summary.发券张数;
            ViewBag.发券金额 = summary.发券金额;
            ViewBag.作废张数 = summary.作废张数;
            ViewBag.作废金额 = summary.作废金额;
            ViewBag.未使用张数 = summary.未使用张数;
            ViewBag.未使用金额 = summary.未使用金额;

            string sql = @"
select m.流水号, m.券序号, v.券码, m.类型, m.发生金额, m.券余额,
       v.门诊号, v.姓名, m.操作工号, m.操作时间, m.备注, m.关联结帐ID, m.关联发票号
from hisdata..抵扣券流水表 m
left join hisdata..抵扣券表 v on m.券序号 = v.序号
where m.操作时间 between @bdate and @edate
  and (@name = '' or v.姓名 = @name)
  and (@type = '' or m.类型 = @type)
order by m.流水号 desc";

            var list = db.Database.SqlQuery<DeductionVoucherDetail>(sql,
                new SqlParameter("@bdate", begin),
                new SqlParameter("@edate", end),
                new SqlParameter("@name", name),
                new SqlParameter("@type", type)).ToList();

            return View("Detail", list);
        }

        // ===== 公共 =====

        private string GetCurrentJobNumber()
        {
            var userId = ((int?)Session["UserId"]) ?? 1;
            var user = db.Users.Find(userId);
            return user == null ? "" : (user.JobNumber ?? "");
        }

        private ActionResult AlertAndBack(string message)
        {
            return Content("<script>alert('" + HttpUtility.JavaScriptStringEncode(message) + "');</script>");
        }
    }
}
