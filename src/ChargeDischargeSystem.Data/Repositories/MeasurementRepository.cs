using System;
using System.Collections.Generic;
using System.Text;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 测量数据仓库——封装时序测量数据的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供测量数据的批量插入、
//       时序查询（含聚合）、旧数据清理及记录数统计等功能。
//       测量数据是系统中数据量最大、写入最频繁的表，需注重性能优化。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 测量数据仓库
    /// 提供对 measurement_data（时序测量数据）表的完整数据访问。
    /// 该表存储充放电过程中设备上报的所有实时测量数据，
    /// 是系统中数据量最大、写入频率最高的核心表。
    /// </summary>
    public class MeasurementRepository
    {
        /// <summary>
        /// 插入单条测量数据
        /// 向 measurement_data 表插入一条测量记录。
        /// </summary>
        /// <param name="data">测量数据实体</param>
        /// <returns>新插入记录的自增ID（record_id），失败时返回 -1</returns>
        public long InsertMeasurement(MeasurementData data)
        {
            try
            {
                const string sql = @"
                    INSERT INTO measurement_data (
                        timestamp, device_id, session_id, parameter_name, value, unit, quality
                    ) VALUES (
                        @timestamp, @device_id, @session_id, @parameter_name, @value, @unit, @quality
                    );
                    SELECT last_insert_rowid();";

                var connection = DatabaseManager.Instance.Connection;
                long recordId = connection.ExecuteScalar<long>(sql, new
                {
                    timestamp = data.Timestamp,
                    device_id = data.DeviceId,
                    session_id = data.SessionId,
                    parameter_name = data.ParameterName,
                    value = data.Value,
                    unit = data.Unit,
                    quality = data.Quality ?? "GOOD"
                });
                return recordId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeasurementRepository.InsertMeasurement] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 批量插入测量数据（使用事务）
        /// 在单个事务中向 measurement_data 表批量插入多条测量记录。
        /// 批量插入相比逐条插入可大幅减少事务开销和索引更新次数，
        /// 推荐在高频数据采集场景下使用。
        /// </summary>
        /// <param name="dataList">测量数据列表</param>
        /// <returns>成功插入的总行数，失败时返回 -1</returns>
        public int InsertMeasurementsBatch(List<MeasurementData> dataList)
        {
            if (dataList == null || dataList.Count == 0) return 0;

            try
            {
                const string sql = @"
                    INSERT INTO measurement_data (
                        timestamp, device_id, session_id, parameter_name, value, unit, quality
                    ) VALUES (
                        @timestamp, @device_id, @session_id, @parameter_name, @value, @unit, @quality
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int totalRows = 0;
                    foreach (var data in dataList)
                    {
                        totalRows += connection.Execute(sql, new
                        {
                            timestamp = data.Timestamp,
                            device_id = data.DeviceId,
                            session_id = data.SessionId,
                            parameter_name = data.ParameterName,
                            value = data.Value,
                            unit = data.Unit,
                            quality = data.Quality ?? "GOOD"
                        }, transaction);
                    }
                    transaction.Commit();
                    return totalRows;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeasurementRepository.InsertMeasurementsBatch] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 查询测量数据（支持聚合模式）
        /// 根据设备ID、参数名称和时间范围查询测量数据。
        /// 支持多种聚合方式：none（原始数据）、avg（平均值）、min（最小值）、
        /// max（最大值）、sum（求和），可指定聚合时间间隔。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="parameterName">参数名称: voltage / current / power / temperature / soc</param>
        /// <param name="startTime">查询起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">查询截止时间（Unix毫秒时间戳）</param>
        /// <param name="aggregation">聚合方式: none / avg / min / max / sum，默认 none</param>
        /// <param name="aggregationIntervalS">聚合时间间隔（秒），仅聚合模式下生效，默认 60</param>
        /// <returns>测量数据列表，发生异常返回空列表</returns>
        public List<MeasurementData> QueryMeasurements(string deviceId, string parameterName,
            long startTime, long endTime, string aggregation = "none", int aggregationIntervalS = 60)
        {
            try
            {
                var connection = DatabaseManager.Instance.Connection;

                // 确定SQL语句：根据聚合方式生成不同的查询
                if (string.IsNullOrEmpty(aggregation) || aggregation == "none")
                {
                    // ---- 原始数据查询（不聚合） ----
                    const string sql = @"
                        SELECT record_id, timestamp, device_id, session_id,
                               parameter_name, value, unit, quality
                        FROM measurement_data
                        WHERE device_id = @device_id
                          AND parameter_name = @parameter_name
                          AND timestamp >= @start_time
                          AND timestamp <= @end_time
                        ORDER BY timestamp ASC;";

                    var results = connection.Query<MeasurementData>(sql, new
                    {
                        device_id = deviceId,
                        parameter_name = parameterName,
                        start_time = startTime,
                        end_time = endTime
                    }).AsList();
                    return results;
                }
                else
                {
                    // ---- 聚合查询：按时间间隔分组聚合 ----
                    // 将时间戳除以间隔毫秒数，取整后分组（时间桶聚合）
                    long intervalMs = (long)aggregationIntervalS * 1000L;

                    string aggregateFunc;
                    switch (aggregation.ToLowerInvariant())
                    {
                        case "avg":
                            aggregateFunc = "ROUND(AVG(value), 4)";
                            break;
                        case "min":
                            aggregateFunc = "MIN(value)";
                            break;
                        case "max":
                            aggregateFunc = "MAX(value)";
                            break;
                        case "sum":
                            aggregateFunc = "ROUND(SUM(value), 4)";
                            break;
                        default:
                            aggregateFunc = "ROUND(AVG(value), 4)";
                            break;
                    }

                    string sql = $@"
                        SELECT 
                            (timestamp / @interval_ms) * @interval_ms AS timestamp,
                            device_id,
                            parameter_name,
                            {aggregateFunc} AS value,
                            unit,
                            'GOOD' AS quality
                        FROM measurement_data
                        WHERE device_id = @device_id
                          AND parameter_name = @parameter_name
                          AND timestamp >= @start_time
                          AND timestamp <= @end_time
                        GROUP BY (timestamp / @interval_ms), device_id, parameter_name, unit
                        ORDER BY timestamp ASC;";

                    var results = connection.Query<MeasurementData>(sql, new
                    {
                        device_id = deviceId,
                        parameter_name = parameterName,
                        start_time = startTime,
                        end_time = endTime,
                        interval_ms = intervalMs
                    }).AsList();
                    return results;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeasurementRepository.QueryMeasurements] 错误: {ex.Message}");
                return new List<MeasurementData>();
            }
        }

        /// <summary>
        /// 删除指定时间之前的旧测量数据
        /// 用于定期清理过期数据，防止数据库文件无限增长。
        /// 此操作可能影响大量行，建议在系统负载较低的时段执行。
        /// </summary>
        /// <param name="beforeTime">删除此时间戳之前的所有数据（Unix毫秒时间戳）</param>
        /// <returns>删除的行数，失败时返回 -1</returns>
        public int DeleteOldData(long beforeTime)
        {
            try
            {
                const string sql = @"
                    DELETE FROM measurement_data
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
                System.Diagnostics.Debug.WriteLine($"[MeasurementRepository.DeleteOldData] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 获取测量数据表的总记录数
        /// 用于监控数据增长情况，辅助制定数据清理策略。
        /// </summary>
        /// <returns>总记录数，失败时返回 -1</returns>
        public long GetRecordCount()
        {
            try
            {
                const string sql = "SELECT COUNT(*) FROM measurement_data;";

                var connection = DatabaseManager.Instance.Connection;
                long count = connection.ExecuteScalar<long>(sql);
                return count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MeasurementRepository.GetRecordCount] 错误: {ex.Message}");
                return -1;
            }
        }
    }
}
