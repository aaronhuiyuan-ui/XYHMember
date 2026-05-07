using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using XYHMember.Context;

namespace XYHMember.Controllers
{
    [AuthFilter]
    public class MemberController : Controller
    {
        private XYHDbContext db = new XYHDbContext();


        //查询最近100条建档病人信息并显示
        public ActionResult PatientInfo()
        {
            string sqlQuery = @"select  top 100 a.pid,a.门诊号,a.姓名,case when a.性别=1 then '男' else '女'end as 性别,a.身份证号,a.联系手机, case when b.brkh  is  null then '未开卡'else '已开卡' 
        end 是否开卡
		from fghis5..系统_病人基本信息表 a 
		left join hisdata..MS_BRZH b on b.brkh = a.pid
		order by a.创建时间 desc";

            // 执行原生SQL查询
            var result = db.Database.SqlQuery<PatientInfo>(sqlQuery).ToList();

            return View(result);
        }

        //储值卡充值页面
        public ActionResult TopUP(int pid)
        {
            string sqlQuery = @"
    SELECT b.pid, b.门诊号, b.姓名, case when b.性别=1 then '男' else '女'end as 性别, b.身份证号, b.联系手机, a.zhye as 余额
    FROM hisdata..MS_BRZH a
    LEFT JOIN fghis5..系统_病人基本信息表 b ON a.brkh = b.pid
    where b.pid=@pid";

            var result = db.Database.SqlQuery<Member>(sqlQuery, new SqlParameter("@pid", pid)).SingleOrDefault();

            return View(result);

        }

        //储值卡充值操作页面
        [HttpPost]
        public ActionResult TopUPMember(MemberPay member)
        {
            int userId = (int)Session["UserId"];
            member.门诊号 = int.Parse(Request["mzhm"]);
            member.PID = int.Parse(Request["pid"]);
            member.支付金额 = decimal.Parse(Request["pay"]);
            member.支付方式 = Request["state"];
            member.备注 = Request["remark"];
            member.折扣金额 = Request["discount"];
            member.关联销售 = Request["relatedSales"];

            string sqlQuery0 = @"update hisdata..GY_IDENTITY  set VALUE=(select max(SBXH) FROM hisdata..MS_SZMX)+1 where TNAME ='MS_SZMX'";
            string sqlQuery1 = @"update hisdata..MS_BRZH set ZHYE=ZHYE+@zfje where brkh=@pid";
            string sqlQuery2 = @"insert into hisdata..MS_SZMX (SBXH,ID,JFJE,DFJE,CZGH,RQ,ZY) select (select max(SBXH) FROM hisdata..MS_SZMX)+1 as SBXH,@mzhm as ID,@zfje as JFJE,0 as DFJE,(select JobNumber from hisdata..xyhuser where UserId=@id) as 操作工号,
GETDATE() as RQ, @bz as ZY from hisdata..MS_BRDA WHERE MZHM = @pid";
            string sqlQuery3 = @"insert into hisdata..MS_SZMXXG VALUES ( (select max(SBXH) FROM hisdata..MS_SZMX),@zkje,@glxs)";

            // 在 SQL 查询中使用参数可以防止 SQL 注入攻击
            SqlParameter paramPay = new SqlParameter("@zfje", member.支付金额);
            SqlParameter paramPid = new SqlParameter("@pid", member.PID);
            SqlParameter paramId = new SqlParameter("@id", userId);
            SqlParameter paramMzhm = new SqlParameter("@mzhm", member.门诊号);
            SqlParameter paramRemark= new SqlParameter("@bz", member.支付方式+member.备注);
            SqlParameter paramZkje = new SqlParameter("@zkje", member.折扣金额);
            SqlParameter paramGlxs = new SqlParameter("@glxs", member.关联销售);


            // 执行 SQL 修改
            int rowsAffected0 = db.Database.ExecuteSqlCommand(sqlQuery0);
            int rowsAffected1 = db.Database.ExecuteSqlCommand(sqlQuery1, paramPay, paramPid);
            int rowsAffected2 = db.Database.ExecuteSqlCommand(sqlQuery2, paramPay,paramPid, paramId, paramMzhm, paramRemark);
            int rowsAffected3 = db.Database.ExecuteSqlCommand(sqlQuery3, paramZkje, paramGlxs);

            return Content("<script>window.close(); window.opener.location.reload();</script>");
            //return Content("<script>window.parent.location.href = '/Member/Member';</script>");
        }


