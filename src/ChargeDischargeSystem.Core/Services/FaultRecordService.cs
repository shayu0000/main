using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Repositories;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 故障录波服务实现类
// 说明: 实现设备故障的自动检测和波形数据记录
//       使用循环缓冲区存储故障前后数据，支持双采样率模式
//       数据持久化通过 FaultRepository 存入 SQLite
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 故障录波服务实现类
    /// 核心功能：
    ///   1. 循环缓冲区：使用CircularBuffer存储固定时长的预故障采样数据
    ///   2. 双采样模式：
    ///      - 正常采样(10Hz): 日常监控使用，预故障1000点/后故障2000点
    ///      - 高速采样(1kHz): 故障触发时切换，预故障5000点/后故障10000点
    ///   3. 故障触发：当检测到参数超过配置阈值时，自动保存预故障缓冲区和后故障采集数据
    ///   4. 波形持久化：通过FaultRepository将故障事件和波形BLOB存入SQLite
    ///   5. 波形导出：将录波数据导出为CSV或JSON格式文件
    ///   6. 线程安全：使用 lock 保护所有共享状态
    /// </summary>
    public class FaultRecordService : IFaultRecordService, IDisposable
    {
        #region -- 字段定义 --

        private readonly FaultRepository _faultRepository;

        private readonly Dictionary<string, FaultTriggerConfig> _configs = new Dictionary<string, FaultTriggerConfig>();

        private readonly Dictionary<string, CircularBuffer<double>> _preFaultBuffers = new Dictionary<string, CircularBuffer<double>>();

        private readonly Dictionary<string, PostFaultCollectionState> _activePostFaultCollectors = new Dictionary<string, PostFaultCollectionState>();

        private readonly object _syncRoot = new object();

        private bool _isDisposed;

        #endregion

        #region -- 事件声明 --

        public event Action<FaultEvent> OnFaultOccurred;

        #endregion

        #region -- 内部类型 --

        /// <summary>
        /// 后故障数据采集状态
        /// 记录故障触发后正在收集的波形数据和剩余样点数
        /// </summary>
        private sealed class PostFaultCollectionState
        {
            public string EventId { get; set; }
            public string DeviceId { get; set; }
            public FaultTriggerConfig Config { get; set; }
            public FaultEvent FaultEvent { get; set; }
            public Dictionary<string, double[]> PreFaultData { get; set; }
            public Dictionary<string, List<double>> PostFaultData { get; set; }
            public int PostFaultSamplesTotal { get; set; }
            public int PostFaultSamplesCollected { get; set; }
        }

        #endregion

        /// <summary>
        /// 构造故障录波服务实例
        /// </summary>
        /// <param name="faultRepository">故障录波数据仓库</param>
        public FaultRecordService(FaultRepository faultRepository)
        {
            _faultRepository = faultRepository ?? throw new ArgumentNullException(nameof(faultRepository));
        }

        #region -- 公开方法：FeedDataPoint（外部数据馈送入口） --

        /// <summary>
        /// 向故障录波服务馈送一条设备数据点
        /// 数据写入循环缓冲区，检测触发条件，完成后故障采集
        /// 调用方按所需采样率调用此方法（正常10Hz，高速1kHz）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="dataPoint">设备数据点</param>
        public void FeedDataPoint(string deviceId, DeviceDataPoint dataPoint)
        {
            if (string.IsNullOrEmpty(deviceId) || dataPoint == null)
                return;

            FaultTriggerConfig config;
            PostFaultCollectionState postFaultState;

            lock (_syncRoot)
            {
                if (!_configs.TryGetValue(deviceId, out config) || !config.Enabled)
                    return;

                _activePostFaultCollectors.TryGetValue(deviceId, out postFaultState);
            }

            if (postFaultState != null)
            {
                CollectPostFaultSample(postFaultState, dataPoint);
            }
            else
            {
                FeedPreFaultBuffer(deviceId, config, dataPoint);
                CheckTriggerCondition(deviceId, config, dataPoint);
            }
        }

        #endregion

        #region -- IFaultRecordService 实现：故障录波管理 --

        public bool EnableFaultRecording(string deviceId, FaultTriggerConfig config)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                bool isHighSpeed = config.SampleRateHz >= 1000.0;
                int preFaultSamples = isHighSpeed ? 5000 : 1000;

                lock (_syncRoot)
                {
                    _configs[deviceId] = config;

                    foreach (var channel in config.RecordChannels)
                    {
                        string bufferKey = $"{deviceId}_{channel}";
                        if (!_preFaultBuffers.ContainsKey(bufferKey))
                        {
                            _preFaultBuffers[bufferKey] = new CircularBuffer<double>(preFaultSamples);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[FaultRecordService] 故障录波已启用: {deviceId}, " +
                    $"触发通道: {config.TriggerChannel}, " +
                    $"采样率: {config.SampleRateHz}Hz, " +
                    $"预故障样点: {preFaultSamples}");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 启用故障录波失败: {ex.Message}");
                return false;
            }
        }

        public bool DisableFaultRecording(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return false;

            lock (_syncRoot)
            {
                _configs.Remove(deviceId);

                var bufferKeys = _preFaultBuffers.Keys
                    .Where(k => k.StartsWith(deviceId + "_"))
                    .ToList();
                foreach (var key in bufferKeys)
                    _preFaultBuffers.Remove(key);

                _activePostFaultCollectors.Remove(deviceId);
            }

            System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 故障录波已禁用: {deviceId}");
            return true;
        }

        #endregion

        #region -- IFaultRecordService 实现：数据获取 --

        public Dictionary<string, double[]> GetWaveformData(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
                return new Dictionary<string, double[]>();

            try
            {
                var waveforms = _faultRepository.GetWaveformByEventId(eventId);
                var result = new Dictionary<string, double[]>();

                foreach (var wf in waveforms)
                {
                    if (wf.DataBlob != null && wf.DataBlob.Length > 0)
                    {
                        result[wf.ChannelName] = DeserializeDoubleArray(wf.DataBlob);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 获取波形数据失败: {ex.Message}");
                return new Dictionary<string, double[]>();
            }
        }

        public List<FaultEvent> ListFaultEvents(string deviceId, long startTime, long endTime, string faultLevel = null)
        {
            if (string.IsNullOrEmpty(deviceId))
                return new List<FaultEvent>();

            try
            {
                return _faultRepository.GetFaultEvents(deviceId, startTime, endTime, faultLevel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 查询故障事件失败: {ex.Message}");
                return new List<FaultEvent>();
            }
        }

        public string ExportWaveform(string eventId, string format = "csv")
        {
            if (string.IsNullOrEmpty(eventId))
                return null;

            var waveformData = GetWaveformData(eventId);
            if (waveformData.Count == 0)
                return null;

            try
            {
                string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "waveforms");
                if (!Directory.Exists(exportDir))
                    Directory.CreateDirectory(exportDir);

                string filePath = Path.Combine(exportDir, $"fault_waveform_{eventId}.{format.ToLowerInvariant()}");

                if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    ExportToCsv(filePath, waveformData);
                }
                else if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                {
                    ExportToJson(filePath, waveformData);
                }
                else
                {
                    return null;
                }

                _faultRepository.MarkExported(eventId);

                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 波形导出: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 波形导出失败: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region -- 预故障缓冲 --

        /// <summary>
        /// 将数据点写入对应通道的预故障循环缓冲区
        /// </summary>
        private void FeedPreFaultBuffer(string deviceId, FaultTriggerConfig config, DeviceDataPoint dataPoint)
        {
            lock (_syncRoot)
            {
                foreach (var channel in config.RecordChannels)
                {
                    double value = GetChannelValue(dataPoint, channel);
                    string bufferKey = $"{deviceId}_{channel}";
                    if (_preFaultBuffers.TryGetValue(bufferKey, out var buffer))
                    {
                        buffer.Write(value);
                    }
                }
            }
        }

        #endregion

        #region -- 触发检测 --

        /// <summary>
        /// 检测故障触发条件
        /// 根据配置的触发模式（absolute/rising/falling）和阈值判断是否触发录波
        /// </summary>
        private void CheckTriggerCondition(string deviceId, FaultTriggerConfig config, DeviceDataPoint dataPoint)
        {
            double triggerValue = GetChannelValue(dataPoint, config.TriggerChannel);

            bool triggered = false;
            string faultLevel;

            switch (config.TriggerMode?.ToLowerInvariant())
            {
                case "absolute":
                    double absValue = Math.Abs(triggerValue);
                    triggered = absValue >= config.TriggerThreshold;
                    if (absValue >= config.TriggerThreshold * 2.0)
                        faultLevel = "CRITICAL";
                    else if (absValue >= config.TriggerThreshold * 1.5)
                        faultLevel = "MAJOR";
                    else if (absValue >= config.TriggerThreshold)
                        faultLevel = "MINOR";
                    else
                        faultLevel = "MINOR";
                    break;

                case "rising":
                    triggered = triggerValue >= config.TriggerThreshold;
                    faultLevel = triggerValue >= config.TriggerThreshold * 1.5 ? "MAJOR" : "MINOR";
                    break;

                case "falling":
                    triggered = triggerValue <= config.TriggerThreshold;
                    faultLevel = triggerValue <= config.TriggerThreshold * 0.5 ? "MAJOR" : "MINOR";
                    break;

                default:
                    return;
            }

            if (!triggered)
                return;

            lock (_syncRoot)
            {
                if (_activePostFaultCollectors.ContainsKey(deviceId))
                    return;
            }

            StartFaultRecording(deviceId, config, triggerValue, faultLevel, dataPoint.Timestamp);
        }

        /// <summary>
        /// 启动故障录波
        /// 快照预故障缓冲，创建后故障采集状态
        /// </summary>
        private void StartFaultRecording(string deviceId, FaultTriggerConfig config, double triggerValue, string faultLevel, long timestamp)
        {
            bool isHighSpeed = config.SampleRateHz >= 1000.0;
            int preFaultSamples = isHighSpeed ? 5000 : 1000;
            int postFaultSamples = isHighSpeed ? 10000 : 2000;

            string eventId = CryptoHelper.GenerateUuid();

            var preFaultData = new Dictionary<string, double[]>();
            lock (_syncRoot)
            {
                foreach (var channel in config.RecordChannels)
                {
                    string bufferKey = $"{deviceId}_{channel}";
                    if (_preFaultBuffers.TryGetValue(bufferKey, out var buffer))
                    {
                        var allSamples = buffer.ReadAll();
                        if (allSamples.Count > preFaultSamples)
                            allSamples = allSamples.Skip(allSamples.Count - preFaultSamples).ToList();

                        preFaultData[channel] = allSamples.ToArray();
                    }
                    else
                    {
                        preFaultData[channel] = Array.Empty<double>();
                    }
                }
            }

            var faultEvent = new FaultEvent
            {
                EventId = eventId,
                DeviceId = deviceId,
                FaultCode = $"FLT_{config.TriggerChannel.ToUpperInvariant()}",
                FaultLevel = faultLevel,
                FaultDescription = $"[故障录波] {config.TriggerChannel} 触发: 当前值={triggerValue:F2}, 阈值={config.TriggerThreshold:F2}",
                TriggeredAt = timestamp,
                TriggerChannel = config.TriggerChannel,
                TriggerValue = triggerValue,
                PreFaultSamples = preFaultSamples,
                PostFaultSamples = postFaultSamples,
                SampleRateHz = config.SampleRateHz
            };

            var postFaultData = new Dictionary<string, List<double>>();
            foreach (var channel in config.RecordChannels)
            {
                postFaultData[channel] = new List<double>(postFaultSamples);
            }

            var state = new PostFaultCollectionState
            {
                EventId = eventId,
                DeviceId = deviceId,
                Config = config,
                FaultEvent = faultEvent,
                PreFaultData = preFaultData,
                PostFaultData = postFaultData,
                PostFaultSamplesTotal = postFaultSamples,
                PostFaultSamplesCollected = 0
            };

            lock (_syncRoot)
            {
                _activePostFaultCollectors[deviceId] = state;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[FaultRecordService] 故障触发开始: {eventId}, " +
                $"设备: {deviceId}, 等级: {faultLevel}, " +
                $"预故障: {preFaultSamples}点, 后故障: {postFaultSamples}点");
        }

        #endregion

        #region -- 后故障数据采集 --

        /// <summary>
        /// 收集后故障采样点
        /// 当采集达到目标样点数时，完成故障事件持久化
        /// </summary>
        private void CollectPostFaultSample(PostFaultCollectionState state, DeviceDataPoint dataPoint)
        {
            lock (_syncRoot)
            {
                foreach (var channel in state.Config.RecordChannels)
                {
                    double value = GetChannelValue(dataPoint, channel);
                    if (state.PostFaultData.TryGetValue(channel, out var list))
                    {
                        list.Add(value);
                    }
                }

                state.PostFaultSamplesCollected++;

                if (state.PostFaultSamplesCollected >= state.PostFaultSamplesTotal)
                {
                    _activePostFaultCollectors.Remove(state.DeviceId);
                }
                else
                {
                    return;
                }
            }

            FinalizeFaultRecording(state);
        }

        /// <summary>
        /// 完成故障录波
        /// 组装完整波形数据，持久化到 SQLite，触发通知事件
        /// </summary>
        private void FinalizeFaultRecording(PostFaultCollectionState state)
        {
            try
            {
                var fullWaveform = new Dictionary<string, double[]>();
                foreach (var channel in state.Config.RecordChannels)
                {
                    var preData = state.PreFaultData.TryGetValue(channel, out var pre) ? pre : Array.Empty<double>();
                    var postData = state.PostFaultData.TryGetValue(channel, out var post) ? post.ToArray() : Array.Empty<double>();

                    fullWaveform[channel] = preData.Concat(postData).ToArray();
                }

                _faultRepository.InsertFaultEvent(state.FaultEvent);

                int channelIndex = 0;
                foreach (var channel in state.Config.RecordChannels)
                {
                    if (fullWaveform.TryGetValue(channel, out var data) && data.Length > 0)
                    {
                        var waveform = new FaultWaveform
                        {
                            WaveformId = CryptoHelper.GenerateUuid(),
                            EventId = state.EventId,
                            ChannelIndex = channelIndex,
                            ChannelName = channel,
                            DataBlob = SerializeDoubleArray(data),
                            DataSize = data.Length,
                            Unit = GetChannelUnit(channel)
                        };

                        _faultRepository.InsertFaultWaveform(waveform);
                        channelIndex++;
                    }
                }

                state.FaultEvent.WaveformDataPath =
                    Path.Combine("Data", "waveforms", $"fault_waveform_{state.EventId}.csv");

                OnFaultOccurred?.Invoke(state.FaultEvent);

                System.Diagnostics.Debug.WriteLine(
                    $"[FaultRecordService] 故障录波完成: {state.EventId}, " +
                    $"通道数: {channelIndex}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 故障录波持久化失败: {ex.Message}");
            }
        }

        #endregion

        #region -- 通道工具方法 --

        private static double GetChannelValue(DeviceDataPoint dataPoint, string channelName)
        {
            if (dataPoint == null || string.IsNullOrEmpty(channelName))
                return 0;

            switch (channelName.ToLowerInvariant())
            {
                case "dc_voltage":
                case "voltage":
                    return dataPoint.Voltage;
                case "dc_current":
                case "current":
                    return dataPoint.Current;
                case "power":
                    return dataPoint.Power;
                case "temperature":
                    return dataPoint.Temperature;
                case "soc":
                    return dataPoint.Soc;
                case "soh":
                    return dataPoint.Soh;
                case "cell_max_voltage":
                    return dataPoint.CellMaxVoltage;
                case "cell_min_voltage":
                    return dataPoint.CellMinVoltage;
                case "cell_max_temp":
                case "cell_max_temperature":
                    return dataPoint.CellMaxTemperature;
                case "cell_min_temp":
                case "cell_min_temperature":
                    return dataPoint.CellMinTemperature;
                default:
                    return dataPoint.ExtraParams != null &&
                           dataPoint.ExtraParams.TryGetValue(channelName, out var val)
                        ? val
                        : 0;
            }
        }

        private static string GetChannelUnit(string channelName)
        {
            if (string.IsNullOrEmpty(channelName))
                return string.Empty;

            switch (channelName.ToLowerInvariant())
            {
                case "dc_voltage":
                case "voltage":
                case "cell_max_voltage":
                case "cell_min_voltage":
                    return "V";
                case "dc_current":
                case "current":
                    return "A";
                case "power":
                    return "W";
                case "temperature":
                case "cell_max_temp":
                case "cell_max_temperature":
                case "cell_min_temp":
                case "cell_min_temperature":
                    return "°C";
                case "soc":
                case "soh":
                    return "%";
                default:
                    return string.Empty;
            }
        }

        #endregion

        #region -- 波形序列化/反序列化 --

        /// <summary>
        /// 将 double 数组序列化为字节数组
        /// 使用 Buffer.BlockCopy 实现高效转换
        /// </summary>
        private static byte[] SerializeDoubleArray(double[] data)
        {
            if (data == null || data.Length == 0)
                return Array.Empty<byte>();

            byte[] bytes = new byte[data.Length * sizeof(double)];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// 将字节数组反序列化为 double 数组
        /// </summary>
        private static double[] DeserializeDoubleArray(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
                return Array.Empty<double>();

            double[] data = new double[blob.Length / sizeof(double)];
            Buffer.BlockCopy(blob, 0, data, 0, blob.Length);
            return data;
        }

        #endregion

        #region -- 波形导出 --

        private static void ExportToCsv(string filePath, Dictionary<string, double[]> waveformData)
        {
            var sb = new StringBuilder();
            var channelNames = waveformData.Keys.ToList();

            sb.AppendLine(string.Join(",", channelNames.Select(c => $"\"{c}\"")));

            int maxLength = waveformData.Values.Max(arr => arr.Length);
            for (int i = 0; i < maxLength; i++)
            {
                var rowValues = channelNames.Select(c =>
                {
                    var arr = waveformData[c];
                    return i < arr.Length ? arr[i].ToString("F6") : string.Empty;
                });
                sb.AppendLine(string.Join(",", rowValues));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static void ExportToJson(string filePath, Dictionary<string, double[]> waveformData)
        {
            var jsonObj = new Dictionary<string, List<double>>();
            foreach (var kvp in waveformData)
            {
                jsonObj[kvp.Key] = new List<double>(kvp.Value);
            }

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        #endregion

        #region -- IDisposable 实现 --

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            lock (_syncRoot)
            {
                _configs.Clear();
                _preFaultBuffers.Clear();
                _activePostFaultCollectors.Clear();
            }
        }

        #endregion
    }
}
