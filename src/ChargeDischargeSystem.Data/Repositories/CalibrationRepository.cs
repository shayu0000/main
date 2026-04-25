using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 校准数据仓库——封装校准记录和校准数据点的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供校准记录的CRUD、
//       校准数据点批量插入、校准历史查询以及最新有效校准检索。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 校准数据仓库
    /// 提供对 calibration_record（校准记录）和 calibration_point（校准数据点）
    /// 两张表的完整数据访问。
    /// </summary>
    public class CalibrationRepository
    {
        /// <summary>
        /// 插入校准记录
        /// 向 calibration_record 表中插入一条新的校准记录。
        /// 使用事务确保写入原子性。
        /// </summary>
        /// <param name="record">校准记录实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertCalibrationRecord(CalibrationRecord record)
        {
            try
            {
                const string sql = @"
                    INSERT INTO calibration_record (
                        calibration_id, device_id, calibration_type, calibration_status,
                        started_at, completed_at, performed_by, approved_by,
                        is_valid, valid_until, notes
                    ) VALUES (
                        @calibration_id, @device_id, @calibration_type, @calibration_status,
                        @started_at, @completed_at, @performed_by, @approved_by,
                        @is_valid, @valid_until, @notes
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        calibration_id = record.CalibrationId,
                        device_id = record.DeviceId,
                        calibration_type = record.CalibrationType,
                        calibration_status = record.CalibrationStatus,
                        started_at = record.StartedAt,
                        completed_at = record.CompletedAt,
                        performed_by = record.PerformedBy,
                        approved_by = record.ApprovedBy,
                        is_valid = record.IsValid,
                        valid_until = record.ValidUntil,
                        notes = record.Notes
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationRepository.InsertCalibrationRecord] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 批量插入校准数据点
        /// 在单个事务中向 calibration_point 表批量插入多个校准点数据。
        /// 确保所有校准点要么全部写入成功，要么全部回滚。
        /// </summary>
        /// <param name="points">校准点列表</param>
        /// <returns>成功插入的总行数，失败时返回 -1</returns>
        public int InsertCalibrationPoints(List<CalibrationPoint> points)
        {
            if (points == null || points.Count == 0) return 0;

            try
            {
                const string sql = @"
                    INSERT INTO calibration_point (
                        calibration_id, parameter_name, point_index,
                        reference_value, measured_value, corrected_value,
                        deviation_percent, is_pass
                    ) VALUES (
                        @calibration_id, @parameter_name, @point_index,
                        @reference_value, @measured_value, @corrected_value,
                        @deviation_percent, @is_pass
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int totalRows = 0;
                    foreach (var point in points)
                    {
                        totalRows += connection.Execute(sql, new
                        {
                            calibration_id = point.CalibrationId,
                            parameter_name = point.ParameterName,
                            point_index = point.PointIndex,
                            reference_value = point.ReferenceValue,
                            measured_value = point.MeasuredValue,
                            corrected_value = point.CorrectedValue,
                            deviation_percent = point.DeviationPercent,
                            is_pass = point.IsPass
                        }, transaction);
                    }
                    transaction.Commit();
                    return totalRows;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationRepository.InsertCalibrationPoints] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 查询校准历史记录
        /// 根据设备ID和时间范围查询校准记录，同时加载每条记录关联的校准数据点。
        /// 结果按开始时间倒序排列。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="startTime">查询起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">查询截止时间（Unix毫秒时间戳）</param>
        /// <returns>校准记录列表（包含关联的校准点）</returns>
        public List<CalibrationRecord> GetCalibrationHistory(string deviceId, long startTime, long endTime)
        {
            try
            {
                const string sql = @"
                    SELECT calibration_id, device_id, calibration_type, calibration_status,
                           started_at, completed_at, performed_by, approved_by,
                           is_valid, valid_until, notes
                    FROM calibration_record
                    WHERE device_id = @device_id
                      AND started_at >= @start_time
                      AND started_at <= @end_time
                    ORDER BY started_at DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var records = connection.Query<CalibrationRecord>(sql, new
                {
                    device_id = deviceId,
                    start_time = startTime,
                    end_time = endTime
                }).AsList();

                // 为每条校准记录加载关联的校准点数据
                foreach (var record in records)
                {
                    record.CalibrationPoints = GetCalibrationPointsByCalibrationId(record.CalibrationId);
                }

                return records;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationRepository.GetCalibrationHistory] 错误: {ex.Message}");
                return new List<CalibrationRecord>();
            }
        }

        /// <summary>
        /// 根据校准ID获取单条校准记录及其关联的校准点
        /// </summary>
        /// <param name="calibrationId">校准记录ID</param>
        /// <returns>校准记录对象（含校准点集合），未找到返回 null</returns>
        public CalibrationRecord GetCalibrationById(string calibrationId)
        {
            try
            {
                const string sql = @"
                    SELECT calibration_id, device_id, calibration_type, calibration_status,
                           started_at, completed_at, performed_by, approved_by,
                           is_valid, valid_until, notes
                    FROM calibration_record
                    WHERE calibration_id = @calibration_id;";

                var connection = DatabaseManager.Instance.Connection;
                var record = connection.QuerySingleOrDefault<CalibrationRecord>(sql, new { calibration_id = calibrationId });

                if (record != null)
                {
                    record.CalibrationPoints = GetCalibrationPointsByCalibrationId(calibrationId);
                }

                return record;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationRepository.GetCalibrationById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 更新校准记录状态
        /// 修改校准记录的状态字段。
        /// </summary>
        /// <param name="calibrationId">校准记录ID</param>
        /// <param name="status">新状态: in_progress / completed / failed</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateCalibrationStatus(string calibrationId, string status)
        {
            try
            {
                const string sql = @"
                    UPDATE calibration_record SET
                        calibration_status = @calibration_status,
                        completed_at = @completed_at
                    WHERE calibration_id = @calibration_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    // 当状态为 completed 或 failed 时记录完成时间
                    long? completedAt = null;
                    if (status == "completed" || status == "failed")
                    {
                        completedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }

                    int result = connection.Execute(sql, new
                    {
                        calibration_id = calibrationId,
                        calibration_status = status,
                        completed_at = completedAt
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationRepository.UpdateCalibrationStatus] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 获取设备最新的有效校准记录
        /// 查询指定设备中 is_valid = 1 且状态为 completed 的最新一条校准记录。
        /// 通常用于判断设备的校准是否在有效期内。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <returns>最新的有效校准记录，不存在时返回 null</returns>
        public CalibrationRecord GetLatestValidCalibration(string deviceId)
        {
            try
            {
                const string sql = @"
                    SELECT calibration_id, device_id, calibration_type, calibration_status,
                           started_at, completed_at, performed_by, approved_by,
                           is_valid, valid_until, notes
                    FROM calibration_record
                    WHERE device_id = @device_id
                      AND is_valid = 1
                      AND calibration_status = 'completed'
                    ORDER BY completed_at DESC
                    LIMIT 1;";

                var connection = DatabaseManager.Instance.Connection;
                var record = connection.QuerySingleOrDefault<CalibrationRecord>(sql, new { device_id = deviceId });

                if (record != null)
                {
                    record.CalibrationPoints = GetCalibrationPointsByCalibrationId(record.CalibrationId);
                }

                return record;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationRepository.GetLatestValidCalibration] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据校准记录ID查询关联的校准点数据（私有辅助方法）
        /// </summary>
        /// <param name="calibrationId">校准记录ID</param>
        /// <returns>校准点列表，按序号升序排列</returns>
        private List<CalibrationPoint> GetCalibrationPointsByCalibrationId(string calibrationId)
        {
            const string sql = @"
                SELECT point_id, calibration_id, parameter_name, point_index,
                       reference_value, measured_value, corrected_value,
                       deviation_percent, is_pass
                FROM calibration_point
                WHERE calibration_id = @calibration_id
                ORDER BY point_index ASC;";

            var connection = DatabaseManager.Instance.Connection;
            return connection.Query<CalibrationPoint>(sql, new { calibration_id = calibrationId }).AsList();
        }
    }
}