        //按门诊号码搜索储值卡信息(开卡)
        [HttpGet]
        public ActionResult GetMemberByName()
        {
            //if (pid == null)
            //{
            //    // 如果 mzhm 为空，则返回空结果或者其他适当的处理方式
            //    return View("PatientInfo", new List<PatientInfo>());
            //}

            var name = Request["name"].Trim();

            string sqlQuery = @"select  top 100 a.pid,a.门诊号,a.姓名,case when a.性别=1 then '男' else '女'end as 性别,a.身份证号,a.联系手机, case when b.brkh  is  null then '未开卡'else '已开卡' 
        end 是否开卡
		from fghis5..系统_病人基本信息表 a 
		left join hisdata..MS_BRZH b on b.brkh = a.pid
        where @name='' or a.姓名 = @name order by a.创建时间 desc";

            // 创建一个参数来防止 SQL 注入攻击
            SqlParameter paramMzhm = new SqlParameter("@name", name);

            // 执行原生SQL查询
            var result = db.Database.SqlQuery<PatientInfo>(sqlQuery, paramMzhm).ToList();

            return View("PatientInfo", result);

        }

        //为用户开卡
        [HttpPost]
        public ActionResult AddMemberCard(int pid)
        {
            // pid 参数不为空，可以调用 AddMemberCard 方法
            string sqlQuery = @"INSERT INTO hisdata..MS_BRZH(ID, ZHLB, ZHYE, BRKH, brxm)
                        SELECT ID, 10, 0, MZHM, BRXM FROM hisdata..MS_BRDA WHERE MZHM = @pid";

            // 在 SQL 查询中使用参数可以防止 SQL 注入攻击
            SqlParameter paramPid = new SqlParameter("@pid", pid);

            // 执行 SQL 查询
            int rowsAffected = db.Database.ExecuteSqlCommand(sqlQuery, paramPid);

            // 检查受影响的行数以确定插入操作是否成功
            if (rowsAffected > 0)
            {
                // 插入成功，可以添加适当的处理逻辑
                Console.WriteLine("插入操作成功！");
                return RedirectToAction("PatientInfo", "Member");
            }
            else
            {
                // 插入失败，可能需要添加错误处理逻辑
                string script = "<script>alert('该病人开卡操作失败，请联系管理员');</script>";
                return Content(script, "text/html");


            }

        }



        // GET: Member 查询储值卡信息
        public ActionResult Member()
        {
            string sqlQuery = @"
    SELECT b.pid, b.门诊号, b.姓名, case when b.性别=1 then '男' else '女'end as 性别, b.身份证号, b.联系手机, a.zhye as 余额
    FROM hisdata..MS_BRZH a 
    LEFT JOIN fghis5..系统_病人基本信息表 b ON a.brkh = b.pid
";

            // 执行原生SQL查询
            var result = db.Database.SqlQuery<Member>(sqlQuery).ToList();

            return View(result);
        }

        //搜索储值卡信息包括余额  
        [HttpGet]
        public ActionResult GetMemberByNamePay()
        {
            var name = Request["name"].Trim();

            //if(pid == null)
            //{
            //    // 如果 mzhm 为空，则返回空结果或者其他适当的处理方式
            //    return View("Member", new List<Member>());
            //}

            string sqlQuery = @"SELECT b.pid, b.门诊号, b.姓名, case when b.性别=1 then '男' else '女'end as 性别, b.身份证号, b.联系手机, a.zhye as 余额
                        FROM hisdata..MS_BRZH a 
                        LEFT JOIN fghis5..系统_病人基本信息表 b ON a.brkh = b.pid 
                        WHERE @name='' or b.姓名 = @name";

            // 创建一个参数来防止 SQL 注入攻击
            SqlParameter paramMzhm = new SqlParameter("@name", name);

            // 执行原生SQL查询
            var result = db.Database.SqlQuery<Member>(sqlQuery, paramMzhm).ToList();

            return View("Member", result);

        }

