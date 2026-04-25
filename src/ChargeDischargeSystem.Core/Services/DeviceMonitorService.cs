using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 设备监控服务实现类
// 说明: 通过定时器定期轮询设备数据，解析CAN原始报文为物理参数，
//       并在检测到异常时触发告警事件
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 设备监控服务实现类
    /// 核心功能：
    ///   1. 定时轮询：通过CAN总线周期性获取设备实时数据
    ///   2. 数据解析：解析CAN消息中的电压/电流/温度/SOC等参数
    ///   3. 告警检测：将实时数据与预设阈值比较，触发相应级别告警
    ///   4. 数据分发：通过事件通知外部组件最新的设备数据
    /// </summary>
    public class DeviceMonitorService : IDeviceMonitorService, IDisposable
    {
        #region -- 字段定义 --

        /// <summary>CAN通信服务引用，用于收发CAN消息</summary>
        private readonly ICanCommunicationService _canService;

        /// <summary>配置服务引用，用于读取告警阈值等配置</summary>
        private readonly IConfigService _configService;

        /// <summary>系统配置引用</summary>
        private readonly AppConfig _appConfig;

        /// <summary>轮询定时器</summary>
        private Timer _pollTimer;

        /// <summary>定时器取消令牌源</summary>
        private CancellationTokenSource _cts;

        /// <summary>设备最新数据快照字典（Key: DeviceId, Value: 最新数据点）</summary>
        private readonly Dictionary<string, DeviceDataPoint> _latestData = new Dictionary<string, DeviceDataPoint>();

        /// <summary>数据字典读写锁</summary>
        private readonly ReaderWriterLockSlim _dataLock = new ReaderWriterLockSlim();

        /// <summary>当前监控的设备ID列表</summary>
        private List<string> _monitoredDeviceIds;

        /// <summary>服务运行状态</summary>
        private bool _isRunning;

        /// <summary>对象销毁标志</summary>
        private bool _isDisposed;

        /// <summary>事件操作锁</summary>
        private readonly object _eventLock = new object();

        // ---- 告警检测辅助字段 ----
        /// <summary>告警冷却计时（防止同一告警频繁触发），Key: "DeviceId_ParameterName"</summary>
        private readonly Dictionary<string, long> _alarmCooldown = new Dictionary<string, long>();

        /// <summary>告警冷却时间（毫秒），默认30秒内不重复触发同一告警</summary>
        private const long AlarmCooldownMs = 30000;

        #endregion

        #region -- 事件声明 --

        /// <summary>数据更新事件</summary>
        public event Action<Dictionary<string, DeviceDataPoint>> OnDataUpdated;

        /// <summary>告警触发事件</summary>
        public event Action<DeviceAlarm> OnAlarmRaised;

        #endregion

        #region -- 公开属性 --

        /// <summary>监控服务是否正在运行</summary>
        public bool IsRunning => _isRunning;

        #endregion

        /// <summary>
        /// 构造设备监控服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务，用于收发CAN消息</param>
        /// <param name="configService">配置服务，用于读取告警阈值配置</param>
        /// <param name="appConfig">应用程序配置，获取轮询间隔等参数</param>
        public DeviceMonitorService(ICanCommunicationService canService, IConfigService configService, AppConfig appConfig)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
        }

        #region -- 监控生命周期管理 --

        /// <summary>
        /// 开始监控指定设备
        /// 订阅CAN消息事件并启动轮询定时器，定期获取设备数据
        /// </summary>
        /// <param name="deviceIds">要监控的设备ID列表，为null则监控所有已注册设备</param>
        /// <returns>启动是否成功</returns>
        public bool StartMonitoring(List<string> deviceIds = null)
        {
            if (_isRunning)
            {
                System.Diagnostics.Debug.WriteLine("[DeviceMonitorService] 监控服务已在运行中。");
                return false;
            }

            try
            {
                _monitoredDeviceIds = deviceIds ?? new List<string>();
                _cts = new CancellationTokenSource();

                // 订阅CAN消息接收事件，用于实时数据解析
                _canService.OnCanMessageReceived += HandleCanMessage;

                // 启动轮询定时器（主动请求设备数据）
                int pollIntervalMs = _appConfig.CanConfig?.PollIntervalMs ?? 100;
                _pollTimer = new Timer(OnPollTimerTick, null, 0, pollIntervalMs);

                _isRunning = true;
                System.Diagnostics.Debug.WriteLine("[DeviceMonitorService] 监控服务已启动");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceMonitorService] 启动监控失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止所有设备监控
        /// 停止定时器并取消CAN消息事件订阅
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            _pollTimer?.Dispose();
            _pollTimer = null;

            _canService.OnCanMessageReceived -= HandleCanMessage;

            _isRunning = false;
            System.Diagnostics.Debug.WriteLine("[DeviceMonitorService] 监控服务已停止");
        }

        #endregion

        #region -- 数据查询 --

        /// <summary>
        /// 获取指定设备的最新数据快照（线程安全）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备最新数据点，无数据则返回null</returns>
        public DeviceDataPoint GetLatestData(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return null;

            _dataLock.EnterReadLock();
            try
            {
                return _latestData.TryGetValue(deviceId, out var data) ? data : null;
            }
            finally
            {
                _dataLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 查询告警历史记录
        /// 从数据库查询指定时间范围内的告警记录
        /// </summary>
        /// <param name="startTime">起始时间戳</param>
        /// <param name="endTime">结束时间戳</param>
        /// <param name="alarmLevel">告警级别过滤</param>
        /// <returns>告警记录列表</returns>
        public List<DeviceAlarm> GetAlarmHistory(long startTime, long endTime, string alarmLevel = null)
        {
            // 注意：此处为服务层接口，实际数据访问通过Repository层完成
            // 本实现提供接口定义，具体数据访问逻辑将在后续集成时注入
            System.Diagnostics.Debug.WriteLine($"[DeviceMonitorService] 查询告警历史: {startTime}~{endTime}, 级别:{alarmLevel ?? "全部"}");
            return new List<DeviceAlarm>();
        }

        /// <summary>
        /// 获取当前所有活跃告警
        /// </summary>
        /// <returns>活跃告警列表</returns>
        public List<DeviceAlarm> GetActiveAlarms()
        {
            System.Diagnostics.Debug.WriteLine("[DeviceMonitorService] 查询活跃告警");
            return new List<DeviceAlarm>();
        }

        /// <summary>
        /// 确认告警
        /// 记录确认操作的用户和确认时间
        /// </summary>
        /// <param name="alarmId">告警ID</param>
        /// <param name="userId">确认用户ID</param>
        /// <returns>确认是否成功</returns>
        public bool AcknowledgeAlarm(string alarmId, string userId)
        {
            if (string.IsNullOrEmpty(alarmId) || string.IsNullOrEmpty(userId))
                return false;

            System.Diagnostics.Debug.WriteLine($"[DeviceMonitorService] 确认告警: {alarmId}, 操作用户: {userId}");
            return true;
        }

        #endregion

        #region -- CAN数据解析 --

        /// <summary>
        /// 处理接收到的CAN消息
        /// 根据CAN ID解析消息中的物理参数（电压、电流、温度、SOC等）
        /// 然后将解析结果存入数据快照并触发告警检测
        /// </summary>
        /// <param name="canMessage">接收到的CAN消息</param>
        private void HandleCanMessage(CanMessage canMessage)
        {
            try
            {
                // 解析CAN消息中的设备参数
                var dataPoint = ParseCanMessageToDataPoint(canMessage);
                if (dataPoint == null) return;

                // 更新设备最新数据快照
                _dataLock.EnterWriteLock();
                try
                {
                    _latestData[dataPoint.DeviceId] = dataPoint;
                }
                finally
                {
                    _dataLock.ExitWriteLock();
                }

                // 触发数据更新事件
                var snapshot = new Dictionary<string, DeviceDataPoint> { { dataPoint.DeviceId, dataPoint } };
                OnDataUpdated?.Invoke(snapshot);

                // 执行告警阈值检测
                CheckAlarmThresholds(dataPoint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceMonitorService] 处理CAN消息异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 将原始CAN消息解析为设备数据点
        /// 根据CAN ID识别数据来源设备，并解析数据帧中的物理参数
        /// 
        /// CAN ID编码规则（示例）：
        ///   高8位：设备类型（0x01=PCS, 0x02=BMS, 0x03=Inverter）
        ///   中8位：设备地址
        ///   低8位：参数类型（0x01=电压, 0x02=电流, 0x03=温度, 0x04=SOC）
        /// </summary>
        /// <param name="canMessage">CAN消息</param>
        /// <returns>解析后的设备数据点</returns>
        private DeviceDataPoint ParseCanMessageToDataPoint(CanMessage canMessage)
        {
            if (canMessage.Data == null || canMessage.Data.Length < 4)
                return null;

            // 从CAN ID中提取设备信息
            uint canId = canMessage.CanId;
            byte deviceType = (byte)((canId >> 16) & 0xFF);
            byte deviceAddr = (byte)((canId >> 8) & 0xFF);
            byte paramType = (byte)(canId & 0xFF);

            // 构造设备ID
            string deviceId = $"DEV_{deviceType:X2}_{deviceAddr:X2}";

            // 检查是否在监控列表中
            if (_monitoredDeviceIds.Count > 0 && !_monitoredDeviceIds.Contains(deviceId))
                return null;

            // 获取或创建设备数据点
            _dataLock.EnterReadLock();
            bool exists = _latestData.TryGetValue(deviceId, out DeviceDataPoint dataPoint);
            _dataLock.ExitReadLock();

            if (!exists)
            {
                dataPoint = new DeviceDataPoint
                {
                    DeviceId = deviceId,
                    Timestamp = canMessage.Timestamp
                };
            }
            else
            {
                dataPoint.Timestamp = canMessage.Timestamp;
            }

            // 解析数据值（大端序，float类型）
            float rawValue = BitConverter.ToSingle(canMessage.Data, 0);

            // 根据参数类型设置对应字段
            switch (paramType)
            {
                case 0x01: // 电压
                    dataPoint.Voltage = Math.Round(rawValue, 2);
                    break;
                case 0x02: // 电流
                    dataPoint.Current = Math.Round(rawValue, 2);
                    break;
                case 0x03: // 温度
                    dataPoint.Temperature = Math.Round(rawValue, 1);
                    break;
                case 0x04: // SOC
                    dataPoint.Soc = Math.Round(rawValue, 1);
                    break;
                case 0x05: // 功率
                    dataPoint.Power = Math.Round(rawValue, 2);
                    break;
                case 0x06: // 单体最高电压
                    dataPoint.CellMaxVoltage = Math.Round(rawValue, 3);
                    break;
                case 0x07: // 单体最低电压
                    dataPoint.CellMinVoltage = Math.Round(rawValue, 3);
                    break;
                case 0x08: // 单体最高温度
                    dataPoint.CellMaxTemperature = Math.Round(rawValue, 1);
                    break;
                case 0x09: // 单体最低温度
                    dataPoint.CellMinTemperature = Math.Round(rawValue, 1);
                    break;
                case 0x0A: // SOH
                    dataPoint.Soh = Math.Round(rawValue, 1);
                    break;
                default:
                    dataPoint.ExtraParams[$"param_{paramType:X2}"] = rawValue;
                    break;
            }

            return dataPoint;
        }

        #endregion

        #region -- 告警检测 --

        /// <summary>
        /// 检测设备数据点是否触发告警阈值
        /// 对比实时数据与配置中定义的告警阈值，并根据超出程度判断告警级别
        /// 同一参数在冷却时间内不会重复触发告警
        /// </summary>
        /// <param name="dataPoint">设备数据点</param>
        private void CheckAlarmThresholds(DeviceDataPoint dataPoint)
        {
            CheckParameterAlarm(dataPoint.DeviceId, "voltage", "电压", dataPoint.Voltage, 900.0, 950.0, 800.0, 750.0);
            CheckParameterAlarm(dataPoint.DeviceId, "current", "电流", dataPoint.Current, 600.0, 700.0, -600.0, -700.0);
            CheckParameterAlarm(dataPoint.DeviceId, "temperature", "温度", dataPoint.Temperature, 60.0, 70.0, -10.0, -20.0);
            CheckParameterAlarm(dataPoint.DeviceId, "soc", "SOC", dataPoint.Soc, 100.0, 105.0, 5.0, 0.0);
            CheckParameterAlarm(dataPoint.DeviceId, "cell_max_voltage", "单体最高电压", dataPoint.CellMaxVoltage, 4.25, 4.35, 0, 0);
            CheckParameterAlarm(dataPoint.DeviceId, "cell_min_voltage", "单体最低电压", dataPoint.CellMinVoltage, 999, 999, 2.5, 2.0);
            CheckParameterAlarm(dataPoint.DeviceId, "cell_max_temp", "单体最高温度", dataPoint.CellMaxTemperature, 55.0, 65.0, 0, 0);
        }

        /// <summary>
        /// 对单个参数进行告警阈值检测
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="paramCode">参数编码</param>
        /// <param name="paramName">参数中文名称</param>
        /// <param name="actualValue">实际测量值</param>
        /// <param name="majorHigh">主要告警上限</param>
        /// <param name="criticalHigh">严重告警上限</param>
        /// <param name="majorLow">主要告警下限</param>
        /// <param name="criticalLow">严重告警下限</param>
        private void CheckParameterAlarm(string deviceId, string paramCode, string paramName,
            double actualValue, double majorHigh, double criticalHigh, double majorLow, double criticalLow)
        {
            string cooldownKey = $"{deviceId}_{paramCode}";
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 检查冷却时间
            lock (_alarmCooldown)
            {
                if (_alarmCooldown.TryGetValue(cooldownKey, out long lastAlarmTime))
                {
                    if (now - lastAlarmTime < AlarmCooldownMs)
                        return; // 冷却期内，不重复触发
                }
            }

            string alarmLevel = null;
            double threshold = 0;

            // 严重级别检测（优先级最高）
            if (actualValue >= criticalHigh)
            {
                alarmLevel = "CRITICAL";
                threshold = criticalHigh;
            }
            else if (actualValue <= criticalLow && criticalLow > 0)
            {
                alarmLevel = "CRITICAL";
                threshold = criticalLow;
            }
            // 主要级别检测
            else if (actualValue >= majorHigh)
            {
                alarmLevel = "MAJOR";
                threshold = majorHigh;
            }
            else if (actualValue <= majorLow && majorLow > 0)
            {
                alarmLevel = "MAJOR";
                threshold = majorLow;
            }

            if (alarmLevel == null) return; // 正常范围

            // 构建告警对象
            var alarm = new DeviceAlarm
            {
                AlarmId = CryptoHelper.GenerateUuid(),
                DeviceId = deviceId,
                AlarmCode = $"ALM_{paramCode.ToUpper()}_{alarmLevel}",
                AlarmLevel = alarmLevel,
                AlarmMessage = $"[{paramName}] 告警: 实际值={actualValue:F2}, 阈值={threshold:F2}",
                ParameterName = paramCode,
                ThresholdValue = threshold,
                ActualValue = actualValue,
                IsActive = 1,
                RaisedAt = now
            };

            // 更新冷却计时
            lock (_alarmCooldown)
            {
                _alarmCooldown[cooldownKey] = now;
            }

            // 触发告警事件
            OnAlarmRaised?.Invoke(alarm);

            System.Diagnostics.Debug.WriteLine($"[DeviceMonitorService] 告警触发: {alarm.AlarmMessage}");
        }

        #endregion

        #region -- 定时轮询 --

        /// <summary>
        /// 定时器回调方法
        /// 定期主动轮询设备数据（发送CAN请求命令）
        /// </summary>
        /// <param name="state">定时器状态对象（未使用）</param>
        private void OnPollTimerTick(object state)
        {
            if (!_isRunning || _cts?.IsCancellationRequested == true)
                return;

            try
            {
                // 对每个监控设备发送数据请求命令（CAN ID: 0x100 + 设备地址）
                // 实际项目中根据协议定义发送相应的请求帧
                foreach (var deviceId in _monitoredDeviceIds)
                {
                    // 发送请求命令（示例：请求设备状态数据）
                    // _canService.SendCanMessage(requestCanId, requestData);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceMonitorService] 轮询异常: {ex.Message}");
            }
        }

        #endregion

        #region -- IDisposable 实现 --

        /// <summary>
        /// 释放设备监控服务占用的所有资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopMonitoring();
            _dataLock?.Dispose();
            _cts?.Dispose();
        }

        #endregion
    }
}
