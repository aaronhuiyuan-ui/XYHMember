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
            modelBuilder.Entity<MedicalTechRegistration>().ToTable("医技登记表");
            modelBuilder.Entity<MedicalTechExecution>().ToTable("医技执行记录表");
        }

        public XYHDbContext() : base("name=XYHdb")
        {
            // 可以在这里配置数据库上下文的其他设置，例如关闭自动检测更改
            this.Configuration.AutoDetectChangesEnabled = false;
        }


        public DbSet<XYHUserEntity> Users { get; set; }
        public DbSet<MS_BRZH> MS_BRZH { get; set; }
        public DbSet<Models.MS_SZMX> MS_SZMX { get; set; }
        public DbSet<MedicalTechRegistration> MedicalTechRegistrations { get; set; }
        public DbSet<MedicalTechExecution> MedicalTechExecutions { get; set; }
        public DbSet<MedicalTechStaff> MedicalTechStaffs { get; set; }
        public DbSet<MedicalTechDefaultCount> MedicalTechDefaultCounts { get; set; }
        public DbSet<PrescriptionResultCache> PrescriptionResultCaches { get; set; }

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
        public decimal? 工艺品 { get; set; }
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
        public string 折扣比例 { get; set; }
        public string 备注 { get; set; }

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
        public decimal? 工艺品 { get; set; }
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
        public string 检查日期 { get; set; }
    }

    public class SavedPatient
    {
        public string 门诊号 { get; set; }
        public string 姓名 { get; set; }
        public string 身份证号 { get; set; }
        public string 电话 { get; set; }
        public string 创建时间 { get; set; }
    }

    public class PidMapping
    {
        public string 身份证号 { get; set; }
        public int PID { get; set; }
    }

    //门诊发药查询
    public class PharmacyDispensing
    {
        public int? 结帐ID { get; set; }
        public int? 处方ID { get; set; }
        public int? 门诊号 { get; set; }
        public string 姓名 { get; set; }
        public string 开方日期 { get; set; }
        public string 开方时间 { get; set; }
        public string 医生工号 { get; set; }
        public string 医生姓名 { get; set; }
        public int? 草药帖数 { get; set; }
        public decimal? 总金额 { get; set; }
        public string 发票状态 { get; set; }
    }

    //发药预览-处方头
    public class DispenseHeaderResult
    {
        public string orgcode { get; set; }
        public string customercode { get; set; }
        public string checkcode { get; set; }
        public int 处方ID { get; set; }
        public string outcfcode { get; set; }
        public string outcfsn { get; set; }
        public string department { get; set; }
        public string jyyq { get; set; }
        public string jynum { get; set; }
        public string zgyq { get; set; }
        public string cftype { get; set; }
        public int? agentnum { get; set; }
        public int? bags { get; set; }
        public int? packagenum { get; set; }
        public string patient { get; set; }
        public string age { get; set; }
        public string jyplan { get; set; }
        public string sex { get; set; }
        public string ispregnancy { get; set; }
        public string telephone { get; set; }
        public string deliveryaddr { get; set; }
        public string client { get; set; }
        public string remark { get; set; }
        public string billdate { get; set; }
        public string doctor { get; set; }
        public string patientcode { get; set; }
        public string customername { get; set; }
        public int? sendmethod { get; set; }
        public string totalprice { get; set; }
        public string diagnosis { get; set; }
        public string medicalno { get; set; }
        public string hcysource { get; set; }
        public string expresstradeno { get; set; }
        public string birthdate { get; set; }
        public string recipelurl { get; set; }
        public string recipelurltype { get; set; }
        public string paymethod { get; set; }
        public string medicalhistory { get; set; }
        public string bringbackflag { get; set; }
        public string isurgent { get; set; }
        public string iscopy { get; set; }
    }

    //发药预览-处方明细
    public class DispenseDetailResult
    {
        public int 处方ID { get; set; }
        public string goodscode { get; set; }
        public string goodsname { get; set; }
        public string dosage { get; set; }
        public string tpyq { get; set; }
        public string goodsspec { get; set; }
        public string goodsunit { get; set; }
        public string manufacturer { get; set; }
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

    //确认发药页面视图模型
    public class DispensingConfirmViewModel
    {
        public DispenseHeaderResult Header { get; set; }
        public List<DispenseDetailResult> Details { get; set; }
    }

    //已发药查询记录
    public class DispensedRecord
    {
        public int 处方ID { get; set; }
        public string 病人姓名 { get; set; }
        public string 处方日期 { get; set; }
        public string outcfcode_original { get; set; }
        public string 发药人工号 { get; set; }
        public string 发药人姓名 { get; set; }
        public DateTime? 发药时间 { get; set; }
        public int? 发药状态 { get; set; }
        public string 发药日期 { get; set; }
        public string 发票状态 { get; set; }
        public string 收货人 { get; set; }
        public string 收货电话 { get; set; }
        public string 收货地址 { get; set; }
    }

    //退药查询参数
    public class DispenseCancelInfo
    {
        public string outcfcode { get; set; }
        public string customercode { get; set; }
        public string checkcode { get; set; }
        public string billdate { get; set; }
    }

    //医技登记
    public class MedicalTechRegistration
    {
        [Key]
        public int 登记ID { get; set; }
        public string 流水号 { get; set; }
        public int? 门诊号 { get; set; }
        public int? 就诊ID { get; set; }
        public string 病人姓名 { get; set; }
        public string 项目名称 { get; set; }
        public int? 总次数 { get; set; }
        public DateTime? 登记时间 { get; set; }
        public string 登记人工号 { get; set; }
        public decimal? 提成金额 { get; set; }
    }

    //医技执行记录
    public class MedicalTechExecution
    {
        [Key]
        public int 执行ID { get; set; }
        public int 登记ID { get; set; }
        public int 本次次数 { get; set; }
        public DateTime? 执行时间 { get; set; }
        public string 执行人工号 { get; set; }
        public string 执行人姓名 { get; set; }
        public string 岗位 { get; set; }
        public string 备注 { get; set; }
        public string delete_flag { get; set; }
    }

    //医技执行人员信息
    public class MedicalTechStaff
    {
        [Key]
        public int 序号 { get; set; }
        public string 工号 { get; set; }
        public string 姓名 { get; set; }
        public string 岗位 { get; set; }
        public string 备注 { get; set; }
    }

    //医技项目默认次数
    public class MedicalTechDefaultCount
    {
        [Key]
        public int 序号 { get; set; }
        public string 项目名称 { get; set; }
        public int 默认总次数 { get; set; }
    }

    //医技项目操作人员提成
    public class MedicalTechCommission
    {
        [Key]
        public int 序号 { get; set; }
        public string 项目名称 { get; set; }
        public string 岗位 { get; set; }
        public decimal? 提成比例 { get; set; }
    }

    //HIS收费明细查询结果（医技登记页面用）
    public class MedicalTechChargeItem
    {
        public int? 结帐ID { get; set; }
        public int? 门诊号 { get; set; }
        public string 姓名 { get; set; }
        public int? 就诊ID { get; set; }
        public int? 处方ID { get; set; }
        public string 日期 { get; set; }
        public string 时间 { get; set; }
        public string 项目名称 { get; set; }
        public decimal? 单价 { get; set; }
        public decimal? 数量 { get; set; }
        public decimal? 金额 { get; set; }
        public decimal? 实收金额 { get; set; }
        public decimal? 提成金额 { get; set; }
        public string 执行人 { get; set; }
        public int? 登记ID { get; set; }
        public int? 总次数 { get; set; }
        public int? 已执行次数 { get; set; }
    }

    //处方结果本地缓存
    public class PrescriptionResultCache
    {
        [Key]
        public int 序号 { get; set; }
        public string outcfcode { get; set; }
        public string billdate { get; set; }
        public string json_data { get; set; }
        public DateTime? 查询时间 { get; set; }
    }

    //收费药品明细（用于核对）
    public class BillingDrugItem
    {
        public int 处方ID { get; set; }
        public string 日期 { get; set; }
        public string 病人姓名 { get; set; }
        public string 项目ID { get; set; }
        public string 药品编码 { get; set; }
        public string 项目名称 { get; set; }
        public decimal? 单价 { get; set; }
        public decimal? 数量 { get; set; }
        public decimal? 金额 { get; set; }
    }

    //发药信息表JSON记录（用于核对）
    public class DispenseJsonRecord
    {
        public int 处方ID { get; set; }
        public string content_json { get; set; }
    }

    //发药明细（JSON解析用）
    public class DispenseDetailItem
    {
        public string goodsname { get; set; }
        public string dosage { get; set; }
        public string goodsid { get; set; }
        public string goodscode { get; set; }
        public string goodsspec { get; set; }
        public string goodsunit { get; set; }
        public decimal? price { get; set; }
    }

    //发药信息（JSON解析结果）
    public class DispenseInfo
    {
        public string Patient { get; set; }
        public int? Agentnum { get; set; }
        public List<DispenseDetailItem> Items { get; set; }
    }

    //药品明细核对结果
    public class DrugDetailCompareItem
    {
        public string 来源 { get; set; }         // "收费" 或 "发药"
        public int 处方ID { get; set; }
        public string 日期 { get; set; }
        public string 病人姓名 { get; set; }
        public string 项目ID { get; set; }
        public string 收费药品编码 { get; set; }          // 代码_药品基本信息表.注册商标
        public string 项目名称 { get; set; }
        public decimal? 单价 { get; set; }
        public decimal? 收费数量 { get; set; }
        public decimal? 金额 { get; set; }              // 收费: 单价×数量
        public string 发药药品编码 { get; set; }          // 发药 sellKpMxVos[].goodscode
        public string 饮片名称 { get; set; }
        public string 饮片用量 { get; set; }
        public int? 剂数 { get; set; }
        public decimal? 发药单价 { get; set; }           // 发药JSON里的价格
        public decimal? 计算数量 { get; set; }           // 用量×剂数
        public decimal? 处方金额 { get; set; }           // 单价×用量×剂数
        public decimal? 折扣 { get; set; }               // 固定折扣率（如 0.6）
        public decimal? 结算金额 { get; set; }           // 发药金额×折扣
        public string 是否一致 { get; set; }
    }

    //药品汇总信息核对（按药品编码汇总，一行一个药品）
    public class DrugSummaryCompareItem
    {
        public string 药品编码 { get; set; }
        public string 药品名称 { get; set; }
        public decimal? 收费数量 { get; set; }           // 收费数量合计
        public decimal? 收费金额 { get; set; }           // 收费金额合计
        public decimal? 发药总用量 { get; set; }          // 发药总用量合计（用量×剂数）
        public decimal? 发药金额 { get; set; }           // 发药金额合计
        public decimal? 结算金额 { get; set; }           // 结算金额合计
        public string 一致 { get; set; }                 // 收费数量==发药总用量 ? 一致 : 不一致
    }

    //医技执行记录查询
    public class ExecutionRecordQuery
    {
        public int 登记ID { get; set; }
        public string 执行时间 { get; set; }
        public string 病人姓名 { get; set; }
        public string 项目名称 { get; set; }
        public int 本次执行次数 { get; set; }
        public decimal? 数量 { get; set; }
        public int? 默认次数 { get; set; }
        public int? 总次数 { get; set; }
        public int 最新本次次数 { get; set; }
        public decimal? 本次执行金额 { get; set; }
        public string 执行人姓名 { get; set; }
        public string 岗位 { get; set; }
        public string 备注 { get; set; }
    }
}