using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 充放电会话数据仓库——封装充放电会话的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供会话的CRUD、活跃会话查询、
//       会话历史检索、会话终止以及累计数据更新等功能。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 充放电会话数据仓库
    /// 提供对 charge_session（充放电会话）表的完整数据访问。
    /// 充放电会话记录了每一次充放电测试的设定参数、实时状态和累计数据。
    /// </summary>
    public class SessionRepository
    {
        /// <summary>
        /// 插入新的充放电会话
        /// 向 charge_session 表中创建一条新的会话记录。使用事务保证原子性。
        /// </summary>
        /// <param name="session">充放电会话实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertSession(ChargeSession session)
        {
            try
            {
                const string sql = @"
                    INSERT INTO charge_session (
                        session_id, device_id, battery_protocol, session_type, status,
                        start_time, end_time,
                        target_voltage_v, target_current_a, target_power_kw,
                        target_soc_percent, target_duration_s, cutoff_voltage_v,
                        total_energy_kwh, total_charge_ah,
                        max_voltage_v, min_voltage_v, max_current_a, max_temperature_c,
                        created_by, notes
                    ) VALUES (
                        @session_id, @device_id, @battery_protocol, @session_type, @status,
                        @start_time, @end_time,
                        @target_voltage_v, @target_current_a, @target_power_kw,
                        @target_soc_percent, @target_duration_s, @cutoff_voltage_v,
                        @total_energy_kwh, @total_charge_ah,
                        @max_voltage_v, @min_voltage_v, @max_current_a, @max_temperature_c,
                        @created_by, @notes
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        session_id = session.SessionId,
                        device_id = session.DeviceId,
                        battery_protocol = session.BatteryProtocol,
                        session_type = session.SessionType,
                        status = session.Status ?? "running",
                        start_time = session.StartTime,
                        end_time = session.EndTime,
                        target_voltage_v = session.TargetVoltageV,
                        target_current_a = session.TargetCurrentA,
                        target_power_kw = session.TargetPowerKw,
                        target_soc_percent = session.TargetSocPercent,
                        target_duration_s = session.TargetDurationS,
                        cutoff_voltage_v = session.CutoffVoltageV,
                        total_energy_kwh = session.TotalEnergyKwh,
                        total_charge_ah = session.TotalChargeAh,
                        max_voltage_v = session.MaxVoltageV,
                        min_voltage_v = session.MinVoltageV,
                        max_current_a = session.MaxCurrentA,
                        max_temperature_c = session.MaxTemperatureC,
                        created_by = session.CreatedBy,
                        notes = session.Notes
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionRepository.InsertSession] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新充放电会话信息
        /// 根据会话ID更新会话的设定参数、状态等信息。
        /// </summary>
        /// <param name="session">充放电会话实体（需包含有效的 SessionId）</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateSession(ChargeSession session)
        {
            try
            {
                const string sql = @"
                    UPDATE charge_session SET
                        device_id = @device_id,
                        battery_protocol = @battery_protocol,
                        session_type = @session_type,
                        status = @status,
                        start_time = @start_time,
                        end_time = @end_time,
                        target_voltage_v = @target_voltage_v,
                        target_current_a = @target_current_a,
                        target_power_kw = @target_power_kw,
                        target_soc_percent = @target_soc_percent,
                        target_duration_s = @target_duration_s,
                        cutoff_voltage_v = @cutoff_voltage_v,
                        total_energy_kwh = @total_energy_kwh,
                        total_charge_ah = @total_charge_ah,
                        max_voltage_v = @max_voltage_v,
                        min_voltage_v = @min_voltage_v,
                        max_current_a = @max_current_a,
                        max_temperature_c = @max_temperature_c,
                        notes = @notes
                    WHERE session_id = @session_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        session_id = session.SessionId,
                        device_id = session.DeviceId,
                        battery_protocol = session.BatteryProtocol,
                        session_type = session.SessionType,
                        status = session.Status,
                        start_time = session.StartTime,
                        end_time = session.EndTime,
                        target_voltage_v = session.TargetVoltageV,
                        target_current_a = session.TargetCurrentA,
                        target_power_kw = session.TargetPowerKw,
                        target_soc_percent = session.TargetSocPercent,
                        target_duration_s = session.TargetDurationS,
                        cutoff_voltage_v = session.CutoffVoltageV,
                        total_energy_kwh = session.TotalEnergyKwh,
                        total_charge_ah = session.TotalChargeAh,
                        max_voltage_v = session.MaxVoltageV,
                        min_voltage_v = session.MinVoltageV,
                        max_current_a = session.MaxCurrentA,
                        max_temperature_c = session.MaxTemperatureC,
                        notes = session.Notes
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionRepository.UpdateSession] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 根据会话ID获取单个会话信息
        /// </summary>
        /// <param name="sessionId">会话唯一标识</param>
        /// <returns>会话对象，未找到返回 null</returns>
        public ChargeSession GetSessionById(string sessionId)
        {
            try
            {
                const string sql = @"
                    SELECT session_id, device_id, battery_protocol, session_type, status,
                           start_time, end_time,
                           target_voltage_v, target_current_a, target_power_kw,
                           target_soc_percent, target_duration_s, cutoff_voltage_v,
                           total_energy_kwh, total_charge_ah,
                           max_voltage_v, min_voltage_v, max_current_a, max_temperature_c,
                           created_by, notes
                    FROM charge_session
                    WHERE session_id = @session_id;";

                var connection = DatabaseManager.Instance.Connection;
                var session = connection.QuerySingleOrDefault<ChargeSession>(sql, new { session_id = sessionId });
                return session;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionRepository.GetSessionById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有活跃的充放电会话
        /// 查询所有状态为 running 或 paused 的会话，按开始时间倒序排列。
        /// </summary>
        /// <returns>活跃会话列表</returns>
        public List<ChargeSession> GetActiveSessions()
        {
            try
            {
                const string sql = @"
                    SELECT session_id, device_id, battery_protocol, session_type, status,
                           start_time, end_time,
                           target_voltage_v, target_current_a, target_power_kw,
                           target_soc_percent, target_duration_s, cutoff_voltage_v,
                           total_energy_kwh, total_charge_ah,
                           max_voltage_v, min_voltage_v, max_current_a, max_temperature_c,
                           created_by, notes
                    FROM charge_session
                    WHERE status IN ('running', 'paused')
                    ORDER BY start_time DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var sessions = connection.Query<ChargeSession>(sql).AsList();
                return sessions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionRepository.GetActiveSessions] 错误: {ex.Message}");
                return new List<ChargeSession>();
            }
        }

        /// <summary>
        /// 查询会话历史记录
        /// 根据时间范围查询该时间段内开始（或结束）的会话记录。
        /// 查询条件：会话开始时间在 [startTime, endTime] 范围内，或结束时间在该范围内。
        /// 结果按开始时间倒序排列。
        /// </summary>
        /// <param name="startTime">起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">截止时间（Unix毫秒时间戳）</param>
        /// <returns>会话列表</returns>
        public List<ChargeSession> GetSessionHistory(long startTime, long endTime)
        {
            try
            {
                const string sql = @"
                    SELECT session_id, device_id, battery_protocol, session_type, status,
                           start_time, end_time,
                           target_voltage_v, target_current_a, target_power_kw,
                           target_soc_percent, target_duration_s, cutoff_voltage_v,
                           total_energy_kwh, total_charge_ah,
                           max_voltage_v, min_voltage_v, max_current_a, max_temperature_c,
                           created_by, notes
                    FROM charge_session
                    WHERE (start_time >= @start_time AND start_time <= @end_time)
                       OR (end_time >= @start_time AND end_time <= @end_time)
                    ORDER BY start_time DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var sessions = connection.Query<ChargeSession>(sql, new
                {
                    start_time = startTime,
                    end_time = endTime
                }).AsList();
                return sessions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionRepository.GetSessionHistory] 错误: {ex.Message}");
                return new List<ChargeSession>();
            }
        }

        /// <summary>
        /// 终止指定的充放电会话
        /// 设置会话的结束时间和最终状态，将该会话标记为非活跃状态。
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="endTime">会话结束时间（Unix毫秒时间戳）</param>
        /// <param name="status">终止状态: completed / aborted / fault</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int EndSession(string sessionId, long endTime, string status)
        {
            try
            {
                const string sql = @"
                    UPDATE charge_session SET
                        end_time = @end_time,
                        status = @status
                    WHERE session_id = @session_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        session_id = sessionId,
                        end_time = endTime,
                        status = status
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionRepository.EndSession] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新会话累计数据
        /// 更新充放电会话运行过程中产生的累计统计数据，包括累计能量、安时、
        /// 最高/最低电压、最大电流和最高温度。
        /// 此方法通常在会话运行期间由数据采集模块定期调用。
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="energy">累计能量(kWh)</param>
        /// <param name="ah">累计安时(Ah)</param>
        /// <param name="maxVoltage">最高电压(V)</param>
        /// <param name="minVoltage">最低电压(V)</param>
        /// <param name="maxCurrent">最大电流(A)</param>
        /// <param name="maxTemp">最高温度(°C)</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateSessionCumulativeData(string sessionId, double energy, double ah,
            double maxVoltage, double minVoltage, double maxCurrent, double maxTemp)
        {
            try
            {
                const string sql = @"
                    UPDATE charge_session SET
                        total_energy_kwh = @total_energy_kwh,
                        total_charge_ah = @total_charge_ah,
                        max_voltage_v = @max_voltage_v,
                        min_voltage_v = @min_voltage_v,
                        max_current_a = @max_current_a,
                        max_temperature_c = @max_temperature_c
                    WHERE session_id = @session_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    session_id = sessionId,
                    total_energy_kwh = energy,
                    total_charge_ah = ah,
                    max_voltage_v = maxVoltage,
                    min_voltage_v = minVoltage,
                    max_current_a = maxCurrent,
                    max_temperature_c = maxTemp
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionRepository.UpdateSessionCumulativeData] 错误: {ex.Message}");
                return -1;
            }
        }
    }
}
