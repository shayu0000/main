using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Core.Services;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 设备管理视图模型
// 说明: 管理设备列表的增删改查操作和表单验证
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 设备管理视图模型
    /// 负责设备全生命周期的管理操作：
    ///   1. 设备列表的加载、搜索和过滤
    ///   2. 设备的添加、编辑、删除
    ///   3. 设备表单验证（额定功率、电压、CAN地址等）
    ///   4. 编辑状态管理
    /// </summary>
    public partial class DeviceManagementViewModel : ObservableObject
    {
        #region 字段

        private readonly IDeviceManagerService _deviceManagerService;

        /// <summary>当前编辑的设备信息（用于新增/编辑表单）</summary>
        private DeviceInfo? _editingDevice;

        #endregion

        #region 可观察属性

        /// <summary>
        /// 设备信息列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<DeviceInfo> _devices = new ObservableCollection<DeviceInfo>();

        /// <summary>
        /// 当前选中的设备
        /// </summary>
        [ObservableProperty]
        private DeviceInfo? _selectedDevice;

        /// <summary>
        /// 搜索文本（按设备名称或ID搜索）
        /// </summary>
        [ObservableProperty]
        private string _searchText = string.Empty;

        /// <summary>
        /// 设备类型过滤条件
        /// 可选值：PCS/BMS/Inverter/Meter/Charger/null
        /// </summary>
        [ObservableProperty]
        private string? _filterType;

        /// <summary>
        /// 设备状态过滤条件
        /// 可选值：Online/Offline/Fault/Maintenance/null
        /// </summary>
        [ObservableProperty]
        private string? _filterStatus;

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        [ObservableProperty]
        private bool _isEditing = false;

        /// <summary>
        /// 编辑模式："Add" 表示新增，"Edit" 表示编辑
        /// </summary>
        [ObservableProperty]
        private string _editMode = "Add";

        // ---- 表单绑定属性 ----

        /// <summary>表单：设备ID</summary>
        [ObservableProperty]
        private string _formDeviceId = string.Empty;

        /// <summary>表单：设备类型</summary>
        [ObservableProperty]
        private string _formDeviceType = string.Empty;

        /// <summary>表单：设备名称</summary>
        [ObservableProperty]
        private string _formDeviceName = string.Empty;

        /// <summary>表单：制造商</summary>
        [ObservableProperty]
        private string _formManufacturer = string.Empty;

        /// <summary>表单：型号</summary>
        [ObservableProperty]
        private string _formModel = string.Empty;

        /// <summary>表单：序列号</summary>
        [ObservableProperty]
        private string _formSerialNumber = string.Empty;

        /// <summary>表单：额定功率(kW)</summary>
        [ObservableProperty]
        private double _formRatedPowerKw;

        /// <summary>表单：额定电压(V)</summary>
        [ObservableProperty]
        private double _formRatedVoltageV;

        /// <summary>表单：额定电流(A)</summary>
        [ObservableProperty]
        private double _formRatedCurrentA;

        /// <summary>表单：CAN总线地址</summary>
        [ObservableProperty]
        private int _formCanAddress;

        /// <summary>表单：通信协议名称</summary>
        [ObservableProperty]
        private string _formProtocolName = string.Empty;

        /// <summary>表单：备注</summary>
        [ObservableProperty]
        private string _formNotes = string.Empty;

        /// <summary>表单验证错误信息</summary>
        [ObservableProperty]
        private string _validationMessage = string.Empty;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 DeviceManagementViewModel 实例
        /// </summary>
        /// <param name="deviceManagerService">设备管理服务接口（通过 DI 注入）</param>
        public DeviceManagementViewModel(IDeviceManagerService deviceManagerService)
        {
            _deviceManagerService = deviceManagerService ?? throw new ArgumentNullException(nameof(deviceManagerService));
        }

        #endregion

        #region 命令

        /// <summary>
        /// 添加新设备
        /// 进入新增模式，清空表单等待用户输入
        /// </summary>
        [RelayCommand]
        private void AddDevice()
        {
            ClearForm();
            EditMode = "Add";
            IsEditing = true;
            ValidationMessage = string.Empty;
        }

        /// <summary>
        /// 编辑选中设备
        /// 进入编辑模式，将选中设备的信息填充到表单
        /// </summary>
        [RelayCommand]
        private void EditDevice()
        {
            if (SelectedDevice == null)
            {
                ValidationMessage = "请先选择要编辑的设备";
                return;
            }

            // 将选中设备的信息填充到表单
            FormDeviceId = SelectedDevice.DeviceId;
            FormDeviceType = SelectedDevice.DeviceType;
            FormDeviceName = SelectedDevice.DeviceName;
            FormManufacturer = SelectedDevice.Manufacturer;
            FormModel = SelectedDevice.Model;
            FormSerialNumber = SelectedDevice.SerialNumber;
            FormRatedPowerKw = SelectedDevice.RatedPowerKw;
            FormRatedVoltageV = SelectedDevice.RatedVoltageV;
            FormRatedCurrentA = SelectedDevice.RatedCurrentA;
            FormCanAddress = SelectedDevice.CanAddress;
            FormProtocolName = SelectedDevice.ProtocolName;
            FormNotes = SelectedDevice.Notes;

            EditMode = "Edit";
            IsEditing = true;
            ValidationMessage = string.Empty;
        }

        /// <summary>
        /// 删除选中设备
        /// </summary>
        [RelayCommand]
        private async Task DeleteDeviceAsync()
        {
            if (SelectedDevice == null)
                return;

            try
            {
                var success = await Task.Run(() =>
                    _deviceManagerService.DeleteDevice(SelectedDevice.DeviceId));

                if (success)
                {
                    Devices.Remove(SelectedDevice);
                    SelectedDevice = null;
                }
                else
                {
                    ValidationMessage = "删除设备失败，请确认设备没有活跃会话";
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = $"删除设备异常：{ex.Message}";
            }
        }

        /// <summary>
        /// 保存设备信息（新增或更新）
        /// 执行表单验证后调用服务层保存
        /// </summary>
        [RelayCommand]
        private async Task SaveDeviceAsync()
        {
            // 执行表单验证
            if (!ValidateDeviceForm())
                return;

            var device = new DeviceInfo
            {
                DeviceId = FormDeviceId,
                DeviceType = FormDeviceType,
                DeviceName = FormDeviceName,
                Manufacturer = FormManufacturer,
                Model = FormModel,
                SerialNumber = FormSerialNumber,
                RatedPowerKw = FormRatedPowerKw,
                RatedVoltageV = FormRatedVoltageV,
                RatedCurrentA = FormRatedCurrentA,
                CanAddress = FormCanAddress,
                ProtocolName = FormProtocolName,
                Notes = FormNotes
            };

            try
            {
                bool success;
                if (EditMode == "Add")
                {
                    success = await Task.Run(() => _deviceManagerService.RegisterDevice(device));
                }
                else
                {
                    success = await Task.Run(() => _deviceManagerService.UpdateDevice(device));
                }

                if (success)
                {
                    IsEditing = false;
                    ValidationMessage = string.Empty;
                    // 刷新设备列表
                    await RefreshListAsync();
                }
                else
                {
                    ValidationMessage = "保存设备信息失败";
                }
            }
            catch (Exception ex)
            {
                ValidationMessage = $"保存设备异常：{ex.Message}";
            }
        }

        /// <summary>
        /// 取消编辑操作
        /// 退出编辑模式，丢弃未保存的修改
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            ValidationMessage = string.Empty;
            ClearForm();
        }

        /// <summary>
        /// 刷新设备列表
        /// 从服务层重新加载设备数据
        /// </summary>
        [RelayCommand]
        private async Task RefreshListAsync()
        {
            try
            {
                var deviceList = await Task.Run(() =>
                    _deviceManagerService.GetDeviceList(FilterType, FilterStatus));

                Devices.Clear();
                if (deviceList != null)
                {
                    foreach (var device in deviceList)
                    {
                        Devices.Add(device);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceManagementVM] 刷新列表失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 执行搜索过滤
        /// 根据搜索文本过滤设备列表
        /// </summary>
        [RelayCommand]
        private void Search()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // 搜索文本为空，刷新全部列表
                _ = RefreshListAsync();
                return;
            }

            // 根据搜索文本进行本地过滤
            var filtered = Devices.Where(d =>
                d.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceId.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (d.SerialNumber != null && d.SerialNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            Devices.Clear();
            foreach (var device in filtered)
            {
                Devices.Add(device);
            }
        }

        #endregion

        #region 表单验证

        /// <summary>
        /// 验证设备表单输入
        /// 检查额定功率、额定电压、CAN地址等字段的合法性
        /// </summary>
        /// <returns>验证通过返回 true，否则返回 false</returns>
        private bool ValidateDeviceForm()
        {
            // 验证设备ID不能为空
            if (string.IsNullOrWhiteSpace(FormDeviceId))
            {
                ValidationMessage = "设备ID不能为空";
                return false;
            }

            // 验证设备名称不能为空
            if (string.IsNullOrWhiteSpace(FormDeviceName))
            {
                ValidationMessage = "设备名称不能为空";
                return false;
            }

            // 验证额定功率必须大于0
            if (FormRatedPowerKw <= 0)
            {
                ValidationMessage = "额定功率必须大于0";
                return false;
            }

            // 验证额定电压必须大于0
            if (FormRatedVoltageV <= 0)
            {
                ValidationMessage = "额定电压必须大于0";
                return false;
            }

            // 验证CAN总线地址范围（1-255）
            if (FormCanAddress < 1 || FormCanAddress > 255)
            {
                ValidationMessage = "CAN总线地址必须在1到255之间";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 清空编辑表单
        /// </summary>
        private void ClearForm()
        {
            FormDeviceId = string.Empty;
            FormDeviceType = string.Empty;
            FormDeviceName = string.Empty;
            FormManufacturer = string.Empty;
            FormModel = string.Empty;
            FormSerialNumber = string.Empty;
            FormRatedPowerKw = 0;
            FormRatedVoltageV = 0;
            FormRatedCurrentA = 0;
            FormCanAddress = 0;
            FormProtocolName = string.Empty;
            FormNotes = string.Empty;
        }

        #endregion
    }
}
