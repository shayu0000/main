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
// 功能描述: 测试报告生成视图模型
// 说明: 管理充放电测试报告的生成、预览、下载和定时调度
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 测试报告生成视图模型
    /// 负责各类充放电测试报告的全流程管理：
    ///   1. 支持多种报告类型：充电测试、放电测试、循环测试、校准报告等
    ///   2. 报告生成参数的配置（时间范围、设备、是否包含波形等）
    ///   3. 报告的生成、预览和下载
    ///   4. 定时报告调度（基于Cron表达式）
    /// </summary>
    public partial class ReportViewModel : ObservableObject
    {
        #region 字段

        private readonly IReportService _reportService;

        #endregion

        #region 可观察属性

        /// <summary>可选的报告类型列表</summary>
        [ObservableProperty]
        private ObservableCollection<string> _reportTypes = new ObservableCollection<string>
        {
            "充电测试报告", "放电测试报告", "循环测试报告", "化成测试报告",
            "校准报告", "故障分析报告", "日报", "月报"
        };

        /// <summary>当前选中的报告类型</summary>
        [ObservableProperty]
        private string _selectedReportType = "充电测试报告";

        /// <summary>已生成的报告列表</summary>
        [ObservableProperty]
        private ObservableCollection<TestReport> _reports = new ObservableCollection<TestReport>();

        /// <summary>当前选中的报告</summary>
        [ObservableProperty]
        private TestReport? _selectedReport;

        // ---- 报告参数 ----

        /// <summary>报告参数：起始时间</summary>
        [ObservableProperty]
        private DateTime _paramStartTime = DateTime.Now.AddDays(-7);

        /// <summary>报告参数：结束时间</summary>
        [ObservableProperty]
        private DateTime _paramEndTime = DateTime.Now;

        /// <summary>报告参数：设备ID</summary>
        [ObservableProperty]
        private string _paramDeviceId = string.Empty;

        /// <summary>报告参数：关联会话ID</summary>
        [ObservableProperty]
        private string _paramSessionId = string.Empty;

        /// <summary>报告选项：是否包含波形数据</summary>
        [ObservableProperty]
        private bool _includeWaveforms = false;

        /// <summary>报告选项：是否包含校准数据</summary>
        [ObservableProperty]
        private bool _includeCalibrationData = false;

        // ---- 定时调度 ----

        /// <summary>Cron调度表达式（如 "0 8 * * *" 表示每天8点）</summary>
        [ObservableProperty]
        private string _cronExpression = "0 8 * * *";

        /// <summary>操作状态消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 ReportViewModel 实例
        /// </summary>
        /// <param name="reportService">报告生成服务接口（通过 DI 注入）</param>
        public ReportViewModel(IReportService reportService)
        {
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        }

        #endregion

        #region 命令

        /// <summary>生成测试报告</summary>
        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            try
            {
                // 将中文报告类型映射为服务层识别的类型编码
                var typeMapping = new Dictionary<string, string>
                {
                    { "充电测试报告", "charge_test" },
                    { "放电测试报告", "discharge_test" },
                    { "循环测试报告", "cycle_test" },
                    { "化成测试报告", "formation_test" },
                    { "校准报告", "calibration_report" },
                    { "故障分析报告", "fault_analysis" },
                    { "日报", "daily_summary" },
                    { "月报", "monthly_summary" }
                };

                var reportType = typeMapping.TryGetValue(SelectedReportType, out var mapped) ? mapped : "charge_test";

                var parameters = new Dictionary<string, object>
                {
                    { "startTime", ParamStartTime.ToString("yyyy-MM-dd") },
                    { "endTime", ParamEndTime.ToString("yyyy-MM-dd") },
                    { "includeWaveforms", IncludeWaveforms },
                    { "includeCalibrationData", IncludeCalibrationData }
                };

                var reportId = await Task.Run(() =>
                    _reportService.GenerateReport(reportType, ParamSessionId, ParamDeviceId, parameters));

                if (!string.IsNullOrEmpty(reportId))
                {
                    StatusMessage = $"报告生成成功（报告ID：{reportId}）";
                    await RefreshReportListAsync();
                }
                else
                    StatusMessage = "报告生成失败";
            }
            catch (Exception ex) { StatusMessage = $"生成报告异常：{ex.Message}"; }
        }

        /// <summary>预览选中的报告</summary>
        [RelayCommand]
        private async Task PreviewReportAsync()
        {
            if (SelectedReport == null) { StatusMessage = "请先选择要预览的报告"; return; }
            try
            {
                var filePath = await Task.Run(() => _reportService.DownloadReport(SelectedReport.ReportId));
                if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    // 使用系统默认程序打开报告文件
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                    StatusMessage = "报告已打开";
                }
                else
                    StatusMessage = "报告文件不存在";
            }
            catch (Exception ex) { StatusMessage = $"预览失败：{ex.Message}"; }
        }

        /// <summary>下载选中的报告</summary>
        [RelayCommand]
        private async Task DownloadReportAsync()
        {
            if (SelectedReport == null) { StatusMessage = "请先选择要下载的报告"; return; }
            try
            {
                var filePath = await Task.Run(() => _reportService.DownloadReport(SelectedReport.ReportId));
                StatusMessage = !string.IsNullOrEmpty(filePath)
                    ? $"报告已下载：{filePath}"
                    : "下载失败";
            }
            catch (Exception ex) { StatusMessage = $"下载异常：{ex.Message}"; }
        }

        /// <summary>创建定时报告调度任务</summary>
        [RelayCommand]
        private async Task ScheduleReportAsync()
        {
            try
            {
                var typeMapping = new Dictionary<string, string>
                {
                    { "充电测试报告", "charge_test" }, { "放电测试报告", "discharge_test" },
                    { "循环测试报告", "cycle_test" }, { "校准报告", "calibration_report" },
                    { "日报", "daily_summary" }, { "月报", "monthly_summary" }
                };
                var reportType = typeMapping.TryGetValue(SelectedReportType, out var mapped) ? mapped : "charge_test";
                var parameters = new Dictionary<string, object> { { "auto", true } };
                var taskId = await Task.Run(() =>
                    _reportService.ScheduleReport(reportType, CronExpression, parameters));

                StatusMessage = !string.IsNullOrEmpty(taskId)
                    ? $"定时报告已创建（任务ID：{taskId}，Cron：{CronExpression}）"
                    : "创建定时报告失败";
            }
            catch (Exception ex) { StatusMessage = $"创建定时报告异常：{ex.Message}"; }
        }

        /// <summary>刷新报告列表</summary>
        [RelayCommand]
        private async Task RefreshReportListAsync()
        {
            try
            {
                var list = await Task.Run(() => _reportService.GetReportList());
                Reports.Clear();
                if (list != null)
                    foreach (var r in list) Reports.Add(r);
                StatusMessage = $"已加载 {Reports.Count} 份报告";
            }
            catch (Exception ex) { StatusMessage = $"刷新报告列表失败：{ex.Message}"; }
        }

        #endregion
    }
}
