using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;
using ChargeDischargeSystem.Data.Repositories;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 数据记录服务实现类
// 说明: 实现测量数据的实时记录、批量写入和自动备份功能
//       使用 DataBuffer 缓冲区优化写入性能，支持定时刷新和自动备份
//       每次数据库事务最多写入 1000 条记录
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 数据记录服务实现类
    /// 负责充放电系统中所有测量数据的记录和管理：
    ///   1. 实时记录：将设备上报的数据缓存到 DataBuffer 中，支持按数据类型过滤
    ///   2. 批量写入：定时将缓冲区中的数据分批写入 SQLite 数据库，每事务最多 1000 条
    ///   3. 数据查询：支持按时间范围、多参数、聚合方式查询历史数据
    ///   4. 自动备份：按配置的间隔定期备份数据库文件
    ///   5. 数据清理：删除过期的旧数据以释放存储空间
    /// </summary>
    public class DataLogService : IDataLogService, IDisposable
    {
        #region -- 常量 --

        /// <summary>单次数据库事务最大写入记录数</summary>
        private const int MaxBatchSize = 1000;

        #endregion

        #region -- 依赖注入 --

        /// <summary>设备监控服务引用</summary>
        private readonly IDeviceMonitorService _monitorService;

        /// <summary>应用程序配置引用</summary>
        private readonly AppConfig _appConfig;

        /// <summary>测量数据仓库</summary>
        private readonly MeasurementRepository _measurementRepo;

        /// <summary>充放电会话仓库</summary>
        private readonly SessionRepository _sessionRepo;

        #endregion

        #region -- 缓冲区 --

        /// <summary>数据缓冲区（批量写入优化）</summary>
        private DataBuffer<MeasurementData> _dataBuffer;

        /// <summary>缓冲区刷新互斥锁（防止并发刷新）</summary>
        private readonly object _flushLock = new object();

        #endregion

        #region -- 定时器 --

        /// <summary>缓冲区刷新定时器</summary>
        private Timer _flushTimer;

        /// <summary>备份定时器</summary>
        private Timer _backupTimer;

        /// <summary>定时器取消令牌</summary>
        private CancellationTokenSource _cts;

        #endregion

        #region -- 状态 --

        /// <summary>当前活跃的会话ID</summary>
        private string _activeSessionId;

        /// <summary>本次记录的数据类型集合（null 表示记录全部类型）</summary>
        private HashSet<string> _recordedDataTypes;

        /// <summary>采样间隔（毫秒）</summary>
        private int _sampleIntervalMs;

        /// <summary>上次采样时的时间戳（用于采样间隔控制）</summary>
        private long _lastSampleTime;

        /// <summary>记录状态</summary>
        private volatile bool _isRecording;

        /// <summary>对象销毁标志</summary>
        private volatile bool _isDisposed;

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
        /// <param name="measurementRepo">测量数据仓库</param>
        /// <param name="sessionRepo">充放电会话仓库</param>
        public DataLogService(
            IDeviceMonitorService monitorService,
            AppConfig appConfig,
            MeasurementRepository measurementRepo,
            SessionRepository sessionRepo)
        {
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _measurementRepo = measurementRepo ?? throw new ArgumentNullException(nameof(measurementRepo));
            _sessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
        }

        #region -- 记录会话管理 --

        /// <summary>
        /// 启动数据记录会话
        /// 初始化数据缓冲区并启动定时刷新和备份机制
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="dataTypes">要记录的数据类型（null 表示记录全部），可选值: voltage/current/temperature/soc/power</param>
        /// <param name="sampleIntervalMs">采样间隔（毫秒），默认 1000ms</param>
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
                _sampleIntervalMs = sampleIntervalMs;
                _lastSampleTime = 0;

                // 构建数据类型过滤集合
                if (dataTypes != null && dataTypes.Count > 0)
                    _recordedDataTypes = new HashSet<string>(dataTypes, StringComparer.OrdinalIgnoreCase);
                else
                    _recordedDataTypes = null;

                // 初始化数据缓冲区
                int bufferSize = _appConfig.DataLogging?.BufferMaxSize ?? DataLogConstants.MaxBufferSize;
                _dataBuffer = new DataBuffer<MeasurementData>(bufferSize);

                _cts = new CancellationTokenSource();

                // 订阅设备数据更新事件
                _monitorService.OnDataUpdated += HandleDataUpdated;

                // 启动缓冲区刷新定时器（默认5000ms刷新一次）
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
                _recordedDataTypes = null;
                _dataBuffer = null;

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
        /// 支持按设备、多个参数名称、时间范围和聚合方式查询
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="parameterNames">参数名称列表（voltage/current/temperature/soc/power）</param>
        /// <param name="startTime">起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">结束时间（Unix毫秒时间戳）</param>
        /// <param name="aggregation">聚合方式: none(原始值)/avg(均值)/min(最小值)/max(最大值)</param>
        /// <returns>测量数据列表</returns>
        public List<MeasurementData> QueryData(string deviceId, List<string> parameterNames,
            long startTime, long endTime, string aggregation = "none")
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));
            if (parameterNames == null || parameterNames.Count == 0)
                return new List<MeasurementData>();

            try
            {
                var results = new List<MeasurementData>();
                foreach (var paramName in parameterNames)
                {
                    if (string.IsNullOrEmpty(paramName)) continue;
                    var queryResults = _measurementRepo.QueryMeasurements(
                        deviceId, paramName, startTime, endTime, aggregation);
                    if (queryResults != null && queryResults.Count > 0)
                        results.AddRange(queryResults);
                }
                return results;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 查询数据失败: {ex.Message}");
                return new List<MeasurementData>();
            }
        }

        /// <summary>
        /// 获取记录会话列表
        /// 查询指定时间范围内开始或结束的充放电会话
        /// </summary>
        /// <param name="startTime">起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">结束时间（Unix毫秒时间戳）</param>
        /// <returns>会话列表</returns>
        public List<ChargeSession> GetSessionList(long startTime, long endTime)
        {
            try
            {
                return _sessionRepo.GetSessionHistory(startTime, endTime);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 查询会话列表失败: {ex.Message}");
                return new List<ChargeSession>();
            }
        }

        /// <summary>
        /// 删除指定时间之前的旧数据
        /// 用于定期清理过期测量数据，防止数据库文件无限增长
        /// </summary>
        /// <param name="beforeTime">删除此时间之前的数据（Unix毫秒时间戳）</param>
        /// <returns>删除的数据条数</returns>
        public long DeleteOldData(long beforeTime)
        {
            try
            {
                int deleted = _measurementRepo.DeleteOldData(beforeTime);
                return deleted >= 0 ? deleted : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataLogService] 删除旧数据失败: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region -- 内部方法 --

        /// <summary>
        /// 处理设备数据更新事件
        /// 将最新的设备数据转换为 MeasurementData 并加入缓冲区，按数据类型过滤
        /// </summary>
        /// <param name="dataPoints">设备数据点字典</param>
        private void HandleDataUpdated(Dictionary<string, DeviceDataPoint> dataPoints)
        {
            if (!_isRecording || dataPoints == null || dataPoints.Count == 0)
                return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 采样间隔控制：未达到采样间隔则跳过本次数据
            if (_sampleIntervalMs > 0)
            {
                long prev = Interlocked.Read(ref _lastSampleTime);
                if (now - prev < _sampleIntervalMs)
                    return;
                Interlocked.Exchange(ref _lastSampleTime, now);
            }

            foreach (var kvp in dataPoints)
            {
                var dp = kvp.Value;
                var measurements = new List<MeasurementData>(5);

                // 仅在数据类型过滤集合允许时添加对应测量项
                void AddIfAllowed(string paramName, double value, string unit)
                {
                    if (_recordedDataTypes == null || _recordedDataTypes.Contains(paramName))
                    {
                        measurements.Add(new MeasurementData
                        {
                            Timestamp = now,
                            DeviceId = dp.DeviceId,
                            SessionId = _activeSessionId,
                            ParameterName = paramName,
                            Value = value,
                            Unit = unit,
                            Quality = dp.Quality ?? "GOOD"
                        });
                    }
                }

                AddIfAllowed("voltage", dp.Voltage, "V");
                AddIfAllowed("current", dp.Current, "A");
                AddIfAllowed("temperature", dp.Temperature, "°C");
                AddIfAllowed("soc", dp.Soc, "%");
                AddIfAllowed("power", dp.Power, "kW");

                if (measurements.Count > 0)
                {
                    _dataBuffer.AddRange(measurements);

                    // 缓冲区满时立即刷新
                    if (_dataBuffer.IsFull)
                    {
                        FlushBufferToDatabase();
                    }
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
        /// 每次数据库事务最多写入 MaxBatchSize（1000）条记录
        /// </summary>
        private void FlushBufferToDatabase()
        {
            if (_dataBuffer == null) return;

            List<MeasurementData> batch;
            lock (_flushLock)
            {
                if (_dataBuffer.Count == 0) return;
                batch = _dataBuffer.Flush();
            }

            if (batch == null || batch.Count == 0) return;

            try
            {
                // 分批写入，每事务最多 MaxBatchSize 条
                for (int i = 0; i < batch.Count; i += MaxBatchSize)
                {
                    int chunkSize = Math.Min(MaxBatchSize, batch.Count - i);
                    var chunk = batch.GetRange(i, chunkSize);
                    int inserted = _measurementRepo.InsertMeasurementsBatch(chunk);
                    if (inserted < 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[DataLogService] 批量插入失败: chunk起始={i}, chunk大小={chunkSize}");
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[DataLogService] 缓冲区刷新: {batch.Count} 条数据");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DataLogService] 缓冲区刷新失败: {ex.Message}");
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

                DatabaseManager.Instance.BackupDatabase(backupFile);

                System.Diagnostics.Debug.WriteLine(
                    $"[DataLogService] 数据库备份完成: {backupFile}");

                CleanOldBackups(backupDir, 5);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DataLogService] 数据库备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理旧备份文件，保留最近 N 个
        /// </summary>
        /// <param name="backupDir">备份目录</param>
        /// <param name="keepCount">保留数量</param>
        private void CleanOldBackups(string backupDir, int keepCount)
        {
            try
            {
                var files = Directory.GetFiles(backupDir, "mw_scada_backup_*.db");
                if (files.Length <= keepCount) return;

                Array.Sort(files);
                for (int i = 0; i < files.Length - keepCount; i++)
                {
                    File.Delete(files[i]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[DataLogService] 清理旧备份失败: {ex.Message}");
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
