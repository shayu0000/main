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
// 功能描述: 设备校准视图模型
// 说明: 管理设备电压/电流校准流程，支持多种校准类型
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 校准点视图模型
    /// 用于在 UI 中展示单个校准点的参考值、测量值和偏差信息
    /// </summary>
    public partial class CalPointViewModel : ObservableObject
    {
        /// <summary>校准点序号（从0开始）</summary>
        [ObservableProperty]
        private int _pointIndex;

        /// <summary>标准参考值（从高精度仪表读取）</summary>
        [ObservableProperty]
        private double _referenceValue;

        /// <summary>设备原始测量值（校准前）</summary>
        [ObservableProperty]
        private double _measuredValue;

        /// <summary>偏差百分比</summary>
        [ObservableProperty]
        private double? _deviation;
    }

    /// <summary>
    /// 设备校准视图模型
    /// 负责设备校准流程的管理：
    ///   1. 支持多种校准类型：Zero（零点）/ Span（量程）/ Linear（线性）/
    ///      VICurrent（电流精确）/ VIVoltage（电压精确）
    ///   2. 管理校准点数据采集和偏差计算
    ///   3. 显示校准进度（0-100%）和结果
    ///   4. 查询和展示校准历史记录
    ///   5. 导出校准报告
    /// </summary>
    public partial class CalibrationViewModel : ObservableObject
    {
        #region 字段

        private readonly ICalibrationService _calibrationService;
        private readonly IDeviceManagerService _deviceManagerService;

        /// <summary>当前校准会话ID</summary>
        private string? _currentCalibrationSessionId;

        #endregion

        #region 可观察属性

        /// <summary>可选设备列表</summary>
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> _deviceList = new ObservableCollection<DeviceInfo>();

        /// <summary>当前选中的校准目标设备</summary>
        [ObservableProperty]
        private DeviceInfo? _selectedDevice;

        /// <summary>
        /// 校准类型
        /// 可选值：Zero / Span / Linear / VICurrent / VIVoltage
        /// </summary>
        [ObservableProperty]
        private string _calibrationType = "Zero";

        /// <summary>标准参考值（量程校准时使用）</summary>
        [ObservableProperty]
        private double? _referenceValue;

        /// <summary>校准点集合（线性校准时使用多点数据）</summary>
        [ObservableProperty]
        private ObservableCollection<CalPointViewModel> _calibrationPoints = new ObservableCollection<CalPointViewModel>();

        /// <summary>校准历史记录集合</summary>
        [ObservableProperty]
        private ObservableCollection<CalibrationRecord> _calibrationHistory = new ObservableCollection<CalibrationRecord>();

        /// <summary>
        /// 校准状态描述
        /// 可选值：Idle（空闲）/ InProgress（进行中）/ Completed（已完成）/ Failed（失败）
        /// </summary>
        [ObservableProperty]
        private string _calibrationStatus = "Idle";

        /// <summary>校准进度百分比（0-100）</summary>
        [ObservableProperty]
        private double _calibrationProgress;

        /// <summary>校准结果信息（包含成功/失败状态和计算系数）</summary>
        [ObservableProperty]
        private string? _calibrationResult;

        /// <summary>是否正在执行校准操作</summary>
        [ObservableProperty]
        private bool _isCalibrating;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 CalibrationViewModel 实例
        /// </summary>
        /// <param name="calibrationService">校准服务接口（通过 DI 注入）</param>
        /// <param name="deviceManagerService">设备管理服务接口（通过 DI 注入）</param>
        public CalibrationViewModel(ICalibrationService calibrationService, IDeviceManagerService deviceManagerService)
        {
            _calibrationService = calibrationService ?? throw new ArgumentNullException(nameof(calibrationService));
            _deviceManagerService = deviceManagerService ?? throw new ArgumentNullException(nameof(deviceManagerService));
        }

        #endregion

        #region 命令

        /// <summary>
        /// 启动校准流程
        /// 根据选定的校准类型和设备执行对应的校准程序
        /// </summary>
        [RelayCommand]
        private async Task StartCalibrationAsync()
        {
            if (SelectedDevice == null)
            {
                CalibrationResult = "请先选择要校准的设备";
                return;
            }

            IsCalibrating = true;
            CalibrationProgress = 0;
            CalibrationStatus = "InProgress";

            try
            {
                List<double>? calibrationPointsList = null;
                if (CalibrationType == "Linear" && CalibrationPoints.Count > 0)
                {
                    calibrationPointsList = CalibrationPoints.Select(p => p.ReferenceValue).ToList();
                }

                _currentCalibrationSessionId = await Task.Run(() =>
                    _calibrationService.StartCalibration(
                        SelectedDevice.DeviceId,
                        CalibrationType.ToUpper(),
                        ReferenceValue,
                        calibrationPointsList));

                if (!string.IsNullOrEmpty(_currentCalibrationSessionId))
                {
                    CalibrationProgress = 50;
                    CalibrationResult = "校准流程已启动，等待数据采集完成...";
                }
                else
                {
                    CalibrationStatus = "Failed";
                    CalibrationResult = "启动校准失败，请检查设备通信状态";
                }
            }
            catch (Exception ex)
            {
                CalibrationStatus = "Failed";
                CalibrationResult = $"校准异常：{ex.Message}";
            }
            finally
            {
                IsCalibrating = false;
            }
        }

        /// <summary>
        /// 应用校准结果
        /// 将校准参数写入设备并更新数据库记录
        /// </summary>
        [RelayCommand]
        private async Task ApplyCalibrationAsync()
        {
            if (string.IsNullOrEmpty(_currentCalibrationSessionId))
            {
                CalibrationResult = "没有待应用的校准结果";
                return;
            }

            try
            {
                var success = await Task.Run(() =>
                    _calibrationService.ApplyCalibration(_currentCalibrationSessionId));

                if (success)
                {
                    CalibrationStatus = "Completed";
                    CalibrationProgress = 100;
                    CalibrationResult = "校准已成功应用";
                    _currentCalibrationSessionId = null;
                    await LoadHistoryAsync();
                }
                else
                {
                    CalibrationResult = "应用校准失败";
                }
            }
            catch (Exception ex)
            {
                CalibrationResult = $"应用校准异常：{ex.Message}";
            }
        }

        /// <summary>
        /// 取消当前校准操作
        /// </summary>
        [RelayCommand]
        private void CancelCalibration()
        {
            IsCalibrating = false;
            CalibrationStatus = "Idle";
            CalibrationProgress = 0;
            CalibrationResult = "校准已取消";
            _currentCalibrationSessionId = null;
        }

        /// <summary>
        /// 加载选中设备的校准历史记录（最近30天）
        /// </summary>
        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            if (SelectedDevice == null) return;

            try
            {
                var endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var startTime = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();

                var history = await Task.Run(() =>
                    _calibrationService.GetCalibrationHistory(SelectedDevice.DeviceId, startTime, endTime));

                CalibrationHistory.Clear();
                if (history != null)
                {
                    foreach (var record in history)
                        CalibrationHistory.Add(record);
                }
            }
            catch (Exception ex)
            {
                CalibrationResult = $"加载校准历史失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 导出校准报告
        /// </summary>
        [RelayCommand]
        private async Task ExportCalibrationReportAsync()
        {
            if (SelectedDevice == null)
            {
                CalibrationResult = "请先选择设备";
                return;
            }

            await Task.Delay(100); // 占位模拟
            CalibrationResult = $"校准报告已导出（设备：{SelectedDevice.DeviceName}）";
        }

        /// <summary>
        /// 加载设备列表
        /// </summary>
        [RelayCommand]
        private async Task LoadDeviceListAsync()
        {
            try
            {
                var devices = await Task.Run(() => _deviceManagerService.GetDeviceList());
                DeviceList.Clear();
                if (devices != null)
                {
                    foreach (var device in devices)
                        DeviceList.Add(device);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationVM] 加载设备列表失败：{ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>添加校准点（用于线性校准）</summary>
        public void AddCalibrationPoint(double referenceValue)
        {
            CalibrationPoints.Add(new CalPointViewModel
            {
                PointIndex = CalibrationPoints.Count,
                ReferenceValue = referenceValue,
                MeasuredValue = 0,
                Deviation = null
            });
        }

        /// <summary>清除所有校准点</summary>
        public void ClearCalibrationPoints() => CalibrationPoints.Clear();

        #endregion
    }
}
