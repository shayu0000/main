using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Core.Services;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 仪表盘（设备监控）视图模型
// 说明: 实时监控所有设备状态、数据采集和告警管理
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 设备状态视图模型
    /// 用于在 UI 中展示单个设备的实时状态信息
    /// </summary>
    public partial class DeviceStatusViewModel : ObservableObject
    {
        /// <summary>设备ID</summary>
        [ObservableProperty]
        private string _deviceId = string.Empty;

        /// <summary>设备名称</summary>
        [ObservableProperty]
        private string _deviceName = string.Empty;

        /// <summary>设备状态：Online/Offline/Fault/Maintenance</summary>
        [ObservableProperty]
        private string _status = "Offline";

        /// <summary>实时电压值(V)</summary>
        [ObservableProperty]
        private double _voltage;

        /// <summary>实时电流值(A)</summary>
        [ObservableProperty]
        private double _current;

        /// <summary>实时功率值(kW)</summary>
        [ObservableProperty]
        private double _power;

        /// <summary>实时温度值(°C)</summary>
        [ObservableProperty]
        private double _temperature;

        /// <summary>荷电状态 SOC (%)</summary>
        [ObservableProperty]
        private double _soc;

        /// <summary>数据质量：GOOD/UNCERTAIN/BAD</summary>
        [ObservableProperty]
        private string _dataQuality = "GOOD";

        /// <summary>最后更新时间戳（Unix毫秒）</summary>
        [ObservableProperty]
        private long _lastUpdateTimestamp;
    }

    /// <summary>
    /// 告警视图模型
    /// 用于在 UI 中展示告警信息
    /// </summary>
    public partial class AlarmViewModel : ObservableObject
    {
        /// <summary>告警ID</summary>
        [ObservableProperty]
        private string _alarmId = string.Empty;

        /// <summary>设备ID</summary>
        [ObservableProperty]
        private string _deviceId = string.Empty;

        /// <summary>告警编码</summary>
        [ObservableProperty]
        private string _alarmCode = string.Empty;

        /// <summary>告警级别：CRITICAL/MAJOR/MINOR/WARNING</summary>
        [ObservableProperty]
        private string _alarmLevel = string.Empty;

        /// <summary>告警描述信息</summary>
        [ObservableProperty]
        private string _alarmMessage = string.Empty;

        /// <summary>触发告警的参数名称</summary>
        [ObservableProperty]
        private string _parameterName = string.Empty;

        /// <summary>告警阈值</summary>
        [ObservableProperty]
        private double _thresholdValue;

        /// <summary>实际测量值</summary>
        [ObservableProperty]
        private double _actualValue;

        /// <summary>是否仍然活跃</summary>
        [ObservableProperty]
        private bool _isActive = true;

        /// <summary>告警触发时间（Unix毫秒）</summary>
        [ObservableProperty]
        private long _raisedAt;

        /// <summary>告警确认时间（Unix毫秒）</summary>
        [ObservableProperty]
        private long? _acknowledgedAt;
    }

    /// <summary>
    /// 仪表盘视图模型
    /// 负责设备监控仪表盘的逻辑：
    ///   1. 展示所有设备的实时状态数据
    ///   2. 显示关键参数的实时数值（电压、电流、功率、温度、SOC）
    ///   3. 展示活跃告警列表
    ///   4. 订阅 IDeviceMonitorService 的数据和告警事件
    ///   5. 管理监控启停
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        #region 字段

        private readonly IDeviceMonitorService _monitorService;

        #endregion

        #region 可观察属性

        /// <summary>
        /// 设备列表（包含实时状态数据）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<DeviceStatusViewModel> _deviceList = new ObservableCollection<DeviceStatusViewModel>();

        /// <summary>
        /// 当前选中的设备
        /// </summary>
        [ObservableProperty]
        private DeviceStatusViewModel? _selectedDevice;

        /// <summary>
        /// 实时电压值(V) - 选中设备或整体汇总
        /// </summary>
        [ObservableProperty]
        private double _voltageValue;

        /// <summary>
        /// 实时电流值(A) - 选中设备或整体汇总
        /// </summary>
        [ObservableProperty]
        private double _currentValue;

        /// <summary>
        /// 实时功率值(kW) - 选中设备或整体汇总
        /// </summary>
        [ObservableProperty]
        private double _powerValue;

        /// <summary>
        /// 实时温度值(°C) - 选中设备或整体汇总
        /// </summary>
        [ObservableProperty]
        private double _temperatureValue;

        /// <summary>
        /// 实时 SOC 值(%) - 选中设备或整体汇总
        /// </summary>
        [ObservableProperty]
        private double _socValue;

        /// <summary>
        /// 活跃告警列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<AlarmViewModel> _alarmList = new ObservableCollection<AlarmViewModel>();

        /// <summary>
        /// 是否正在监控
        /// </summary>
        [ObservableProperty]
        private bool _isMonitoring = false;

        /// <summary>
        /// 设备总数
        /// </summary>
        [ObservableProperty]
        private int _totalDevices;

        /// <summary>
        /// 在线设备数量
        /// </summary>
        [ObservableProperty]
        private int _onlineDevices;

        /// <summary>
        /// 故障设备数量
        /// </summary>
        [ObservableProperty]
        private int _faultDevices;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 DashboardViewModel 实例
        /// </summary>
        /// <param name="monitorService">设备监控服务接口（通过 DI 注入）</param>
        public DashboardViewModel(IDeviceMonitorService monitorService)
        {
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));

            // 订阅数据更新事件
            _monitorService.OnDataUpdated += OnMonitorDataUpdated;

            // 订阅告警触发事件
            _monitorService.OnAlarmRaised += OnMonitorAlarmRaised;
        }

        #endregion

        #region 命令

        /// <summary>
        /// 启动设备监控
        /// 开始定期轮询所有设备数据
        /// </summary>
        [RelayCommand]
        private void StartMonitoring()
        {
            if (IsMonitoring)
                return;

            try
            {
                var success = _monitorService.StartMonitoring();
                if (success)
                {
                    IsMonitoring = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardVM] 启动监控失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 停止设备监控
        /// 停止数据轮询
        /// </summary>
        [RelayCommand]
        private void StopMonitoring()
        {
            if (!IsMonitoring)
                return;

            try
            {
                _monitorService.StopMonitoring();
                IsMonitoring = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardVM] 停止监控失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 确认告警
        /// 操作员确认指定的告警，记录确认时间和确认人
        /// </summary>
        /// <param name="alarmId">要确认的告警ID</param>
        [RelayCommand]
        private async Task AcknowledgeAlarmAsync(string alarmId)
        {
            if (string.IsNullOrEmpty(alarmId))
                return;

            try
            {
                // 调用监控服务确认告警
                // userId 应从当前登录用户获取，此处使用占位值
                var success = await Task.Run(() =>
                    _monitorService.AcknowledgeAlarm(alarmId, "current_user_id"));

                if (success)
                {
                    // 从告警列表中移除已确认的告警或更新其状态
                    var alarm = AlarmList.FirstOrDefault(a => a.AlarmId == alarmId);
                    if (alarm != null)
                    {
                        alarm.IsActive = false;
                        alarm.AcknowledgedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardVM] 确认告警失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 刷新设备列表和数据
        /// </summary>
        [RelayCommand]
        private void Refresh()
        {
            // 刷新活跃告警列表
            var activeAlarms = _monitorService.GetActiveAlarms();
            UpdateAlarmList(activeAlarms);

            // 更新统计信息
            UpdateStatistics();
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 监控数据更新事件处理
        /// 当收到设备新数据时更新对应的设备状态
        /// </summary>
        /// <param name="dataPoints">设备ID与最新数据点的字典</param>
        private void OnMonitorDataUpdated(Dictionary<string, DeviceDataPoint> dataPoints)
        {
            if (dataPoints == null)
                return;

            foreach (var kvp in dataPoints)
            {
                var deviceId = kvp.Key;
                var data = kvp.Value;

                // 在设备列表中查找或创建对应的设备状态
                var existingDevice = DeviceList.FirstOrDefault(d => d.DeviceId == deviceId);
                if (existingDevice == null)
                {
                    existingDevice = new DeviceStatusViewModel
                    {
                        DeviceId = deviceId,
                        DeviceName = $"设备_{deviceId}"
                    };
                    DeviceList.Add(existingDevice);
                }

                // 更新设备实时数据
                existingDevice.Voltage = data.Voltage;
                existingDevice.Current = data.Current;
                existingDevice.Power = data.Power;
                existingDevice.Temperature = data.Temperature;
                existingDevice.Soc = data.Soc;
                existingDevice.DataQuality = data.Quality;
                existingDevice.LastUpdateTimestamp = data.Timestamp;
                existingDevice.Status = "Online";

                // 如果该设备被选中，同步更新实时数值显示
                if (SelectedDevice != null && SelectedDevice.DeviceId == deviceId)
                {
                    VoltageValue = data.Voltage;
                    CurrentValue = data.Current;
                    PowerValue = data.Power;
                    TemperatureValue = data.Temperature;
                    SocValue = data.Soc;
                }
            }

            // 更新统计信息
            UpdateStatistics();
        }

        /// <summary>
        /// 监控告警触发事件处理
        /// 当检测到告警时，将新告警添加到告警列表
        /// </summary>
        /// <param name="alarm">触发的告警信息</param>
        private void OnMonitorAlarmRaised(DeviceAlarm alarm)
        {
            if (alarm == null)
                return;

            // 将告警转换为 AlarmViewModel 并添加到列表
            var alarmVM = new AlarmViewModel
            {
                AlarmId = alarm.AlarmId,
                DeviceId = alarm.DeviceId,
                AlarmCode = alarm.AlarmCode,
                AlarmLevel = alarm.AlarmLevel,
                AlarmMessage = alarm.AlarmMessage,
                ParameterName = alarm.ParameterName,
                ThresholdValue = alarm.ThresholdValue,
                ActualValue = alarm.ActualValue,
                IsActive = alarm.IsActive == 1,
                RaisedAt = alarm.RaisedAt
            };

            AlarmList.Add(alarmVM);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 更新告警列表
        /// </summary>
        /// <param name="alarms">告警记录列表</param>
        private void UpdateAlarmList(List<DeviceAlarm> alarms)
        {
            AlarmList.Clear();
            if (alarms == null)
                return;

            foreach (var alarm in alarms)
            {
                AlarmList.Add(new AlarmViewModel
                {
                    AlarmId = alarm.AlarmId,
                    DeviceId = alarm.DeviceId,
                    AlarmCode = alarm.AlarmCode,
                    AlarmLevel = alarm.AlarmLevel,
                    AlarmMessage = alarm.AlarmMessage,
                    ParameterName = alarm.ParameterName,
                    ThresholdValue = alarm.ThresholdValue,
                    ActualValue = alarm.ActualValue,
                    IsActive = alarm.IsActive == 1,
                    RaisedAt = alarm.RaisedAt,
                    AcknowledgedAt = alarm.AcknowledgedAt
                });
            }
        }

        /// <summary>
        /// 更新设备统计信息
        /// 计算在线、离线、故障设备数量
        /// </summary>
        private void UpdateStatistics()
        {
            TotalDevices = DeviceList.Count;
            OnlineDevices = DeviceList.Count(d => d.Status == "Online");
            FaultDevices = DeviceList.Count(d => d.Status == "Fault");
        }

        #endregion
    }
}
