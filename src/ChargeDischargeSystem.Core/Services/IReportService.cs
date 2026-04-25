// ============================================================
// 文件名: IReportService.cs
// 用途: 报告生成服务接口，定义充放电测试报告的生成、查询和调度功能
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 报告生成服务接口
    /// 负责生成各类充放电测试报告和管理报告模板：
    ///   1. 支持报告类型：充电测试/放电测试/循环测试/化成测试/校准报告/故障分析/日报/月报
    ///   2. HTML格式报告生成（包含表格和图表数据嵌入）
    ///   3. 报告模板引擎（基于模板内容替换）
    ///   4. 定时报告调度
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// 生成报告，根据报告类型和相关参数生成测试报告
        /// </summary>
        /// <param name="reportType">报告类型（charge_test / discharge_test / cycle_test / formation_test / calibration_report / fault_analysis / daily_summary / monthly_summary）</param>
        /// <param name="sessionId">关联的充放电会话ID（可选）</param>
        /// <param name="deviceId">关联的设备ID（可选）</param>
        /// <param name="parameters">报告生成参数（如自定义时间范围、图表选项等）</param>
        /// <returns>生成的报告ID，失败返回null</returns>
        string GenerateReport(string reportType, string sessionId = null, string deviceId = null, Dictionary<string, object> parameters = null);

        /// <summary>
        /// 获取报告列表（支持按类型和时间筛选）
        /// </summary>
        /// <param name="reportType">报告类型过滤，为null则不过滤</param>
        /// <param name="startDate">起始日期（yyyy-MM-dd格式），为null则不过滤</param>
        /// <param name="endDate">结束日期（yyyy-MM-dd格式），为null则不过滤</param>
        /// <returns>报告列表</returns>
        List<ChargeDischargeSystem.Core.Models.TestReport> GetReportList(string reportType = null, string startDate = null, string endDate = null);

        /// <summary>
        /// 下载报告，返回报告文件的完整路径
        /// </summary>
        /// <param name="reportId">报告ID</param>
        /// <returns>报告文件路径，不存在返回null</returns>
        string DownloadReport(string reportId);

        /// <summary>
        /// 定时调度报告生成，使用Cron表达式定时自动生成报告
        /// </summary>
        /// <param name="reportType">报告类型</param>
        /// <param name="cronExpression">Cron调度表达式（如 "0 8 * * *" 表示每天8点）</param>
        /// <param name="parameters">报告生成参数</param>
        /// <returns>调度任务ID</returns>
        string ScheduleReport(string reportType, string cronExpression, Dictionary<string, object> parameters);
    }
}
