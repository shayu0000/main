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
// 功能描述: 数据记录与查询视图模型
// 说明: 管理充放电数据记录、历史查询、CSV导出和过期数据清理
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 数据记录与查询视图模型
    /// 负责充放电测试数据的记录管理和历史查询：
    ///   1. 启动/停止数据记录会话
    ///   2. 按时间范围和设备查询历史测量数据
    ///   3. 管理充放电会话列表
    ///   4. 导出查询结果为CSV文件
    ///   5. 清理过期数据释放存储空间
    /// </summary>
    public partial class DataRecordViewModel : ObservableObject
    {
        #region 字段

        private readonly IDataLogService _dataLogService;

        #endregion

        #region 可观察属性

        /// <summary>充放电会话列表</summary>
        [ObservableProperty]
        private ObservableCollection<ChargeSession> _sessions = new ObservableCollection<ChargeSession>();

        /// <summary>当前选中的充放电会话</summary>
        [ObservableProperty]
        private ChargeSession? _selectedSession;

        /// <summary>查询结果：测量数据点集合</summary>
        [ObservableProperty]
        private ObservableCollection<MeasurementData> _dataPoints = new ObservableCollection<MeasurementData>();

        /// <summary>查询起始时间</summary>
        [ObservableProperty]
        private DateTime _queryStartTime = DateTime.Now.AddDays(-7);

        /// <summary>查询结束时间</summary>
        [ObservableProperty]
        private DateTime _queryEndTime = DateTime.Now;

        /// <summary>查询设备ID</summary>
        [ObservableProperty]
        private string _queryDeviceId = string.Empty;

        /// <summary>查询参数名称（voltage/current/power/temperature/soc）</summary>
        [ObservableProperty]
        private string _queryParameter = "voltage";

        /// <summary>
        /// 记录状态
        /// 可选值：Idle（空闲）/ Recording（记录中）/ Error（错误）
        /// </summary>
        [ObservableProperty]
        private string _recordingStatus = "Idle";

        /// <summary>数据库文件大小(MB)</summary>
        [ObservableProperty]
        private double _databaseSize;

        /// <summary>图表绑定数据（时间戳, 测量值）</summary>
        [ObservableProperty]
        private List<(long Timestamp, double Value)> _chartData = new List<(long, double)>();

        /// <summary>操作状态消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 DataRecordViewModel 实例
        /// </summary>
        /// <param name="dataLogService">数据记录服务接口（通过 DI 注入）</param>
        public DataRecordViewModel(IDataLogService dataLogService)
        {
            _dataLogService = dataLogService ?? throw new ArgumentNullException(nameof(dataLogService));
            _dataLogService.OnRecordingStatusChanged += OnServiceRecordingStatusChanged;
        }

        #endregion

        #region 命令

        /// <summary>启动数据记录</summary>
        [RelayCommand]
        private async Task StartRecordingAsync()
        {
            if (SelectedSession == null) { StatusMessage = "请先选择一个会话"; return; }
            try
            {
                var success = await Task.Run(() => _dataLogService.StartRecording(SelectedSession.SessionId));
                StatusMessage = success ? "数据记录已启动" : "启动记录失败";
            }
            catch (Exception ex) { StatusMessage = $"启动记录异常：{ex.Message}"; }
        }

        /// <summary>停止数据记录</summary>
        [RelayCommand]
        private async Task StopRecordingAsync()
        {
            if (SelectedSession == null) return;
            try
            {
                var success = await Task.Run(() => _dataLogService.StopRecording(SelectedSession.SessionId));
                StatusMessage = success ? "数据记录已停止" : "停止记录失败";
            }
            catch (Exception ex) { StatusMessage = $"停止记录异常：{ex.Message}"; }
        }

        /// <summary>查询历史数据</summary>
        [RelayCommand]
        private async Task QueryDataAsync()
        {
            if (string.IsNullOrWhiteSpace(QueryDeviceId)) { StatusMessage = "请输入设备ID"; return; }
            try
            {
                var startMs = new DateTimeOffset(QueryStartTime).ToUnixTimeMilliseconds();
                var endMs = new DateTimeOffset(QueryEndTime).ToUnixTimeMilliseconds();

                var data = await Task.Run(() =>
                    _dataLogService.QueryData(QueryDeviceId, new List<string> { QueryParameter }, startMs, endMs));

                DataPoints.Clear();
                var chart = new List<(long, double)>();
                if (data != null)
                {
                    foreach (var item in data)
                    {
                        DataPoints.Add(item);
                        chart.Add((item.Timestamp, item.Value));
                    }
                }
                ChartData = chart;
                StatusMessage = $"查询完成，共 {DataPoints.Count} 条数据";
            }
            catch (Exception ex) { StatusMessage = $"查询失败：{ex.Message}"; }
        }

        /// <summary>导出数据为CSV文件</summary>
        [RelayCommand]
        private async Task ExportDataAsync()
        {
            if (DataPoints.Count == 0) { StatusMessage = "没有可导出的数据"; return; }
            try
            {
                var lines = new List<string> { "Timestamp,DeviceId,ParameterName,Value,Unit,Quality" };
                foreach (var dp in DataPoints)
                    lines.Add($"{dp.Timestamp},{dp.DeviceId},{dp.ParameterName},{dp.Value},{dp.Unit},{dp.Quality}");

                var filePath = $"Data/exports/export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                await System.IO.File.WriteAllTextAsync(filePath, string.Join(Environment.NewLine, lines));
                StatusMessage = $"数据已导出：{filePath}";
            }
            catch (Exception ex) { StatusMessage = $"导出失败：{ex.Message}"; }
        }

        /// <summary>删除30天前的过期数据</summary>
        [RelayCommand]
        private async Task DeleteOldDataAsync()
        {
            try
            {
                var beforeMs = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
                var count = await Task.Run(() => _dataLogService.DeleteOldData(beforeMs));
                StatusMessage = $"已删除 {count} 条过期数据";
            }
            catch (Exception ex) { StatusMessage = $"删除失败：{ex.Message}"; }
        }

        /// <summary>刷新会话列表</summary>
        [RelayCommand]
        private async Task RefreshSessionListAsync()
        {
            try
            {
                var startMs = new DateTimeOffset(DateTime.Now.AddDays(-30)).ToUnixTimeMilliseconds();
                var endMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var list = await Task.Run(() => _dataLogService.GetSessionList(startMs, endMs));
                Sessions.Clear();
                if (list != null)
                    foreach (var s in list) Sessions.Add(s);
                StatusMessage = $"已加载 {Sessions.Count} 个会话";
            }
            catch (Exception ex) { StatusMessage = $"刷新失败：{ex.Message}"; }
        }

        #endregion

        #region 事件处理

        private void OnServiceRecordingStatusChanged(string sessionId, string newStatus)
        {
            RecordingStatus = newStatus;
        }

        #endregion
    }
}
