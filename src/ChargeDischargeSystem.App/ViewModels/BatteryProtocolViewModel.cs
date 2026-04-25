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
// 功能描述: 电池协议管理视图模型
// 说明: 管理BMS电池通信协议的配置、电芯数据和电池包数据
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 电池协议管理视图模型
    /// 负责BMS电池管理系统协议的管理和数据监控：
    ///   1. 加载和配置不同BMS供应商的通信协议
    ///   2. 获取并展示单体电芯数据（电压、温度、均衡状态）
    ///   3. 获取并展示电池包整体数据（总电压、总电流、SOC、SOH等）
    ///   4. 监控BMS通信连接状态
    /// </summary>
    public partial class BatteryProtocolViewModel : ObservableObject
    {
        #region 字段

        private readonly IBatteryProtocolService _batteryProtocolService;

        #endregion

        #region 可观察属性

        /// <summary>电池协议配置列表</summary>
        [ObservableProperty]
        private ObservableCollection<BatteryProtocolConfig> _batteryProtocols = new ObservableCollection<BatteryProtocolConfig>();

        /// <summary>当前选中的电池协议配置</summary>
        [ObservableProperty]
        private BatteryProtocolConfig? _selectedProtocol;

        /// <summary>单体电芯数据列表</summary>
        [ObservableProperty]
        private ObservableCollection<CellData> _cellDataList = new ObservableCollection<CellData>();

        /// <summary>电池包总电压(V)</summary>
        [ObservableProperty]
        private double _packVoltage;

        /// <summary>电池包总电流(A)，正值充电/负值放电</summary>
        [ObservableProperty]
        private double _packCurrent;

        /// <summary>荷电状态 SOC (%)</summary>
        [ObservableProperty]
        private double _packSoc;

        /// <summary>健康状态 SOH (%)</summary>
        [ObservableProperty]
        private double _packSoh;

        /// <summary>单体最高温度(°C)</summary>
        [ObservableProperty]
        private double _maxCellTemp;

        /// <summary>BMS通信连接状态</summary>
        [ObservableProperty]
        private BmsConnectionStatus? _bmsConnectionStatus;

        /// <summary>操作状态消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 BatteryProtocolViewModel 实例
        /// </summary>
        /// <param name="batteryProtocolService">电池协议服务接口（通过 DI 注入）</param>
        public BatteryProtocolViewModel(IBatteryProtocolService batteryProtocolService)
        {
            _batteryProtocolService = batteryProtocolService ?? throw new ArgumentNullException(nameof(batteryProtocolService));
        }

        #endregion

        #region 命令

        /// <summary>加载选中的电池协议</summary>
        [RelayCommand]
        private async Task LoadProtocolAsync()
        {
            if (SelectedProtocol == null) { StatusMessage = "请先选择协议"; return; }

            try
            {
                var success = await Task.Run(() =>
                    _batteryProtocolService.LoadBatteryProtocol(
                        SelectedProtocol.ProtocolName,
                        SelectedProtocol.BmsVendor,
                        SelectedProtocol.ProtocolVersion));
                StatusMessage = success ? "协议加载成功" : "协议加载失败";
            }
            catch (Exception ex) { StatusMessage = $"加载协议异常：{ex.Message}"; }
        }

        /// <summary>保存电池协议配置</summary>
        [RelayCommand]
        private async Task SaveProtocolAsync()
        {
            if (SelectedProtocol == null) { StatusMessage = "请先选择要保存的协议"; return; }
            await Task.Delay(100);
            StatusMessage = "协议已保存";
        }

        /// <summary>获取单体电芯数据</summary>
        [RelayCommand]
        private async Task GetCellDataAsync()
        {
            try
            {
                var cellData = await Task.Run(() => _batteryProtocolService.GetCellData());
                CellDataList.Clear();
                if (cellData != null)
                {
                    foreach (var kvp in cellData.OrderBy(c => c.Key))
                        CellDataList.Add(kvp.Value);
                }
                StatusMessage = $"已获取 {CellDataList.Count} 个单体电芯数据";
            }
            catch (Exception ex) { StatusMessage = $"获取电芯数据失败：{ex.Message}"; }
        }

        /// <summary>获取电池包整体数据</summary>
        [RelayCommand]
        private async Task GetPackDataAsync()
        {
            try
            {
                var packData = await Task.Run(() => _batteryProtocolService.GetPackData());
                if (packData != null)
                {
                    PackVoltage = packData.TotalVoltage;
                    PackCurrent = packData.TotalCurrent;
                    PackSoc = packData.Soc;
                    PackSoh = packData.Soh;
                    MaxCellTemp = packData.CellMaxTemperature;
                    StatusMessage = $"电池包数据已更新：SOC={PackSoc}%, 电压={PackVoltage}V";
                }
                else StatusMessage = "未能获取电池包数据";
            }
            catch (Exception ex) { StatusMessage = $"获取电池包数据失败：{ex.Message}"; }
        }

        /// <summary>刷新BMS连接状态</summary>
        [RelayCommand]
        private void RefreshConnectionStatus()
        {
            try
            {
                BmsConnectionStatus = _batteryProtocolService.GetBmsConnectionStatus();
                if (BmsConnectionStatus != null)
                    StatusMessage = BmsConnectionStatus.IsConnected
                        ? $"BMS已连接（质量：{BmsConnectionStatus.CommunicationQuality}）"
                        : "BMS未连接";
            }
            catch (Exception ex) { StatusMessage = $"获取BMS连接状态失败：{ex.Message}"; }
        }

        #endregion
    }
}
