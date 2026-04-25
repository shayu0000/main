using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 故障录波服务实现类
// 说明: 实现设备故障的自动检测和波形数据记录
//       使用循环缓冲区存储故障前后数据，支持双采样率模式
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 故障录波服务实现类
    /// 核心功能：
    ///   1. 循环缓冲区：使用CircularBuffer存储固定时长的高速采样数据
    ///   2. 双采样模式：
    ///      - 正常采样(10Hz): 日常监控使用，节省存储
    ///      - 高速采样(1kHz): 故障触发时切换，保证波形细节
    ///   3. 故障触发：当检测到参数超过触发阈值时，自动保存缓冲区中的波形数据
    ///   4. 波形导出：将录波数据导出为CSV或JSON格式文件
    /// </summary>
    public class FaultRecordService : IFaultRecordService, IDisposable
    {
        #region -- 字段定义 --

        /// <summary>CAN通信服务引用</summary>
        private readonly ICanCommunicationService _canService;

        /// <summary>设备监控服务引用</summary>
        private readonly IDeviceMonitorService _monitorService;

        /// <summary>各设备的故障录波配置字典（Key: DeviceId）</summary>
        private readonly Dictionary<string, FaultTriggerConfig> _configs = new Dictionary<string, FaultTriggerConfig>();

        /// <summary>各设备的循环缓冲区字典（Key: "DeviceId_ChannelName"）</summary>
        private readonly Dictionary<string, CircularBuffer<double>> _circularBuffers = new Dictionary<string, CircularBuffer<double>>();

        /// <summary>已记录的故障事件列表</summary>
        private readonly List<FaultEvent> _faultEvents = new List<FaultEvent>();

        /// <summary>波形数据存储字典（Key: EventId, Value: 波形数据）</summary>
        private readonly Dictionary<string, Dictionary<string, double[]>> _waveformData = new Dictionary<string, Dictionary<string, double[]>>();

        /// <summary>数据锁</summary>
        private readonly object _dataLock = new object();

        /// <summary>对象销毁标志</summary>
        private bool _isDisposed;

        /// <summary>高速采样定时器</summary>
        private Timer _highSpeedTimer;

        #endregion

        #region -- 事件声明 --

        /// <summary>故障发生事件</summary>
        public event Action<FaultEvent> OnFaultOccurred;

        #endregion

        /// <summary>
        /// 构造故障录波服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务</param>
        /// <param name="monitorService">设备监控服务</param>
        public FaultRecordService(ICanCommunicationService canService, IDeviceMonitorService monitorService)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
        }

        #region -- 故障录波管理 --

        /// <summary>
        /// 启用指定设备的故障录波功能
        /// 根据配置创建循环缓冲区并订阅数据更新事件
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="config">故障触发配置</param>
        /// <returns>启用是否成功</returns>
        public bool EnableFaultRecording(string deviceId, FaultTriggerConfig config)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                lock (_dataLock)
                {
                    _configs[deviceId] = config;

                    // 为每个录波通道创建循环缓冲区
                    int bufferSize = (int)((config.PreFaultDurationS + config.PostFaultDurationS) * config.SampleRateHz);
                    foreach (var channel in config.RecordChannels)
                    {
                        string bufferKey = $"{deviceId}_{channel}";
                        _circularBuffers[bufferKey] = new CircularBuffer<double>(bufferSize);
                    }
                }

                // 启动高速采样定时器
                if (_highSpeedTimer == null)
                {
                    int intervalMs = (int)(1000.0 / 1000); // 1kHz = 1ms
                    _highSpeedTimer = new Timer(OnHighSpeedSampleTick, null, 0, intervalMs);
                }

                // 订阅CAN消息事件用于实时数据采集
                _canService.OnCanMessageReceived += HandleCanMessageForFault;

                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 故障录波已启用: {deviceId}, 触发通道: {config.TriggerChannel}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 启用故障录波失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 禁用指定设备的故障录波功能
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>禁用是否成功</returns>
        public bool DisableFaultRecording(string deviceId)
        {
            lock (_dataLock)
            {
                _configs.Remove(deviceId);

                // 清理该设备的循环缓冲区
                var keysToRemove = _circularBuffers.Keys.Where(k => k.StartsWith(deviceId + "_")).ToList();
                foreach (var key in keysToRemove)
                    _circularBuffers.Remove(key);
            }

            _canService.OnCanMessageReceived -= HandleCanMessageForFault;

            System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 故障录波已禁用: {deviceId}");
            return true;
        }

        #endregion

        #region -- 数据获取 --

        /// <summary>
        /// 获取故障事件的波形数据
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <returns>波形数据字典</returns>
        public Dictionary<string, double[]> GetWaveformData(string eventId)
        {
            lock (_dataLock)
            {
                return _waveformData.TryGetValue(eventId, out var data) ? data : new Dictionary<string, double[]>();
            }
        }

        /// <summary>
        /// 查询故障事件列表
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="startTime">起始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <param name="faultLevel">故障等级过滤</param>
        /// <returns>故障事件列表</returns>
        public List<FaultEvent> ListFaultEvents(string deviceId, long startTime, long endTime, string faultLevel = null)
        {
            lock (_dataLock)
            {
                var query = _faultEvents.Where(e => e.DeviceId == deviceId
                    && e.TriggeredAt >= startTime
                    && e.TriggeredAt <= endTime);

                if (!string.IsNullOrEmpty(faultLevel))
                    query = query.Where(e => e.FaultLevel == faultLevel);

                return query.ToList();
            }
        }

        /// <summary>
        /// 导出故障波形数据为文件
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <param name="format">导出格式</param>
        /// <returns>导出文件路径</returns>
        public string ExportWaveform(string eventId, string format = "csv")
        {
            var waveformData = GetWaveformData(eventId);
            if (waveformData.Count == 0) return null;

            string exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "waveforms");
            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            string filePath = Path.Combine(exportDir, $"fault_waveform_{eventId}.{format}");

            if (format.ToLower() == "csv")
            {
                ExportToCsv(filePath, waveformData);
            }
            else if (format.ToLower() == "json")
            {
                ExportToJson(filePath, waveformData);
            }

            System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 波形导出: {filePath}");
            return filePath;
        }

        #endregion

        #region -- 实时数据采集与触发检测 --

        /// <summary>
        /// 处理CAN消息用于故障录波数据采集
        /// 将测量数据写入循环缓冲区并执行故障触发检测
        /// </summary>
        /// <param name="message">CAN消息</param>
        private void HandleCanMessageForFault(CanMessage message)
        {
            // TODO: 解析CAN消息中的设备ID和通道数据
            // 将数据写入对应通道的循环缓冲区
            // 执行故障触发判断
        }

        /// <summary>
        /// 高速采样定时器回调
        /// 按1kHz频率从设备读取数据并存入循环缓冲区
        /// </summary>
        private void OnHighSpeedSampleTick(object state)
        {
            lock (_dataLock)
            {
                foreach (var kvp in _configs)
                {
                    string deviceId = kvp.Key;
                    var config = kvp.Value;
                    if (!config.Enabled) continue;

                    // 从监控服务获取最新数据
                    var dataPoint = _monitorService.GetLatestData(deviceId);
                    if (dataPoint == null) continue;

                    // 将数据写入各通道的循环缓冲区
                    foreach (var channel in config.RecordChannels)
                    {
                        double value = GetChannelValue(dataPoint, channel);
                        string bufferKey = $"{deviceId}_{channel}";
                        if (_circularBuffers.TryGetValue(bufferKey, out var buffer))
                        {
                            buffer.Write(value);
                        }
                    }

                    // 检测触发条件
                    double triggerValue = GetChannelValue(dataPoint, config.TriggerChannel);
                    CheckFaultTrigger(deviceId, config, triggerValue, dataPoint.Timestamp);
                }
            }
        }

        /// <summary>
        /// 从设备数据点中提取指定通道的值
        /// </summary>
        private double GetChannelValue(DeviceDataPoint dataPoint, string channelName)
        {
            return channelName switch
            {
                "dc_voltage" => dataPoint.Voltage,
                "dc_current" => dataPoint.Current,
                "temperature" => dataPoint.Temperature,
                "soc" => dataPoint.Soc,
                "cell_max_voltage" => dataPoint.CellMaxVoltage,
                "cell_min_voltage" => dataPoint.CellMinVoltage,
                "cell_max_temp" => dataPoint.CellMaxTemperature,
                "cell_min_temp" => dataPoint.CellMinTemperature,
                _ => 0
            };
        }

        /// <summary>
        /// 检查是否触发故障录波
        /// 当通道值超过配置的触发阈值时，触发故障事件并保存波形数据
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="config">故障触发配置</param>
        /// <param name="triggerValue">触发通道当前值</param>
        /// <param name="timestamp">当前时间戳</param>
        private void CheckFaultTrigger(string deviceId, FaultTriggerConfig config, double triggerValue, long timestamp)
        {
            bool triggered = false;
            string faultLevel = "MINOR";

            switch (config.TriggerMode)
            {
                case "absolute":
                    triggered = Math.Abs(triggerValue) >= config.TriggerThreshold;
                    faultLevel = Math.Abs(triggerValue) >= config.TriggerThreshold * 1.5 ? "CRITICAL" : "MAJOR";
                    break;
                case "rising":
                    triggered = triggerValue >= config.TriggerThreshold;
                    break;
                case "falling":
                    triggered = triggerValue <= config.TriggerThreshold;
                    break;
            }

            if (!triggered) return;

            // 创建故障事件
            string eventId = CryptoHelper.GenerateUuid();
            var faultEvent = new FaultEvent
            {
                EventId = eventId,
                DeviceId = deviceId,
                FaultCode = $"FLT_{config.TriggerChannel.ToUpper()}",
                FaultLevel = faultLevel,
                FaultDescription = $"[故障录波] {config.TriggerChannel} 触发: 当前值={triggerValue:F2}, 阈值={config.TriggerThreshold:F2}",
                TriggeredAt = timestamp,
                TriggerChannel = config.TriggerChannel,
                TriggerValue = triggerValue,
                PreFaultSamples = (int)(config.PreFaultDurationS * config.SampleRateHz),
                PostFaultSamples = (int)(config.PostFaultDurationS * config.SampleRateHz),
                SampleRateHz = config.SampleRateHz
            };

            // 保存波形数据
            SaveWaveformData(eventId, deviceId, config);

            lock (_dataLock)
            {
                _faultEvents.Add(faultEvent);
                faultEvent.WaveformDataPath = ExportWaveform(eventId, "csv");
            }

            // 触发故障事件通知
            OnFaultOccurred?.Invoke(faultEvent);

            System.Diagnostics.Debug.WriteLine($"[FaultRecordService] 故障触发: {faultEvent.FaultDescription}");
        }

        /// <summary>
        /// 保存故障波形数据
        /// 从循环缓冲区中提取故障前后的数据并保存
        /// </summary>
        private void SaveWaveformData(string eventId, string deviceId, FaultTriggerConfig config)
        {
            var waveformData = new Dictionary<string, double[]>();

            foreach (var channel in config.RecordChannels)
            {
                string bufferKey = $"{deviceId}_{channel}";
                if (_circularBuffers.TryGetValue(bufferKey, out var buffer))
                {
                    waveformData[channel] = buffer.ReadAll().ToArray();
                }
            }

            lock (_dataLock)
            {
                _waveformData[eventId] = waveformData;
            }
        }

        #endregion

        #region -- 波形导出 --

        /// <summary>
        /// 导出波形数据为CSV格式
        /// </summary>
        private void ExportToCsv(string filePath, Dictionary<string, double[]> waveformData)
        {
            var sb = new StringBuilder();

            // 表头
            var channelNames = waveformData.Keys.ToList();
            sb.AppendLine(string.Join(",", channelNames.Select(c => $"\"{c}\"")));

            // 数据行（按最长的通道对齐）
            int maxLength = waveformData.Values.Max(arr => arr.Length);
            for (int i = 0; i < maxLength; i++)
            {
                var rowData = channelNames.Select(c =>
                    i < waveformData[c].Length ? waveformData[c][i].ToString("F3") : "");
                sb.AppendLine(string.Join(",", rowData));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 导出波形数据为JSON格式
        /// </summary>
        private void ExportToJson(string filePath, Dictionary<string, double[]> waveformData)
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

        /// <summary>
        /// 释放故障录波服务占用的所有资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _highSpeedTimer?.Dispose();
            _canService.OnCanMessageReceived -= HandleCanMessageForFault;
        }

        #endregion
    }
}
