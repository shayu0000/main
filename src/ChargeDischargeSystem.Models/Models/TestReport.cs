// ============================================================
// 文件名: TestReport.cs
// 用途: 测试报告和报告模板实体类，对应 test_report 和 report_template 表
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 测试报告实体类，存储充放电测试生成的各类报告信息
    /// </summary>
    public class TestReport
    {
        /// <summary>
        /// 获取或设置报告 ID，主键
        /// </summary>
        public string ReportId { get; set; }

        /// <summary>
        /// 获取或设置报告类型: charge_test(充电测试)/discharge_test(放电测试)/cycle_test(循环测试)/calibration(校准报告)
        /// </summary>
        public string ReportType { get; set; }

        /// <summary>
        /// 获取或设置报告标题
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置关联设备 ID，可为空
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置关联充放电会话 ID，可为空
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 获取或设置报告生成人用户 ID
        /// </summary>
        public string GeneratedBy { get; set; }

        /// <summary>
        /// 获取或设置报告生成时间
        /// </summary>
        public string GeneratedAt { get; set; }

        /// <summary>
        /// 获取或设置报告输出格式: pdf / html / csv，默认为 pdf
        /// </summary>
        public string ReportFormat { get; set; } = "pdf";

        /// <summary>
        /// 获取或设置报告文件存储路径
        /// </summary>
        public string ReportFilePath { get; set; }

        /// <summary>
        /// 获取或设置报告数据内容，JSON 格式
        /// </summary>
        public string ReportData { get; set; }

        /// <summary>
        /// 获取或设置报告状态: draft(草稿)/published(已发布)/archived(已归档)，默认为 draft
        /// </summary>
        public string Status { get; set; } = "draft";
    }

    /// <summary>
    /// 报告模板实体类，存储报告生成模板的定义信息
    /// </summary>
    public class ReportTemplate
    {
        /// <summary>
        /// 获取或设置模板 ID，主键
        /// </summary>
        public string TemplateId { get; set; }

        /// <summary>
        /// 获取或设置模板名称
        /// </summary>
        public string TemplateName { get; set; }

        /// <summary>
        /// 获取或设置适用的报告类型
        /// </summary>
        public string ReportType { get; set; }

        /// <summary>
        /// 获取或设置模板内容，Jinja2 模板语法
        /// </summary>
        public string TemplateContent { get; set; }

        /// <summary>
        /// 获取或设置是否为默认模板: 1=默认, 0=非默认
        /// </summary>
        public int IsDefault { get; set; }

        /// <summary>
        /// 获取或设置模板创建人用户 ID
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// 获取或设置模板最后更新时间
        /// </summary>
        public string UpdatedAt { get; set; }
    }
}
