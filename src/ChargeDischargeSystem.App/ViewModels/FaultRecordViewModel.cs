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
// 功能描述: 故障录波视图模型
// 说明: 管理故障事件的自动检测、录波存储和波形分析
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 故障录波视图模型
    /// 负责设备故障事件的录波管理和波形分析：
    ///   1. 启用/禁用指定设备的故障录波功能
    ///   2. 按设备、时间范围、故障等级查询故障事件
    ///   3. 加载和显示故障波形数据
    ///   4. 导出波形数据为CSV文件
    ///   5. 实时接收并展示新触发的故障事件
    /// </summary>
    public partial class FaultRecordViewModel : ObservableObject
    {
        #region 字段

        private readonly IFaultRecordService _faultRecordService;

        #endregion

        #region 可观察属性

        /// <summary>故障事件列表</summary>
        [ObservableProperty]
        private ObservableCollection<FaultEvent> _faultEvents = new ObservableCollection<FaultEvent>();

        /// <summary>当前选中的故障事件</summary>
        [ObservableProperty]
        private FaultEvent? _selectedFault;

        /// <summary>当前故障的波形数据（通道名称 -> 采样值数组）</summary>
        [ObservableProperty]
        private Dictionary<string, double[]>? _waveformData;

        /// <summary>过滤条件：设备ID</summary>
        [ObservableProperty]
        private string _filterDeviceId = string.Empty;

        /// <summary>过滤条件：起始时间</summary>
        [ObservableProperty]
        private DateTime _filterStartTime = DateTime.Now.AddDays(-7);

        /// <summary>过滤条件：结束时间</summary>
        [ObservableProperty]
        private DateTime _filterEndTime = DateTime.Now;

        /// <summary>过滤条件：故障等级（CRITICAL/MAJOR/MINOR）</summary>
        [ObservableProperty]
        private string? _filterLevel;

        /// <summary>故障录波功能是否已启用</summary>
        [ObservableProperty]
        private bool _faultRecordingEnabled = false;

        /// <summary>波形图表数据（用于 UI 绑定的波形点）</summary>
        [ObservableProperty]
        private List<(long Timestamp, double Value)> _waveformChartData = new List<(long, double)>();

        /// <summary>操作状态消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 FaultRecordViewModel 实例
        /// </summary>
        /// <param name="faultRecordService">故障录波服务接口（通过 DI 注入）</param>
        public FaultRecordViewModel(IFaultRecordService faultRecordService)
        {
            _faultRecordService = faultRecordService ?? throw new ArgumentNullException(nameof(faultRecordService));

            // 订阅故障发生事件，实现实时故障通知
            _faultRecordService.OnFaultOccurred += OnFaultOccurred;
        }

        #endregion

        #region 命令

        /// <summary>启用故障录波功能</summary>
        [RelayCommand]
        private async Task EnableRecordingAsync()
        {
            if (string.IsNullOrWhiteSpace(FilterDeviceId)) { StatusMessage = "请先输入设备ID"; return; }
            try
            {
                var config = new FaultTriggerConfig
                {
                    Enabled = true,
                    TriggerChannel = "dc_voltage",
                    TriggerMode = "absolute",
                    TriggerThreshold = 900,
                    PreFaultDurationS = 2.0,
                    PostFaultDurationS = 3.0,
                    SampleRateHz = 1000.0
                };
                var ok = await Task.Run(() => _faultRecordService.EnableFaultRecording(FilterDeviceId, config));
                if (ok) { FaultRecordingEnabled = true; StatusMessage = "故障录波已启用"; }
                else StatusMessage = "启用故障录波失败";
            }
            catch (Exception ex) { StatusMessage = $"启用录波异常：{ex.Message}"; }
        }

        /// <summary>禁用故障录波功能</summary>
        [RelayCommand]
        private async Task DisableRecordingAsync()
        {
            if (string.IsNullOrWhiteSpace(FilterDeviceId)) { StatusMessage = "请先输入设备ID"; return; }
            try
            {
                var ok = await Task.Run(() => _faultRecordService.DisableFaultRecording(FilterDeviceId));
                if (ok) { FaultRecordingEnabled = false; StatusMessage = "故障录波已禁用"; }
                else StatusMessage = "禁用故障录波失败";
            }
            catch (Exception ex) { StatusMessage = $"禁用录波异常：{ex.Message}"; }
        }

        /// <summary>按过滤条件查询故障事件</summary>
        [RelayCommand]
        private async Task QueryFaultsAsync()
        {
            if (string.IsNullOrWhiteSpace(FilterDeviceId)) { StatusMessage = "请输入设备ID"; return; }
            try
            {
                var startMs = new DateTimeOffset(FilterStartTime).ToUnixTimeMilliseconds();
                var endMs = new DateTimeOffset(FilterEndTime).ToUnixTimeMilliseconds();
                var events = await Task.Run(() =>
                    _faultRecordService.ListFaultEvents(FilterDeviceId, startMs, endMs, FilterLevel));
                FaultEvents.Clear();
                if (events != null)
                    foreach (var e in events) FaultEvents.Add(e);
                StatusMessage = $"查询完成，共 {FaultEvents.Count} 个故障事件";
            }
            catch (Exception ex) { StatusMessage = $"查询故障失败：{ex.Message}"; }
        }

        /// <summary>导出选中故障的波形数据</summary>
        [RelayCommand]
        private async Task ExportWaveformAsync()
        {
            if (SelectedFault == null) { StatusMessage = "请先选择一个故障事件"; return; }
            try
            {
                var path = await Task.Run(() => _faultRecordService.ExportWaveform(SelectedFault.EventId, "csv"));
                StatusMessage = $"波形数据已导出：{path}";
            }
            catch (Exception ex) { StatusMessage = $"导出失败：{ex.Message}"; }
        }

        /// <summary>选中故障事件并加载其波形数据</summary>
        [RelayCommand]
        private async Task SelectFaultAsync(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return;
            try
            {
                var waveform = await Task.Run(() => _faultRecordService.GetWaveformData(eventId));
                WaveformData = waveform;
                if (waveform != null && waveform.Count > 0)
                {
                    var first = waveform.First();
                    var points = new List<(long, double)>();
                    for (int i = 0; i < first.Value.Length; i++)
                        points.Add((i, first.Value[i]));
                    WaveformChartData = points;
                }
                StatusMessage = $"波形数据已加载（{waveform?.Count ?? 0} 个通道）";
            }
            catch (Exception ex) { StatusMessage = $"加载波形失败：{ex.Message}"; }
        }

        #endregion

        #region 事件处理

        /// <summary>故障发生事件处理（实时接收）</summary>
        private void OnFaultOccurred(FaultEvent faultEvent)
        {
            if (faultEvent == null) return;
            FaultEvents.Insert(0, faultEvent);
            SelectedFault = faultEvent;
            _ = SelectFaultAsync(faultEvent.EventId);
        }

        #endregion
    }
}
