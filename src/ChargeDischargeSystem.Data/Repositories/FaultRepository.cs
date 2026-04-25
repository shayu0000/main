using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 故障录波数据仓库——封装故障事件和录波波形的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供故障事件的记录查询、
//       录波波形数据的存储、故障分析标注和导出标记等功能。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 故障录波数据仓库
    /// 提供对 fault_event（故障事件）和 fault_waveform（故障录波波形）
    /// 两张表的完整数据访问。故障录波系统在设备发生故障时自动触发，
    /// 记录故障前后时间段内的详细波形数据，用于故障分析。
    /// </summary>
    public class FaultRepository
    {
        /// <summary>
        /// 插入故障事件记录
        /// 向 fault_event 表插入一条新的故障事件。使用事务保证原子性。
        /// </summary>
        /// <param name="faultEvent">故障事件实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertFaultEvent(FaultEvent faultEvent)
        {
            try
            {
                const string sql = @"
                    INSERT INTO fault_event (
                        event_id, device_id, session_id, fault_code, fault_level,
                        fault_description, triggered_at, trigger_channel, trigger_value,
                        pre_fault_samples, post_fault_samples, sample_rate_hz,
                        waveform_data_path, is_exported, analyzed_by, analysis_notes
                    ) VALUES (
                        @event_id, @device_id, @session_id, @fault_code, @fault_level,
                        @fault_description, @triggered_at, @trigger_channel, @trigger_value,
                        @pre_fault_samples, @post_fault_samples, @sample_rate_hz,
                        @waveform_data_path, @is_exported, @analyzed_by, @analysis_notes
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        event_id = faultEvent.EventId,
                        device_id = faultEvent.DeviceId,
                        session_id = faultEvent.SessionId,
                        fault_code = faultEvent.FaultCode,
                        fault_level = faultEvent.FaultLevel,
                        fault_description = faultEvent.FaultDescription,
                        triggered_at = faultEvent.TriggeredAt,
                        trigger_channel = faultEvent.TriggerChannel,
                        trigger_value = faultEvent.TriggerValue,
                        pre_fault_samples = faultEvent.PreFaultSamples,
                        post_fault_samples = faultEvent.PostFaultSamples,
                        sample_rate_hz = faultEvent.SampleRateHz,
                        waveform_data_path = faultEvent.WaveformDataPath,
                        is_exported = faultEvent.IsExported,
                        analyzed_by = faultEvent.AnalyzedBy,
                        analysis_notes = faultEvent.AnalysisNotes
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRepository.InsertFaultEvent] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 插入故障录波波形数据
        /// 向 fault_waveform 表插入单通道的录波波形数据。
        /// </summary>
        /// <param name="waveform">故障录波波形实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertFaultWaveform(FaultWaveform waveform)
        {
            try
            {
                const string sql = @"
                    INSERT INTO fault_waveform (
                        waveform_id, event_id, channel_index, channel_name,
                        data_blob, data_size, unit
                    ) VALUES (
                        @waveform_id, @event_id, @channel_index, @channel_name,
                        @data_blob, @data_size, @unit
                    );";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    waveform_id = waveform.WaveformId,
                    event_id = waveform.EventId,
                    channel_index = waveform.ChannelIndex,
                    channel_name = waveform.ChannelName,
                    data_blob = waveform.DataBlob,
                    data_size = waveform.DataSize,
                    unit = waveform.Unit
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRepository.InsertFaultWaveform] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 根据事件ID获取单个故障事件
        /// 同时加载该事件关联的所有录波波形数据。
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <returns>故障事件对象（含波形集合），未找到返回 null</returns>
        public FaultEvent GetFaultEventById(string eventId)
        {
            try
            {
                const string sql = @"
                    SELECT event_id, device_id, session_id, fault_code, fault_level,
                           fault_description, triggered_at, trigger_channel, trigger_value,
                           pre_fault_samples, post_fault_samples, sample_rate_hz,
                           waveform_data_path, is_exported, analyzed_by, analysis_notes
                    FROM fault_event
                    WHERE event_id = @event_id;";

                var connection = DatabaseManager.Instance.Connection;
                var faultEvent = connection.QuerySingleOrDefault<FaultEvent>(sql, new { event_id = eventId });

                if (faultEvent != null)
                {
                    faultEvent.Waveforms = GetWaveformByEventIdInternal(eventId);
                }

                return faultEvent;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRepository.GetFaultEventById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 查询故障事件列表
        /// 根据设备ID、时间范围和可选的故障等级筛选故障事件。
        /// 结果按触发时间倒序排列。
        /// </summary>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="startTime">查询起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">查询截止时间（Unix毫秒时间戳）</param>
        /// <param name="faultLevel">可选，故障等级筛选: CRITICAL / MAJOR / MINOR，null 表示不过滤</param>
        /// <returns>故障事件列表</returns>
        public List<FaultEvent> GetFaultEvents(string deviceId, long startTime, long endTime,
            string faultLevel = null)
        {
            try
            {
                string sql;
                object parameters;

                if (string.IsNullOrEmpty(faultLevel))
                {
                    sql = @"
                        SELECT event_id, device_id, session_id, fault_code, fault_level,
                               fault_description, triggered_at, trigger_channel, trigger_value,
                               pre_fault_samples, post_fault_samples, sample_rate_hz,
                               waveform_data_path, is_exported, analyzed_by, analysis_notes
                        FROM fault_event
                        WHERE device_id = @device_id
                          AND triggered_at >= @start_time
                          AND triggered_at <= @end_time
                        ORDER BY triggered_at DESC;";
                    parameters = new
                    {
                        device_id = deviceId,
                        start_time = startTime,
                        end_time = endTime
                    };
                }
                else
                {
                    sql = @"
                        SELECT event_id, device_id, session_id, fault_code, fault_level,
                               fault_description, triggered_at, trigger_channel, trigger_value,
                               pre_fault_samples, post_fault_samples, sample_rate_hz,
                               waveform_data_path, is_exported, analyzed_by, analysis_notes
                        FROM fault_event
                        WHERE device_id = @device_id
                          AND triggered_at >= @start_time
                          AND triggered_at <= @end_time
                          AND fault_level = @fault_level
                        ORDER BY triggered_at DESC;";
                    parameters = new
                    {
                        device_id = deviceId,
                        start_time = startTime,
                        end_time = endTime,
                        fault_level = faultLevel
                    };
                }

                var connection = DatabaseManager.Instance.Connection;
                var events = connection.Query<FaultEvent>(sql, parameters).AsList();
                return events;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRepository.GetFaultEvents] 错误: {ex.Message}");
                return new List<FaultEvent>();
            }
        }

        /// <summary>
        /// 根据故障事件ID获取关联的录波波形数据
        /// 从 fault_waveform 表查询该故障事件的所有通道波形。
        /// 结果按通道索引升序排列。
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <returns>录波波形数据列表</returns>
        public List<FaultWaveform> GetWaveformByEventId(string eventId)
        {
            try
            {
                return GetWaveformByEventIdInternal(eventId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRepository.GetWaveformByEventId] 错误: {ex.Message}");
                return new List<FaultWaveform>();
            }
        }

        /// <summary>
        /// 更新故障分析信息
        /// 记录故障分析人员和分析备注/结论。
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <param name="analystId">分析人用户ID</param>
        /// <param name="notes">分析备注/结论</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateFaultAnalysis(string eventId, string analystId, string notes)
        {
            try
            {
                const string sql = @"
                    UPDATE fault_event SET
                        analyzed_by = @analyzed_by,
                        analysis_notes = @analysis_notes
                    WHERE event_id = @event_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        event_id = eventId,
                        analyzed_by = analystId,
                        analysis_notes = notes
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRepository.UpdateFaultAnalysis] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 标记故障事件为已导出
        /// 将 is_exported 字段设置为 1，表示该故障事件的波形数据已导出到外部文件。
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int MarkExported(string eventId)
        {
            try
            {
                const string sql = @"
                    UPDATE fault_event SET
                        is_exported = 1
                    WHERE event_id = @event_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new { event_id = eventId });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRepository.MarkExported] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 内部方法：根据事件ID获取波形数据（供 GetFaultEventById 复用）
        /// </summary>
        private List<FaultWaveform> GetWaveformByEventIdInternal(string eventId)
        {
            const string sql = @"
                SELECT waveform_id, event_id, channel_index, channel_name,
                       data_blob, data_size, unit
                FROM fault_waveform
                WHERE event_id = @event_id
                ORDER BY channel_index ASC;";

            var connection = DatabaseManager.Instance.Connection;
            return connection.Query<FaultWaveform>(sql, new { event_id = eventId }).AsList();
        }
    }
}