        //查询储值卡费用明细
        public ActionResult Cost()
        {
            //            string sqlQuery = @"select SBXH as 序号,a.ID as 门诊号,b.姓名 ,jfje as 借方金额,dfje as 贷方金额,CZGH as 操作工号,RQ as 操作日期,zy as 备注,c.折扣 as 折扣金额,c.关联销售
            //from hisdata..MS_SZMX a
            //left join FGHIS5..系统_病人基本信息表 b on a.ID=b.门诊号
            //left join hisdata..MS_SZMXXG c on a.sbxh=c.id
            //order by SBXH desc";

            //            var result = db.Database.SqlQuery<MS_SZMX>(sqlQuery).ToList();

            //            return View(result);
            return View();
        }


        //搜索储值卡费用明细
        [HttpGet]
        public ActionResult GetMemberByNameCost()
        {
            var name = Request["name"].Trim();
            var bdate = Request["bdatepicker"]+ " "+ "00:00:00";
            var edate = Request["edatepicker"]+" "+"23:59:59";
            var mySelect = Request.QueryString["mySelect"];

            string sqlQuery = "";

            switch (mySelect)
            {
                case "0":
                    // 处理选择全部的逻辑
                     sqlQuery = @"select SBXH as 序号,a.ID as 门诊号,b.姓名 ,jfje as 借方金额,dfje as 贷方金额,CZGH as 操作工号,RQ as 操作日期,zy as 备注,c.折扣 as 折扣金额,c.关联销售 
from hisdata..MS_SZMX a
left join FGHIS5..系统_病人基本信息表 b on a.ID=b.门诊号
left join hisdata..MS_SZMXXG c on a.sbxh=c.id
where RQ BETWEEN @bdate AND @edate
and (@name='' or b.姓名 = @name)
order by SBXH desc";
                    break;
                case "1":
                    // 处理选择充值的逻辑
                     sqlQuery = @"select SBXH as 序号,a.ID as 门诊号,b.姓名 ,jfje as 借方金额,dfje as 贷方金额,CZGH as 操作工号,RQ as 操作日期,zy as 备注,c.折扣 as 折扣金额,c.关联销售 
from hisdata..MS_SZMX a
left join FGHIS5..系统_病人基本信息表 b on a.ID=b.门诊号
left join hisdata..MS_SZMXXG c on a.sbxh=c.id
where RQ BETWEEN @bdate AND @edate  
and (@name='' or b.姓名 = @name)
and jfje >0
order by SBXH desc";
                    break;
                case "2":
                    // 处理选择收费的逻辑
                     sqlQuery = @"select SBXH as 序号,a.ID as 门诊号,b.姓名 ,jfje as 借方金额,dfje as 贷方金额,CZGH as 操作工号,RQ as 操作日期,zy as 备注,c.折扣 as 折扣金额,c.关联销售 
from hisdata..MS_SZMX a
left join FGHIS5..系统_病人基本信息表 b on a.ID=b.门诊号
left join hisdata..MS_SZMXXG c on a.sbxh=c.id
where RQ BETWEEN @bdate AND @edate  
and (@name='' or b.姓名 = @name)
and dfje>0
order by SBXH desc";
                    break;
                case "3":
                    // 处理选择销卡的逻辑
                     sqlQuery = @"select SBXH as 序号,a.ID as 门诊号,b.姓名 ,jfje as 借方金额,dfje as 贷方金额,CZGH as 操作工号,RQ as 操作日期,zy as 备注,c.折扣 as 折扣金额,c.关联销售 
from hisdata..MS_SZMX a
left join FGHIS5..系统_病人基本信息表 b on a.ID=b.门诊号
left join hisdata..MS_SZMXXG c on a.sbxh=c.id
where RQ BETWEEN @bdate AND @edate  
and (@name='' or b.姓名 = @name)
and jfje < 0
order by SBXH desc";
                    break;
            }

           

            // 创建一个参数来防止 SQL 注入攻击
            SqlParameter paramMzhm = new SqlParameter("@name", name);
            SqlParameter parambdate = new SqlParameter("@bdate", bdate);
            SqlParameter paramedate = new SqlParameter("@edate", edate);

            // 执行原生SQL查询
            var result = db.Database.SqlQuery<MS_SZMX>(sqlQuery, paramMzhm, parambdate, paramedate).ToList();

            return View("Cost", result);

        }



    }
}