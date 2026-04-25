using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 数据记录服务实现类
// 说明: 实现测量数据的实时记录、批量写入和自动备份功能
//       使用DataBuffer缓冲区优化写入性能，支持定时刷新和自动备份
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 数据记录服务实现类
    /// 负责充放电系统中所有测量数据的记录和管理：
    ///   1. 实时记录：将设备上报的数据缓存到DataBuffer中
    ///   2. 批量写入：定时将缓冲区中的数据批量写入SQLite数据库
    ///   3. 数据查询：支持按时间范围、聚合方式查询历史数据
    ///   4. 自动备份：按配置的间隔定期备份数据库文件
    ///   5. 数据清理：删除过期的旧数据以释放存储空间
    /// </summary>
    public class DataLogService : IDataLogService, IDisposable
    {
        #region -- 字段定义 --

        /// <summary>设备监控服务引用</summary>
        private readonly IDeviceMonitorService _monitorService;

        /// <summary>应用程序配置引用</summary>
        private readonly AppConfig _appConfig;

        /// <summary>数据缓冲区（批量写入优化）</summary>
        private readonly DataBuffer<MeasurementData> _dataBuffer;

        /// <summary>缓冲区刷新定时器</summary>
        private Timer _flushTimer;

        /// <summary>备份定时器</summary>
        private Timer _backupTimer;

        /// <summary>定时器取消令牌</summary>
        private CancellationTokenSource _cts;

        /// <summary>当前活跃的会话ID</summary>
        private string _activeSessionId;

        /// <summary>记录状态</summary>
        private bool _isRecording;

        /// <summary>对象销毁标志</summary>
        private bool _isDisposed;

        /// <summary>事件操作锁</summary>
        private readonly object _eventLock = new object();

        #endregion

        #region -- 事件声明 --

        /// <summary>记录状态变化事件</summary>
        public event Action<string, string> OnRecordingStatusChanged;

        #endregion

        /// <summary>
        /// 构造数据记录服务实例
        /// </summary>
        /// <param name="monitorService">设备监控服务</param>
        /// <param name="appConfig">应用程序配置</param>
        public DataLogService(IDeviceMonitorService monitorService, AppConfig appConfig)
        {
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));

            int bufferSize = appConfig.DataLogging?.BufferMaxSize ?? DataLogConstants.MaxBufferSize;
            _dataBuffer = new DataBuffer<MeasurementData>(bufferSize);
        }

        #region -- 记录会话管理 --

        /// <summary>
        /// 启动数据记录会话
        /// 初始化数据缓冲区并启动定时刷新和备份机制
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="dataTypes">要记录的数据类型</param>
        /// <param name="sampleIntervalMs">采样间隔</param>
        /// <returns>启动是否成功</returns>
        public bool StartRecording(string sessionId, List<string> dataTypes = null, int sampleIntervalMs = 1000)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("会话ID不能为空", nameof(sessionId));

            if (_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("[DataLogService] 已有正在进行的记录会话，请先停止当前会话。");
                return false;
            }

            try
            {
                _activeSessionId = sessionId;
                _cts = new CancellationTokenSource();

                // 订阅设备数据更新事件
                _monitorService.OnDataUpdated += HandleDataUpdated;

                // 启动缓冲区刷新定时器（默认5秒刷新一次）
                int flushIntervalMs = _appConfig.DataLogging?.FlushIntervalMs ?? DataLogConstants.FlushIntervalMs;
                _flushTimer = new Timer(OnFlushTimerTick, null, flushIntervalMs, flushIntervalMs);

                // 启动自动备份定时器（默认24小时备份一次）
                int backupIntervalMs = (_appConfig.DataLogging?.AutoBackupIntervalHours ?? 24) * 3600 * 1000;
                _backupTimer = new Timer(OnBackupTimerTick, null, backupIntervalMs, backupIntervalMs);

                _isRecording = true;

                OnRecordingStatusChanged?.Invoke(sessionId, "Recording");
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 数据记录已启动: {sessionId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 启动记录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止数据记录会话
        /// 立即刷新缓冲区并清理定时器资源
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>停止是否成功</returns>
        public bool StopRecording(string sessionId)
        {
            if (!_isRecording || _activeSessionId != sessionId)
                return false;

            try
            {
                // 取消事件订阅
                _monitorService.OnDataUpdated -= HandleDataUpdated;

                // 停止定时器并最后一次刷新
                _cts?.Cancel();
                _flushTimer?.Dispose();
                _backupTimer?.Dispose();
                _flushTimer = null;
                _backupTimer = null;

                // 刷新剩余数据
                FlushBufferToDatabase();

                _isRecording = false;
                _activeSessionId = null;

                OnRecordingStatusChanged?.Invoke(sessionId, "Stopped");
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 数据记录已停止: {sessionId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 停止记录失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region -- 数据查询 --

        /// <summary>
        /// 查询历史测量数据
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="parameterName">参数名称</param>
        /// <param name="startTime">起始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <param name="aggregation">聚合方式</param>
        /// <returns>测量数据列表</returns>
        public List<MeasurementData> QueryData(string deviceId, string parameterName,
            long startTime, long endTime, string aggregation = "none")
        {
            // TODO: 注入MeasurementRepository进行数据库查询
            System.Diagnostics.Debug.WriteLine(
                $"[DataLogService] 查询数据: {deviceId}.{parameterName}, {startTime}~{endTime}, 聚合={aggregation}");
            return new List<MeasurementData>();
        }

        /// <summary>
        /// 获取记录会话列表
        /// </summary>
        /// <param name="startTime">起始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>会话列表</returns>
        public List<ChargeSession> GetSessionList(long startTime, long endTime)
        {
            // TODO: 注入ChargeSessionRepository查询
            System.Diagnostics.Debug.WriteLine($"[DataLogService] 查询会话列表: {startTime}~{endTime}");
            return new List<ChargeSession>();
        }

        /// <summary>
        /// 删除指定时间之前的旧数据
        /// </summary>
        /// <param name="beforeTime">删除此时间之前的数据</param>
        /// <returns>删除的数据条数</returns>
        public long DeleteOldData(long beforeTime)
        {
            // TODO: 注入MeasurementRepository执行DELETE操作
            System.Diagnostics.Debug.WriteLine($"[DataLogService] 删除旧数据: before={beforeTime}");
            return 0;
        }

        #endregion

        #region -- 内部方法 --

        /// <summary>
        /// 处理设备数据更新事件
        /// 将最新的设备数据转换为MeasurementData并加入缓冲区
        /// </summary>
        /// <param name="dataPoints">设备数据点字典</param>
        private void HandleDataUpdated(Dictionary<string, DeviceDataPoint> dataPoints)
        {
            if (!_isRecording || dataPoints == null) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var kvp in dataPoints)
            {
                var dp = kvp.Value;

                // 创建多条测量数据记录（每个参数一条）
                var measurements = new List<MeasurementData>
                {
                    new MeasurementData { Timestamp = now, DeviceId = dp.DeviceId, SessionId = _activeSessionId, ParameterName = "voltage", Value = dp.Voltage, Unit = "V", Quality = dp.Quality },
                    new MeasurementData { Timestamp = now, DeviceId = dp.DeviceId, SessionId = _activeSessionId, ParameterName = "current", Value = dp.Current, Unit = "A", Quality = dp.Quality },
                    new MeasurementData { Timestamp = now, DeviceId = dp.DeviceId, SessionId = _activeSessionId, ParameterName = "temperature", Value = dp.Temperature, Unit = "°C", Quality = dp.Quality },
                    new MeasurementData { Timestamp = now, DeviceId = dp.DeviceId, SessionId = _activeSessionId, ParameterName = "soc", Value = dp.Soc, Unit = "%", Quality = dp.Quality },
                    new MeasurementData { Timestamp = now, DeviceId = dp.DeviceId, SessionId = _activeSessionId, ParameterName = "power", Value = dp.Power, Unit = "kW", Quality = dp.Quality }
                };

                _dataBuffer.AddRange(measurements);

                // 缓冲区满时立即刷新
                if (_dataBuffer.IsFull)
                {
                    FlushBufferToDatabase();
                }
            }
        }

        /// <summary>
        /// 定时器回调：刷新缓冲区到数据库
        /// </summary>
        /// <param name="state">状态对象（未使用）</param>
        private void OnFlushTimerTick(object state)
        {
            FlushBufferToDatabase();
        }

        /// <summary>
        /// 将缓冲区中的数据批量写入数据库
        /// </summary>
        private void FlushBufferToDatabase()
        {
            var data = _dataBuffer.Flush();
            if (data.Count == 0) return;

            try
            {
                // TODO: 注入MeasurementRepository并调用 BulkInsertAsync(data)
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 缓冲区刷新: {data.Count} 条数据");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 缓冲区刷新失败: {ex.Message}");
                // 刷新失败时数据会丢失，可根据业务需求加入重试或持久化机制
            }
        }

        /// <summary>
        /// 定时器回调：自动备份数据库
        /// </summary>
        /// <param name="state">状态对象（未使用）</param>
        private void OnBackupTimerTick(object state)
        {
            PerformDatabaseBackup();
        }

        /// <summary>
        /// 执行数据库自动备份
        /// 将数据库文件复制到备份目录，命名包含时间戳
        /// </summary>
        private void PerformDatabaseBackup()
        {
            try
            {
                string dbPath = _appConfig.DatabasePath;
                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                    return;

                string backupDir = SystemConstants.DefaultBackupDir;
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupDir, $"mw_scada_backup_{timestamp}.db");

                // 使用DatabaseManager的在线备份功能
                DatabaseManager.Instance.BackupDatabase(backupFile);

                System.Diagnostics.Debug.WriteLine($"[DataLogService] 数据库备份完成: {backupFile}");

                // 清理旧备份（保留最近5个）
                CleanOldBackups(backupDir, 5);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 数据库备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理旧备份文件，保留最近N个
        /// </summary>
        /// <param name="backupDir">备份目录</param>
        /// <param name="keepCount">保留数量</param>
        private void CleanOldBackups(string backupDir, int keepCount)
        {
            try
            {
                var files = Directory.GetFiles(backupDir, "mw_scada_backup_*.db");
                if (files.Length <= keepCount) return;

                // 按文件名排序（时间戳在文件名中），删除最旧的
                Array.Sort(files);
                for (int i = 0; i < files.Length - keepCount; i++)
                {
                    File.Delete(files[i]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 清理旧备份失败: {ex.Message}");
            }
        }

        #endregion

        #region -- IDisposable 实现 --

        /// <summary>
        /// 释放数据记录服务占用的所有资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_isRecording)
                StopRecording(_activeSessionId);

            _flushTimer?.Dispose();
            _backupTimer?.Dispose();
            _cts?.Dispose();
        }

        #endregion
    }
}
