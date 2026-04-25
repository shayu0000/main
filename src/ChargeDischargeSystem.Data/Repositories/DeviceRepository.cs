using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 设备数据仓库——封装设备信息、设备状态及告警的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库（通过 DatabaseManager.Instance.Connection）。
//       提供设备的CRUD操作、状态历史查询、告警管理等功能。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 设备数据仓库
    /// 提供对 device_info（设备信息）、device_status（设备状态）和
    /// device_alarm（设备告警）三张表的完整数据访问。
    /// 所有方法均包含 try-catch 错误处理，重要写操作使用事务。
    /// </summary>
    public class DeviceRepository
    {
        /// <summary>
        /// 获取所有设备列表
        /// 从 device_info 表中查询全部设备信息，按注册时间倒序排列。
        /// </summary>
        /// <returns>设备信息列表，若发生异常则返回空列表</returns>
        public List<DeviceInfo> GetAllDevices()
        {
            try
            {
                const string sql = @"
                    SELECT device_id, device_type, device_name, manufacturer, model,
                           serial_number, rated_power_kw, rated_voltage_v, rated_current_a,
                           can_address, protocol_name, firmware_version, status,
                           registered_at, last_online_time, notes
                    FROM device_info
                    ORDER BY registered_at DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var devices = connection.Query<DeviceInfo>(sql).AsList();
                return devices;
            }
            catch (Exception ex)
            {
                // 记录错误日志并返回空列表
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.GetAllDevices] 错误: {ex.Message}");
                return new List<DeviceInfo>();
            }
        }

        /// <summary>
        /// 根据设备ID获取单个设备信息
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <returns>设备信息对象，未找到时返回 null</returns>
        public DeviceInfo GetDeviceById(string deviceId)
        {
            try
            {
                const string sql = @"
                    SELECT device_id, device_type, device_name, manufacturer, model,
                           serial_number, rated_power_kw, rated_voltage_v, rated_current_a,
                           can_address, protocol_name, firmware_version, status,
                           registered_at, last_online_time, notes
                    FROM device_info
                    WHERE device_id = @device_id;";

                var connection = DatabaseManager.Instance.Connection;
                var device = connection.QuerySingleOrDefault<DeviceInfo>(sql, new { device_id = deviceId });
                return device;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.GetDeviceById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 插入新设备信息
        /// 使用事务确保数据写入的原子性。
        /// </summary>
        /// <param name="device">设备信息实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertDevice(DeviceInfo device)
        {
            try
            {
                const string sql = @"
                    INSERT INTO device_info (
                        device_id, device_type, device_name, manufacturer, model,
                        serial_number, rated_power_kw, rated_voltage_v, rated_current_a,
                        can_address, protocol_name, firmware_version, status,
                        registered_at, last_online_time, notes
                    ) VALUES (
                        @device_id, @device_type, @device_name, @manufacturer, @model,
                        @serial_number, @rated_power_kw, @rated_voltage_v, @rated_current_a,
                        @can_address, @protocol_name, @firmware_version, @status,
                        @registered_at, @last_online_time, @notes
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        device_id = device.DeviceId,
                        device_type = device.DeviceType,
                        device_name = device.DeviceName,
                        manufacturer = device.Manufacturer,
                        model = device.Model,
                        serial_number = device.SerialNumber,
                        rated_power_kw = device.RatedPowerKw,
                        rated_voltage_v = device.RatedVoltageV,
                        rated_current_a = device.RatedCurrentA,
                        can_address = device.CanAddress,
                        protocol_name = device.ProtocolName,
                        firmware_version = device.FirmwareVersion,
                        status = device.Status ?? "Offline",
                        registered_at = device.RegisteredAt,
                        last_online_time = device.LastOnlineTime,
                        notes = device.Notes
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.InsertDevice] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新设备信息
        /// 根据设备ID更新设备的所有可修改字段。
        /// </summary>
        /// <param name="device">设备信息实体（需包含有效的 DeviceId）</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateDevice(DeviceInfo device)
        {
            try
            {
                const string sql = @"
                    UPDATE device_info SET
                        device_type = @device_type,
                        device_name = @device_name,
                        manufacturer = @manufacturer,
                        model = @model,
                        serial_number = @serial_number,
                        rated_power_kw = @rated_power_kw,
                        rated_voltage_v = @rated_voltage_v,
                        rated_current_a = @rated_current_a,
                        can_address = @can_address,
                        protocol_name = @protocol_name,
                        firmware_version = @firmware_version,
                        status = @status,
                        last_online_time = @last_online_time,
                        notes = @notes
                    WHERE device_id = @device_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        device_id = device.DeviceId,
                        device_type = device.DeviceType,
                        device_name = device.DeviceName,
                        manufacturer = device.Manufacturer,
                        model = device.Model,
                        serial_number = device.SerialNumber,
                        rated_power_kw = device.RatedPowerKw,
                        rated_voltage_v = device.RatedVoltageV,
                        rated_current_a = device.RatedCurrentA,
                        can_address = device.CanAddress,
                        protocol_name = device.ProtocolName,
                        firmware_version = device.FirmwareVersion,
                        status = device.Status ?? "Offline",
                        last_online_time = device.LastOnlineTime,
                        notes = device.Notes
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.UpdateDevice] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 删除指定设备
        /// 从 device_info 表中删除对应设备记录。注意：删除前应确保没有外部引用。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int DeleteDevice(string deviceId)
        {
            try
            {
                const string sql = @"
                    DELETE FROM device_info
                    WHERE device_id = @device_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new { device_id = deviceId }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.DeleteDevice] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 获取设备状态历史记录
        /// 根据设备ID和时间范围查询 device_status 表中的历史状态数据，
        /// 按时间戳升序排列以保持时序顺序。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="startTime">查询起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">查询截止时间（Unix毫秒时间戳）</param>
        /// <returns>设备状态记录列表</returns>
        public List<DeviceStatusRecord> GetDeviceStatusHistory(string deviceId, long startTime, long endTime)
        {
            try
            {
                const string sql = @"
                    SELECT record_id, device_id, parameter_name, parameter_value,
                           unit, quality, timestamp
                    FROM device_status
                    WHERE device_id = @device_id
                      AND timestamp >= @start_time
                      AND timestamp <= @end_time
                    ORDER BY timestamp ASC;";

                var connection = DatabaseManager.Instance.Connection;
                var records = connection.Query<DeviceStatusRecord>(sql, new
                {
                    device_id = deviceId,
                    start_time = startTime,
                    end_time = endTime
                }).AsList();
                return records;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.GetDeviceStatusHistory] 错误: {ex.Message}");
                return new List<DeviceStatusRecord>();
            }
        }

        /// <summary>
        /// 插入设备状态记录
        /// 向 device_status 表中插入一条实时状态数据。
        /// </summary>
        /// <param name="status">设备状态记录实体</param>
        /// <returns>新插入记录的自增ID（record_id），失败时返回 -1</returns>
        public long InsertDeviceStatus(DeviceStatusRecord status)
        {
            try
            {
                const string sql = @"
                    INSERT INTO device_status (
                        device_id, parameter_name, parameter_value, unit, quality, timestamp
                    ) VALUES (
                        @device_id, @parameter_name, @parameter_value, @unit, @quality, @timestamp
                    );
                    SELECT last_insert_rowid();";

                var connection = DatabaseManager.Instance.Connection;
                long recordId = connection.ExecuteScalar<long>(sql, new
                {
                    device_id = status.DeviceId,
                    parameter_name = status.ParameterName,
                    parameter_value = status.ParameterValue,
                    unit = status.Unit,
                    quality = status.Quality ?? "GOOD",
                    timestamp = status.Timestamp
                });
                return recordId;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.InsertDeviceStatus] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 插入设备告警记录
        /// 向 device_alarm 表中插入一条告警信息。
        /// </summary>
        /// <param name="alarm">设备告警实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertDeviceAlarm(DeviceAlarm alarm)
        {
            try
            {
                const string sql = @"
                    INSERT INTO device_alarm (
                        alarm_id, device_id, alarm_code, alarm_level, alarm_message,
                        parameter_name, threshold_value, actual_value, is_active,
                        raised_at, acknowledged_at, acknowledged_by, cleared_at
                    ) VALUES (
                        @alarm_id, @device_id, @alarm_code, @alarm_level, @alarm_message,
                        @parameter_name, @threshold_value, @actual_value, @is_active,
                        @raised_at, @acknowledged_at, @acknowledged_by, @cleared_at
                    );";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    alarm_id = alarm.AlarmId,
                    device_id = alarm.DeviceId,
                    alarm_code = alarm.AlarmCode,
                    alarm_level = alarm.AlarmLevel,
                    alarm_message = alarm.AlarmMessage,
                    parameter_name = alarm.ParameterName,
                    threshold_value = alarm.ThresholdValue,
                    actual_value = alarm.ActualValue,
                    is_active = alarm.IsActive,
                    raised_at = alarm.RaisedAt,
                    acknowledged_at = alarm.AcknowledgedAt,
                    acknowledged_by = alarm.AcknowledgedBy,
                    cleared_at = alarm.ClearedAt
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.InsertDeviceAlarm] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 获取活跃告警列表
        /// 查询 device_alarm 表中所有活跃（is_active = 1）的告警记录。
        /// 可指定 deviceId 过滤特定设备，传 null 则查询所有设备的活跃告警。
        /// 结果按告警等级（严重>主要>次要>警告）和触发时间倒序排列。
        /// </summary>
        /// <param name="deviceId">可选，指定设备ID进行过滤；为 null 则查询全部</param>
        /// <returns>活跃告警列表</returns>
        public List<DeviceAlarm> GetActiveAlarms(string deviceId = null)
        {
            try
            {
                string sql;
                object parameters;

                if (string.IsNullOrEmpty(deviceId))
                {
                    sql = @"
                        SELECT alarm_id, device_id, alarm_code, alarm_level, alarm_message,
                               parameter_name, threshold_value, actual_value, is_active,
                               raised_at, acknowledged_at, acknowledged_by, cleared_at
                        FROM device_alarm
                        WHERE is_active = 1
                        ORDER BY 
                            CASE alarm_level
                                WHEN 'CRITICAL' THEN 1
                                WHEN 'MAJOR' THEN 2
                                WHEN 'MINOR' THEN 3
                                WHEN 'WARNING' THEN 4
                                ELSE 5
                            END,
                            raised_at DESC;";
                    parameters = null;
                }
                else
                {
                    sql = @"
                        SELECT alarm_id, device_id, alarm_code, alarm_level, alarm_message,
                               parameter_name, threshold_value, actual_value, is_active,
                               raised_at, acknowledged_at, acknowledged_by, cleared_at
                        FROM device_alarm
                        WHERE is_active = 1 AND device_id = @device_id
                        ORDER BY 
                            CASE alarm_level
                                WHEN 'CRITICAL' THEN 1
                                WHEN 'MAJOR' THEN 2
                                WHEN 'MINOR' THEN 3
                                WHEN 'WARNING' THEN 4
                                ELSE 5
                            END,
                            raised_at DESC;";
                    parameters = new { device_id = deviceId };
                }

                var connection = DatabaseManager.Instance.Connection;
                var alarms = connection.Query<DeviceAlarm>(sql, parameters).AsList();
                return alarms;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.GetActiveAlarms] 错误: {ex.Message}");
                return new List<DeviceAlarm>();
            }
        }

        /// <summary>
        /// 确认告警
        /// 将指定告警标记为已确认，记录确认人和确认时间。
        /// 确认操作仅对活跃告警有效。
        /// </summary>
        /// <param name="alarmId">告警ID</param>
        /// <param name="userId">确认人用户ID</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int AcknowledgeAlarm(string alarmId, string userId)
        {
            try
            {
                const string sql = @"
                    UPDATE device_alarm SET
                        acknowledged_at = @acknowledged_at,
                        acknowledged_by = @acknowledged_by
                    WHERE alarm_id = @alarm_id AND is_active = 1;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    int result = connection.Execute(sql, new
                    {
                        alarm_id = alarmId,
                        acknowledged_at = now,
                        acknowledged_by = userId
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.AcknowledgeAlarm] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 清除/关闭告警
        /// 将指定告警标记为非活跃状态，记录清除时间。
        /// </summary>
        /// <param name="alarmId">告警ID</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int ClearAlarm(string alarmId)
        {
            try
            {
                const string sql = @"
                    UPDATE device_alarm SET
                        is_active = 0,
                        cleared_at = @cleared_at
                    WHERE alarm_id = @alarm_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    int result = connection.Execute(sql, new
                    {
                        alarm_id = alarmId,
                        cleared_at = now
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceRepository.ClearAlarm] 错误: {ex.Message}");
                return -1;
            }
        }
    }
}
