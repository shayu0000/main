using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Core.Services;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 固件升级视图模型
// 说明: 管理设备固件的远程升级全流程和版本控制
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 固件升级视图模型
    /// 负责设备固件远程升级的全流程管理：
    ///   1. 固件文件浏览和选择
    ///   2. 升级任务创建、启动和进度监控
    ///   3. 升级状态机各步骤的实时状态跟踪
    ///   4. 固件版本检查和回滚
    ///   5. 升级任务的取消
    /// </summary>
    public partial class FirmwareUpgradeViewModel : ObservableObject
    {
        #region 字段

        private readonly IFirmwareService _firmwareService;
        private readonly IDeviceManagerService _deviceManagerService;

        /// <summary>当前升级任务ID</summary>
        private string? _currentTaskId;

        #endregion

        #region 可观察属性

        /// <summary>可升级的设备列表</summary>
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> _devices = new ObservableCollection<DeviceInfo>();

        /// <summary>当前选中的目标升级设备</summary>
        [ObservableProperty]
        private DeviceInfo? _selectedDevice;

        /// <summary>选择的固件文件路径</summary>
        [ObservableProperty]
        private string _firmwareFile = string.Empty;

        /// <summary>目标固件版本号</summary>
        [ObservableProperty]
        private string _firmwareVersion = string.Empty;

        /// <summary>升级任务列表（任务历史记录）</summary>
        [ObservableProperty]
        private ObservableCollection<UpgradeTask> _upgradeTasks = new ObservableCollection<UpgradeTask>();

        /// <summary>当前选中的升级任务</summary>
        [ObservableProperty]
        private UpgradeTask? _selectedTask;

        /// <summary>升级进度百分比（0-100）</summary>
        [ObservableProperty]
        private double _upgradeProgress;

        /// <summary>
        /// 升级状态描述
        /// 可选值：Idle / Pending / InProgress / Completed / Failed / Cancelled
        /// </summary>
        [ObservableProperty]
        private string _upgradeStatus = "Idle";

        /// <summary>当前升级步骤的中文描述</summary>
        [ObservableProperty]
        private string _currentStep = "就绪";

        /// <summary>操作状态消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 FirmwareUpgradeViewModel 实例
        /// </summary>
        /// <param name="firmwareService">固件升级服务接口（通过 DI 注入）</param>
        /// <param name="deviceManagerService">设备管理服务接口（通过 DI 注入）</param>
        public FirmwareUpgradeViewModel(IFirmwareService firmwareService, IDeviceManagerService deviceManagerService)
        {
            _firmwareService = firmwareService ?? throw new ArgumentNullException(nameof(firmwareService));
            _deviceManagerService = deviceManagerService ?? throw new ArgumentNullException(nameof(deviceManagerService));

            // 订阅升级进度更新事件，实时接收进度通知
            _firmwareService.OnUpgradeProgressUpdated += OnUpgradeProgressUpdated;
        }

        #endregion

        #region 命令

        /// <summary>浏览并选择固件文件（打开文件选择对话框）</summary>
        [RelayCommand]
        private void BrowseFirmware()
        {
            // 实际项目中使用 OpenFileDialog 选择 .bin/.hex 固件文件
            // 此处为占位实现，由 View 层触发文件对话框
            StatusMessage = "请选择固件文件（.bin / .hex / .fw）";
        }

        /// <summary>启动固件升级流程</summary>
        [RelayCommand]
        private async Task StartUpgradeAsync()
        {
            if (SelectedDevice == null) { StatusMessage = "请先选择目标设备"; return; }
            if (string.IsNullOrWhiteSpace(FirmwareFile)) { StatusMessage = "请先选择固件文件"; return; }
            if (string.IsNullOrWhiteSpace(FirmwareVersion)) { StatusMessage = "请输入固件版本号"; return; }

            UpgradeStatus = "InProgress";
            UpgradeProgress = 0;
            CurrentStep = "正在启动升级...";

            try
            {
                _currentTaskId = await Task.Run(() =>
                    _firmwareService.StartUpgrade(SelectedDevice.DeviceId, FirmwareFile, FirmwareVersion));

                if (!string.IsNullOrEmpty(_currentTaskId))
                {
                    StatusMessage = "升级已启动，请等待完成...";
                }
                else
                {
                    UpgradeStatus = "Failed";
                    CurrentStep = "启动升级失败";
                    StatusMessage = "启动升级失败";
                }
            }
            catch (Exception ex)
            {
                UpgradeStatus = "Failed";
                CurrentStep = $"启动异常：{ex.Message}";
                StatusMessage = $"启动升级异常：{ex.Message}";
            }
        }

        /// <summary>取消当前正在进行的升级任务</summary>
        [RelayCommand]
        private async Task CancelUpgradeAsync()
        {
            if (string.IsNullOrEmpty(_currentTaskId)) { StatusMessage = "没有正在进行的升级任务"; return; }
            try
            {
                var ok = await Task.Run(() => _firmwareService.CancelUpgrade(_currentTaskId));
                if (ok)
                {
                    UpgradeStatus = "Cancelled";
                    CurrentStep = "升级已取消";
                    StatusMessage = "升级已取消";
                    _currentTaskId = null;
                }
                else StatusMessage = "取消失败";
            }
            catch (Exception ex) { StatusMessage = $"取消异常：{ex.Message}"; }
        }

        /// <summary>检查选中设备的当前固件版本</summary>
        [RelayCommand]
        private async Task CheckVersionAsync()
        {
            if (SelectedDevice == null) { StatusMessage = "请先选择设备"; return; }
            try
            {
                var ver = await Task.Run(() => _firmwareService.GetFirmwareVersion(SelectedDevice.DeviceId));
                StatusMessage = $"设备 {SelectedDevice.DeviceName} 当前固件版本：{ver}";
            }
            catch (Exception ex) { StatusMessage = $"检查版本失败：{ex.Message}"; }
        }

        /// <summary>回滚设备固件到上一个版本</summary>
        [RelayCommand]
        private async Task RollbackAsync()
        {
            if (SelectedDevice == null) { StatusMessage = "请先选择设备"; return; }
            try
            {
                var ok = await Task.Run(() => _firmwareService.RollbackFirmware(SelectedDevice.DeviceId));
                StatusMessage = ok ? "固件回滚成功" : "固件回滚失败";
            }
            catch (Exception ex) { StatusMessage = $"回滚异常：{ex.Message}"; }
        }

        /// <summary>加载设备列表</summary>
        [RelayCommand]
        private async Task LoadDeviceListAsync()
        {
            try
            {
                var list = await Task.Run(() => _deviceManagerService.GetDeviceList());
                Devices.Clear();
                if (list != null)
                    foreach (var d in list) Devices.Add(d);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FirmwareVM] 加载设备失败：{ex.Message}"); }
        }

        #endregion

        #region 事件处理

        /// <summary>固件升级进度更新事件处理</summary>
        private void OnUpgradeProgressUpdated(string taskId, UpgradeProgress progress)
        {
            if (progress == null) return;
            UpgradeProgress = progress.ProgressPercent;
            CurrentStep = progress.Message ?? progress.CurrentState;

            if (progress.HasError)
            {
                UpgradeStatus = "Failed";
                StatusMessage = progress.ErrorMessage ?? "升级过程发生错误";
            }
            else if (progress.CurrentState == "COMPLETED")
            {
                UpgradeStatus = "Completed";
                UpgradeProgress = 100;
                CurrentStep = "升级完成";
                StatusMessage = "固件升级已成功完成";
                _currentTaskId = null;
            }
        }

        #endregion
    }
}
