using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 系统日志数据仓库——封装系统日志的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供日志的写入、按级别/模块
//       查询、旧日志清理以及按级别统计等功能。系统日志用于运维监控、
//       问题排查和审计追溯。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 系统日志数据仓库
    /// 提供对 system_log（系统日志）表的完整数据访问。
    /// 系统日志记录了应用程序运行过程中的所有重要事件，
    /// 按日志级别分类（DEBUG/INFO/WARNING/ERROR/CRITICAL），
    /// 支持按模块、级别和时间进行检索和分析。
    /// </summary>
    public class LogRepository
    {
        /// <summary>
        /// 插入系统日志
        /// 向 system_log 表写入一条新的日志记录。
        /// </summary>
        /// <param name="log">系统日志实体</param>
        /// <returns>新插入日志的自增ID（log_id），失败时返回 -1</returns>
        public long InsertLog(SystemLog log)
        {
            try
            {
                const string sql = @"
                    INSERT INTO system_log (
                        timestamp, log_level, module_name, message, details, user_id, device_id
                    ) VALUES (
                        @timestamp, @log_level, @module_name, @message, @details, @user_id, @device_id
                    );
                    SELECT last_insert_rowid();";

                var connection = DatabaseManager.Instance.Connection;
                long logId = connection.ExecuteScalar<long>(sql, new
                {
                    timestamp = log.Timestamp,
                    log_level = log.LogLevel,
                    module_name = log.ModuleName,
                    message = log.Message,
                    details = log.Details,
                    user_id = log.UserId,
                    device_id = log.DeviceId
                });
                return logId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogRepository.InsertLog] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 查询系统日志
        /// 支持按日志级别和模块名称过滤，可限制返回条数。
        /// 结果按时间戳倒序排列（最新日志在前）。
        /// </summary>
        /// <param name="logLevel">可选，日志级别筛选: DEBUG / INFO / WARNING / ERROR / CRITICAL，null 表示不过滤</param>
        /// <param name="moduleName">可选，模块名称筛选，null 表示不过滤</param>
        /// <param name="limit">返回最大条数，默认 1000</param>
        /// <returns>系统日志列表</returns>
        public List<SystemLog> GetLogs(string logLevel = null, string moduleName = null, int limit = 1000)
        {
            try
            {
                // 动态构建 WHERE 条件
                var conditions = new List<string>();
                var parameters = new DynamicParameters();

                if (!string.IsNullOrEmpty(logLevel))
                {
                    conditions.Add("log_level = @log_level");
                    parameters.Add("log_level", logLevel);
                }
                if (!string.IsNullOrEmpty(moduleName))
                {
                    conditions.Add("module_name = @module_name");
                    parameters.Add("module_name", moduleName);
                }

                string whereClause = conditions.Count > 0
                    ? "WHERE " + string.Join(" AND ", conditions)
                    : "";

                string sql = $@"
                    SELECT log_id, timestamp, log_level, module_name,
                           message, details, user_id, device_id
                    FROM system_log
                    {whereClause}
                    ORDER BY timestamp DESC
                    LIMIT @limit;";

                parameters.Add("limit", limit);

                var connection = DatabaseManager.Instance.Connection;
                var logs = connection.Query<SystemLog>(sql, parameters).AsList();
                return logs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogRepository.GetLogs] 错误: {ex.Message}");
                return new List<SystemLog>();
            }
        }

        /// <summary>
        /// 删除指定时间之前的旧日志
        /// 用于定期清理过期日志数据，防止日志表无限增长。
        /// 建议根据日志保留策略定期执行（如每周清理 90 天前的日志）。
        /// </summary>
        /// <param name="beforeTime">删除此时间戳之前的所有日志（Unix毫秒时间戳）</param>
        /// <returns>删除的行数，失败时返回 -1</returns>
        public int DeleteOldLogs(long beforeTime)
        {
            try
            {
                const string sql = @"
                    DELETE FROM system_log
                    WHERE timestamp < @before_time;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int deletedRows = connection.Execute(sql, new { before_time = beforeTime }, transaction);
                    transaction.Commit();
                    return deletedRows;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogRepository.DeleteOldLogs] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 按日志级别统计日志数量
        /// 查询指定日志级别的记录总数，通常用于监控面板展示。
        /// </summary>
        /// <param name="logLevel">日志级别: DEBUG / INFO / WARNING / ERROR / CRITICAL</param>
        /// <returns>该级别的日志总数，失败时返回 -1</returns>
        public long GetLogCountByLevel(string logLevel)
        {
            try
            {
                const string sql = @"
                    SELECT COUNT(*)
                    FROM system_log
                    WHERE log_level = @log_level;";

                var connection = DatabaseManager.Instance.Connection;
                long count = connection.ExecuteScalar<long>(sql, new { log_level = logLevel });
                return count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogRepository.GetLogCountByLevel] 错误: {ex.Message}");
                return -1;
            }
        }
    }
}
