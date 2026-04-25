using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 报告生成服务实现类
// 说明: 实现充放电测试报告的HTML格式生成、模板替换和定时调度
//       支持充电测试、放电测试、循环测试、化成测试等多种报告类型
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 报告生成服务实现类
    /// 负责各类充放电测试报告的生成和管理：
    ///   1. HTML报告生成：基于预设模板生成包含表格和图表的HTML报告
    ///   2. 模板引擎：支持占位符替换的简单模板渲染
    ///   3. 报告类型：
    ///      - charge_test: 充电测试报告（含恒流/恒压充电曲线、容量统计）
    ///      - discharge_test: 放电测试报告（含放电曲线、能量效率）
    ///      - cycle_test: 循环测试报告（含多周期对比、寿命衰减分析）
    ///      - formation_test: 化成测试报告（含首次充放电效率、容量分析）
    ///      - calibration_report: 校准报告（含校准曲线、误差分析）
    ///      - fault_analysis: 故障分析报告（含故障波形、根因分析）
    ///      - daily_summary: 日报（含当日运行摘要、设备状态）
    ///      - monthly_summary: 月报（含月度统计、趋势分析）
    /// </summary>
    public class ReportService : IReportService
    {
        #region -- 字段定义 --

        /// <summary>配置服务引用</summary>
        private readonly IConfigService _configService;

        /// <summary>应用程序配置</summary>
        private readonly AppConfig _appConfig;

        /// <summary>报告模板缓存字典（Key: 报告类型, Value: 模板内容）</summary>
        private readonly Dictionary<string, string> _templateCache = new Dictionary<string, string>();

        /// <summary>调度任务字典（Key: 调度ID, Value: 调度信息）</summary>
        private readonly Dictionary<string, ScheduledReport> _scheduledReports = new Dictionary<string, ScheduledReport>();

        #endregion

        /// <summary>
        /// 构造报告生成服务实例
        /// </summary>
        /// <param name="configService">配置服务</param>
        /// <param name="appConfig">应用程序配置</param>
        public ReportService(IConfigService configService, AppConfig appConfig)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
        }

        #region -- 报告生成 --

        /// <summary>
        /// 生成报告
        /// 根据报告类型加载对应模板，填充数据并输出HTML文件
        /// </summary>
        /// <param name="reportType">报告类型</param>
        /// <param name="sessionId">会话ID（可选）</param>
        /// <param name="deviceId">设备ID（可选）</param>
        /// <param name="parameters">生成参数</param>
        /// <returns>报告ID</returns>
        public string GenerateReport(string reportType, string sessionId = null, string deviceId = null, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(reportType))
                throw new ArgumentException("报告类型不能为空", nameof(reportType));

            try
            {
                string reportId = CryptoHelper.GenerateUuid();

                // ---- 第一步：加载报告模板 ----
                string template = GetReportTemplate(reportType);
                if (string.IsNullOrEmpty(template))
                {
                    template = GenerateDefaultTemplate(reportType);
                }

                // ---- 第二步：准备报告数据 ----
                var reportData = new Dictionary<string, object>
                {
                    ["reportId"] = reportId,
                    ["reportType"] = reportType,
                    ["sessionId"] = sessionId,
                    ["deviceId"] = deviceId,
                    ["generatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["title"] = GetReportTitle(reportType)
                };

                // 合并自定义参数
                if (parameters != null)
                {
                    foreach (var kvp in parameters)
                        reportData[kvp.Key] = kvp.Value;
                }

                // ---- 第三步：渲染报告内容 ----
                string reportContent = RenderTemplate(template, reportData);

                // ---- 第四步：保存报告文件 ----
                string outputPath = _appConfig.ReportConfig?.OutputPath ?? SystemConstants.DefaultReportDir;
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);

                string fileName = $"{reportType}_{DateTime.Now:yyyyMMdd_HHmmss}_{reportId.Substring(0, 8)}.html";
                string filePath = Path.Combine(outputPath, fileName);
                File.WriteAllText(filePath, reportContent, Encoding.UTF8);

                // ---- 第五步：创建报告记录 ----
                var report = new TestReport
                {
                    ReportId = reportId,
                    ReportType = reportType,
                    Title = GetReportTitle(reportType),
                    DeviceId = deviceId,
                    SessionId = sessionId,
                    GeneratedBy = "SYSTEM",
                    GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ReportFormat = "html",
                    ReportFilePath = filePath,
                    Status = "published"
                };

                // TODO: 注入ReportRepository并调用 InsertAsync(report)
                System.Diagnostics.Debug.WriteLine($"[ReportService] 报告生成完成: {reportId} -> {filePath}");
                return reportId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportService] 报告生成失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region -- 报告查询 --

        /// <summary>
        /// 获取报告列表（支持按类型和时间筛选）
        /// </summary>
        /// <param name="reportType">报告类型过滤</param>
        /// <param name="startDate">起始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>报告列表</returns>
        public List<TestReport> GetReportList(string reportType = null, string startDate = null, string endDate = null)
        {
            // TODO: 注入ReportRepository并按条件查询
            System.Diagnostics.Debug.WriteLine(
                $"[ReportService] 查询报告列表: 类型={reportType ?? "全部"}, {startDate}~{endDate}");
            return new List<TestReport>();
        }

        /// <summary>
        /// 下载报告
        /// 返回报告文件的完整路径
        /// </summary>
        /// <param name="reportId">报告ID</param>
        /// <returns>报告文件路径</returns>
        public string DownloadReport(string reportId)
        {
            if (string.IsNullOrEmpty(reportId)) return null;

            // TODO: 从数据库查询报告记录，返回 ReportFilePath
            System.Diagnostics.Debug.WriteLine($"[ReportService] 下载报告: {reportId}");
            return null;
        }

        #endregion

        #region -- 报告调度 --

        /// <summary>
        /// 定时调度报告生成
        /// 注册Cron定时任务，到指定时间自动生成报告
        /// </summary>
        /// <param name="reportType">报告类型</param>
        /// <param name="cronExpression">Cron表达式</param>
        /// <param name="parameters">生成参数</param>
        /// <returns>调度任务ID</returns>
        public string ScheduleReport(string reportType, string cronExpression, Dictionary<string, object> parameters)
        {
            string scheduleId = CryptoHelper.GenerateUuid();

            var scheduled = new ScheduledReport
            {
                ScheduleId = scheduleId,
                ReportType = reportType,
                CronExpression = cronExpression,
                Parameters = parameters,
                CreatedAt = DateTime.Now
            };

            lock (_scheduledReports)
            {
                _scheduledReports[scheduleId] = scheduled;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[ReportService] 报告调度已注册: {scheduleId} ({reportType}, Cron={cronExpression})");
            return scheduleId;
        }

        #endregion

        #region -- 模板引擎 --

        /// <summary>
        /// 获取指定类型报告的HTML模板
        /// 优先从配置服务加载模板，没有则使用内置默认模板
        /// </summary>
        private string GetReportTemplate(string reportType)
        {
            lock (_templateCache)
            {
                if (_templateCache.TryGetValue(reportType, out var cached))
                    return cached;
            }

            // 尝试从配置服务加载模板
            string template = _configService?.GetConfigValue<string>("report_templates", reportType);
            if (!string.IsNullOrEmpty(template))
            {
                lock (_templateCache) { _templateCache[reportType] = template; }
                return template;
            }

            return null;
        }

        /// <summary>
        /// 生成默认HTML报告模板
        /// </summary>
        private string GenerateDefaultTemplate(string reportType)
        {
            string title = GetReportTitle(reportType);

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset='UTF-8'>");
            sb.AppendLine($"  <title>{{{{title}}}}</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: 'Microsoft YaHei', sans-serif; margin: 40px; }");
            sb.AppendLine("    h1 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; }");
            sb.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
            sb.AppendLine("    th, td { border: 1px solid #ddd; padding: 8px; text-align: center; }");
            sb.AppendLine("    th { background-color: #3498db; color: white; }");
            sb.AppendLine("    .footer { margin-top: 40px; color: #7f8c8d; font-size: 12px; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine($"  <h1>{{{{title}}}}</h1>");
            sb.AppendLine("  <div class='meta'>");
            sb.AppendLine($"    <p>生成时间: {{{{generatedAt}}}}</p>");
            sb.AppendLine($"    <p>设备ID: {{{{deviceId}}}}</p>");
            sb.AppendLine($"    <p>会话ID: {{{{sessionId}}}}</p>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class='content'>");
            sb.AppendLine("    <h2>测试概要</h2>");
            sb.AppendLine("    <table>");
            sb.AppendLine("      <tr><th>参数</th><th>数值</th></tr>");
            sb.AppendLine("      <tr><td>总能量</td><td>{{totalEnergy}} kWh</td></tr>");
            sb.AppendLine("      <tr><td>最高电压</td><td>{{maxVoltage}} V</td></tr>");
            sb.AppendLine("      <tr><td>最低电压</td><td>{{minVoltage}} V</td></tr>");
            sb.AppendLine("      <tr><td>最大电流</td><td>{{maxCurrent}} A</td></tr>");
            sb.AppendLine("      <tr><td>最高温度</td><td>{{maxTemperature}} °C</td></tr>");
            sb.AppendLine("    </table>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class='footer'>");
            sb.AppendLine("    <p>本报告由 MW级充放电上位机系统 自动生成</p>");
            sb.AppendLine($"    <p>报告ID: {{{{reportId}}}}</p>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// 渲染模板：将占位符 {{key}} 替换为实际数据
        /// </summary>
        /// <param name="template">模板内容</param>
        /// <param name="data">替换数据字典</param>
        /// <returns>渲染后的内容</returns>
        private string RenderTemplate(string template, Dictionary<string, object> data)
        {
            if (string.IsNullOrEmpty(template) || data == null)
                return template;

            string result = template;
            foreach (var kvp in data)
            {
                string placeholder = $"{{{{{kvp.Key}}}}}";
                string value = kvp.Value?.ToString() ?? "";
                result = result.Replace(placeholder, value);
            }

            // 清除未替换的占位符
            while (result.Contains("{{") && result.Contains("}}"))
            {
                int start = result.IndexOf("{{");
                int end = result.IndexOf("}}", start);
                if (start < 0 || end < 0) break;
                result = result.Remove(start, end - start + 2);
            }

            return result;
        }

        /// <summary>
        /// 获取报告类型的中文标题
        /// </summary>
        private string GetReportTitle(string reportType)
        {
            return reportType?.ToLower() switch
            {
                "charge_test" => "充电测试报告",
                "discharge_test" => "放电测试报告",
                "cycle_test" => "循环测试报告",
                "formation_test" => "化成测试报告",
                "calibration_report" => "校准报告",
                "fault_analysis" => "故障分析报告",
                "daily_summary" => "日报",
                "monthly_summary" => "月报",
                _ => "测试报告"
            };
        }

        #endregion
    }

    /// <summary>
    /// 定时报告调度信息
    /// </summary>
    internal class ScheduledReport
    {
        public string ScheduleId { get; set; }
        public string ReportType { get; set; }
        public string CronExpression { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
