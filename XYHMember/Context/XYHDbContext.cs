using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Web;
using XYHMember.Models;

namespace XYHMember.Context
{
    public class XYHDbContext:DbContext
    {
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 明确指定 XYHUser 实体映射到 "XYHUser" 表
            modelBuilder.Entity<XYHUserEntity>().ToTable("XYHUser");
        }

        public XYHDbContext() : base("name=XYHdb")
        {
            // 可以在这里配置数据库上下文的其他设置，例如关闭自动检测更改
            this.Configuration.AutoDetectChangesEnabled = false;
        }


        public DbSet<XYHUserEntity> Users { get; set; }
        public DbSet<MS_BRZH> MS_BRZH { get; set; }   
        public DbSet<Models.MS_SZMX> MS_SZMX { get; set; }

    }

    public class XYHUserEntity
    {
        [Key]
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string JobNumber { get; set; }
        public string Name { get; set; }
        // ...其他属性...
    }


    public class MS_SZMX
    {
        [Key]
        public decimal 序号 { get; set; }
        public decimal 门诊号 { get; set; }
        public string 姓名 { get; set; }
        public decimal 借方金额 { get; set; }
        public decimal 贷方金额 { get; set; }
        public string 操作工号 { get; set; }
        public DateTime 操作日期 { get; set; }
        public string 备注 { get; set; }
        public string 折扣金额 { get; set; }
        public string 关联销售 { get; set; }
        // ...其他属性...
    }

    public class PatientInfo
    {
        public int PID { get; set; }
        public int 门诊号 { get; set; }
        public string 姓名 { get; set; }
        public string 性别 { get; set; }
        public string 身份证号 { get; set; }
        public string 联系手机 { get; set; }
        public decimal 余额 { get; set; }
        public string 是否开卡 { get; set; }
    }

    public class Member
    {
        public int PID { get; set; }
        public int 门诊号 { get; set; }
        public string 姓名 { get; set; }
        public string 性别 { get; set; }
        public string 身份证号 { get; set; }
        public string 联系手机 { get; set; }
        public decimal 余额 { get; set; }
    }

    public class MemberPay
    {
        public int 门诊号 { get; set; }
        public int PID { get; set; }
        public decimal 支付金额 { get; set; }
        public string 支付方式 { get; set; }
        public string 备注 { get; set; }
        public string 折扣金额 { get; set; }
        public string 关联销售 { get; set; }
    }


    //日报表
    public class DailyReports
    {
        public int? 就诊ID { get; set; }
        public int? PID { get; set; }
        public int? 门诊号 { get; set; }
        public string 姓名 { get; set; }
        public string 出生日期 { get; set; }
        public string 身份证号 { get; set; }
        public int? 结帐ID { get; set; }
        public string 结帐日期 { get; set; }
        public string 结帐时间 { get; set; }
        public int? 发票状态 { get; set; }
        public string 类别 { get; set; }
        public string 操作工号 { get; set; }
        public string 关联销售 { get; set; }
        public string 接诊医生 { get; set; }
        public decimal? 总金额 { get; set; } 
        public decimal? 诊查费 { get; set; }
        public decimal? 材料费 { get; set; }
        public decimal? 检查费 { get; set; }
        public decimal? 检验费 { get; set; }
        public decimal? 治疗费 { get; set; }
        public decimal? 中草药 { get; set; }
        public decimal? 理疗费 { get; set; }
        public decimal? 加工费 { get; set; }
        public decimal? 辨证论治费 { get; set; }
        public decimal? 现金 { get; set; }
        public decimal? POS { get; set; }
        public decimal? 储值卡 { get; set; }
        public decimal? 折扣 { get; set; }
        public decimal? 微信 { get; set; }
        public decimal? 支付宝 { get; set; }
        public decimal? 实际支付 { get; set; }

    }

    //各科室查询
    public  class Departments
    {
        public string 结帐日期 { get; set; }
        public string 科室名称 { get; set; }
        public decimal 科室总金额 { get; set; }
    }


    //各医生查询
    public class DoctorInfos
    {
        public string 结帐日期 { get; set; }
        public string 接诊医生 { get; set; }
        public decimal? 总金额 { get; set; }
        public decimal? 诊查费 { get; set; }
        public decimal? 材料费 { get; set; }
        public decimal? 检查费 { get; set; }
        public decimal? 检验费 { get; set; }
        public decimal? 治疗费 { get; set; }
        public decimal? 中草药 { get; set; }
        public decimal? 理疗费 { get; set; }
        public decimal? 加工费 { get; set; }
        public decimal? 辨证论治费 { get; set; }
        public decimal? 现金 { get; set; }
        public decimal? POS { get; set; }
        public decimal? 储值卡 { get; set; }
        public decimal? 折扣 { get; set; }
        public decimal? 微信 { get; set; }
        public decimal? 支付宝 { get; set; }
        public decimal? 实际支付 { get; set; }
    }

    //药品
    public  class Medicaments
    {
        public int 药品ID { get; set; }
        public int 药品通用ID { get; set; }
        public string 药品通用名 { get; set; }
        public string 药品名称 { get; set; }
        public string 商品编号 { get; set; }
        public string 新医保编码 { get; set; }
        public string 折后比例 { get; set; }
        public decimal? 基本零售价 { get; set; }
        public decimal? 常规批发价 { get; set; }
        public decimal? 采购价 { get; set; }
        public decimal? 数量 { get; set; }
        public decimal? 总售价 { get; set; }
        public decimal? 总批发价 { get; set; }
        public decimal? 总成本 { get; set; }
    }


    //接诊明细
    public class JZMX
    {
        public int 就诊ID { get; set; }
        public string 交易卡号 { get; set; }
        public string 姓名 { get; set; }
        public string 接诊医生工号 { get; set; }
        public string 接诊医生姓名 { get; set; }

    }


    //导出excel
    public class ExportData
    {
        public List<string> Headers { get; set; }
        public List<List<object>> Rows { get; set; }
    }

    //项目报表
    public class Manners
    {
        public string 项目名称 { get; set; }
        public decimal? 单价 { get; set; }
        public decimal? 数量 { get; set; }
        public decimal? 金额 { get; set; }
    }

    //体检病人信息
    public class HealthExamPatient
    {
        public string 体检号 { get; set; }
        public string 姓名 { get; set; }
        public string 身份证号 { get; set; }
        public string 电话 { get; set; }
        public string 住址 { get; set; }
        public string 门诊号 { get; set; }
    }

    public class PidMapping
    {
        public string 身份证号 { get; set; }
        public int PID { get; set; }
    }

    //分页
    public class PaginationModel<T>
    {
        public List<T> Items { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

}