using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 报告数据仓库——封装测试报告和报告模板的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供测试报告的CRUD、
//       状态管理、模板管理等功能。报告按类型分类，支持多种输出格式。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 报告数据仓库
    /// 提供对 test_report（测试报告）和 report_template（报告模板）
    /// 两张表的完整数据访问。报告用于存档和分析充放电测试结果，
    /// 支持 PDF、HTML、CSV 等多种格式输出。
    /// </summary>
    public class ReportRepository
    {
        #region 测试报告管理

        /// <summary>
        /// 插入新的测试报告
        /// 向 test_report 表中创建一条新的报告记录。使用事务保证原子性。
        /// </summary>
        /// <param name="report">测试报告实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertReport(TestReport report)
        {
            try
            {
                const string sql = @"
                    INSERT INTO test_report (
                        report_id, report_type, title, device_id, session_id,
                        generated_by, generated_at, report_format,
                        report_file_path, report_data, status
                    ) VALUES (
                        @report_id, @report_type, @title, @device_id, @session_id,
                        @generated_by, @generated_at, @report_format,
                        @report_file_path, @report_data, @status
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        report_id = report.ReportId,
                        report_type = report.ReportType,
                        title = report.Title,
                        device_id = report.DeviceId,
                        session_id = report.SessionId,
                        generated_by = report.GeneratedBy,
                        generated_at = report.GeneratedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                        report_format = report.ReportFormat ?? "pdf",
                        report_file_path = report.ReportFilePath,
                        report_data = report.ReportData,
                        status = report.Status ?? "draft"
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportRepository.InsertReport] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 根据报告ID获取单个报告
        /// </summary>
        /// <param name="reportId">报告唯一标识</param>
        /// <returns>测试报告对象，未找到返回 null</returns>
        public TestReport GetReportById(string reportId)
        {
            try
            {
                const string sql = @"
                    SELECT report_id, report_type, title, device_id, session_id,
                           generated_by, generated_at, report_format,
                           report_file_path, report_data, status
                    FROM test_report
                    WHERE report_id = @report_id;";

                var connection = DatabaseManager.Instance.Connection;
                var report = connection.QuerySingleOrDefault<TestReport>(sql, new { report_id = reportId });
                return report;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportRepository.GetReportById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取报告列表（支持类型和时间筛选）
        /// 根据报告类型和日期范围查询报告列表。日期参数为字符串格式，
        /// 与 generated_at 字段进行比较。结果按生成时间倒序排列。
        /// </summary>
        /// <param name="reportType">可选，报告类型筛选: charge_test / discharge_test / cycle_test / calibration，null 表示不过滤</param>
        /// <param name="startDate">可选，起始日期字符串（如: "2026-01-01 00:00:00"），null 表示不过滤</param>
        /// <param name="endDate">可选，截止日期字符串，null 表示不过滤</param>
        /// <returns>报告列表</returns>
        public List<TestReport> GetReportList(string reportType, string startDate, string endDate)
        {
            try
            {
                // 动态构建 WHERE 条件
                var conditions = new List<string>();
                var parameters = new DynamicParameters();

                if (!string.IsNullOrEmpty(reportType))
                {
                    conditions.Add("report_type = @report_type");
                    parameters.Add("report_type", reportType);
                }
                if (!string.IsNullOrEmpty(startDate))
                {
                    conditions.Add("generated_at >= @start_date");
                    parameters.Add("start_date", startDate);
                }
                if (!string.IsNullOrEmpty(endDate))
                {
                    conditions.Add("generated_at <= @end_date");
                    parameters.Add("end_date", endDate);
                }

                string whereClause = conditions.Count > 0
                    ? "WHERE " + string.Join(" AND ", conditions)
                    : "";

                string sql = $@"
                    SELECT report_id, report_type, title, device_id, session_id,
                           generated_by, generated_at, report_format,
                           report_file_path, report_data, status
                    FROM test_report
                    {whereClause}
                    ORDER BY generated_at DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var reports = connection.Query<TestReport>(sql, parameters).AsList();
                return reports;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportRepository.GetReportList] 错误: {ex.Message}");
                return new List<TestReport>();
            }
        }

        /// <summary>
        /// 更新报告状态
        /// 修改报告的状态: draft（草稿）/ published（已发布）/ archived（已归档）。
        /// </summary>
        /// <param name="reportId">报告ID</param>
        /// <param name="status">新状态</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateReportStatus(string reportId, string status)
        {
            try
            {
                const string sql = @"
                    UPDATE test_report SET
                        status = @status
                    WHERE report_id = @report_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    report_id = reportId,
                    status = status
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportRepository.UpdateReportStatus] 错误: {ex.Message}");
                return -1;
            }
        }

        #endregion

        #region 报告模板管理

        /// <summary>
        /// 获取报告模板列表
        /// 从 report_template 表查询模板。可指定报告类型筛选，null 则返回全部。
        /// 结果按更新时间倒序排列。
        /// </summary>
        /// <param name="reportType">可选，按报告类型筛选，null 表示不过滤</param>
        /// <returns>报告模板列表</returns>
        public List<ReportTemplate> GetReportTemplates(string reportType = null)
        {
            try
            {
                string sql;
                object parameters = null;

                if (string.IsNullOrEmpty(reportType))
                {
                    sql = @"
                        SELECT template_id, template_name, report_type,
                               template_content, is_default, created_by, updated_at
                        FROM report_template
                        ORDER BY updated_at DESC;";
                }
                else
                {
                    sql = @"
                        SELECT template_id, template_name, report_type,
                               template_content, is_default, created_by, updated_at
                        FROM report_template
                        WHERE report_type = @report_type
                        ORDER BY updated_at DESC;";
                    parameters = new { report_type = reportType };
                }

                var connection = DatabaseManager.Instance.Connection;
                var templates = connection.Query<ReportTemplate>(sql, parameters).AsList();
                return templates;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportRepository.GetReportTemplates] 错误: {ex.Message}");
                return new List<ReportTemplate>();
            }
        }

        /// <summary>
        /// 插入新的报告模板
        /// 向 report_template 表添加一个模板定义。
        /// </summary>
        /// <param name="template">报告模板实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertReportTemplate(ReportTemplate template)
        {
            try
            {
                const string sql = @"
                    INSERT INTO report_template (
                        template_id, template_name, report_type,
                        template_content, is_default, created_by, updated_at
                    ) VALUES (
                        @template_id, @template_name, @report_type,
                        @template_content, @is_default, @created_by, @updated_at
                    );";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    template_id = template.TemplateId,
                    template_name = template.TemplateName,
                    report_type = template.ReportType,
                    template_content = template.TemplateContent,
                    is_default = template.IsDefault,
                    created_by = template.CreatedBy,
                    updated_at = template.UpdatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportRepository.InsertReportTemplate] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新报告模板
        /// 根据模板ID更新模板内容、默认标记和更新时间。
        /// </summary>
        /// <param name="template">报告模板实体（需包含有效的 TemplateId）</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateReportTemplate(ReportTemplate template)
        {
            try
            {
                const string sql = @"
                    UPDATE report_template SET
                        template_name = @template_name,
                        report_type = @report_type,
                        template_content = @template_content,
                        is_default = @is_default,
                        updated_at = @updated_at
                    WHERE template_id = @template_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    template_id = template.TemplateId,
                    template_name = template.TemplateName,
                    report_type = template.ReportType,
                    template_content = template.TemplateContent,
                    is_default = template.IsDefault,
                    updated_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReportRepository.UpdateReportTemplate] 错误: {ex.Message}");
                return -1;
            }
        }

        #endregion
    }
}
