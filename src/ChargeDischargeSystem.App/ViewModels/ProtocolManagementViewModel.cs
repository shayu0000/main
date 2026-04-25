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
// 功能描述: 协议管理视图模型
// 说明: 管理通信协议的加载、配置、命令定义和消息解析测试
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 协议管理视图模型
    /// 负责通信协议的全生命周期管理：
    ///   1. 协议的加载、添加、编辑、保存和删除
    ///   2. 协议命令的定义和管理
    ///   3. 协议消息解析测试
    ///   4. 编辑状态管理
    /// </summary>
    public partial class ProtocolManagementViewModel : ObservableObject
    {
        #region 字段

        private readonly IProtocolService _protocolService;

        #endregion

        #region 可观察属性

        /// <summary>已加载的协议配置列表</summary>
        [ObservableProperty]
        private ObservableCollection<ProtocolConfig> _protocols = new ObservableCollection<ProtocolConfig>();

        /// <summary>当前选中的协议配置</summary>
        [ObservableProperty]
        private ProtocolConfig? _selectedProtocol;

        /// <summary>当前选中协议的命令列表</summary>
        [ObservableProperty]
        private ObservableCollection<ProtocolCommand> _commands = new ObservableCollection<ProtocolCommand>();

        /// <summary>是否处于编辑模式</summary>
        [ObservableProperty]
        private bool _isEditing;

        /// <summary>编辑模式："Add"（新增）或 "Edit"（编辑）</summary>
        [ObservableProperty]
        private string _editMode = "Add";

        // ---- 表单绑定属性 ----

        /// <summary>表单：协议ID</summary>
        [ObservableProperty]
        private string _formProtocolId = string.Empty;

        /// <summary>表单：协议名称</summary>
        [ObservableProperty]
        private string _formProtocolName = string.Empty;

        /// <summary>表单：协议类型（device/battery）</summary>
        [ObservableProperty]
        private string _formProtocolType = "device";

        /// <summary>表单：协议版本号</summary>
        [ObservableProperty]
        private string _formProtocolVersion = string.Empty;

        /// <summary>表单：协议配置内容（JSON格式）</summary>
        [ObservableProperty]
        private string _formConfigContent = "{}";

        /// <summary>表单：是否启用</summary>
        [ObservableProperty]
        private bool _formIsActive = true;

        // ---- 协议测试 ----

        /// <summary>测试消息的CAN ID</summary>
        [ObservableProperty]
        private uint _testCanId;

        /// <summary>测试消息的原始数据（十六进制字符串）</summary>
        [ObservableProperty]
        private string _testRawData = string.Empty;

        /// <summary>解析结果文本</summary>
        [ObservableProperty]
        private string _parseResult = string.Empty;

        /// <summary>操作状态消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 ProtocolManagementViewModel 实例
        /// </summary>
        /// <param name="protocolService">协议管理服务接口（通过 DI 注入）</param>
        public ProtocolManagementViewModel(IProtocolService protocolService)
        {
            _protocolService = protocolService ?? throw new ArgumentNullException(nameof(protocolService));
        }

        #endregion

        #region 命令

        /// <summary>加载指定协议配置文件</summary>
        [RelayCommand]
        private async Task LoadProtocolAsync(string protocolFile)
        {
            if (string.IsNullOrWhiteSpace(protocolFile))
            {
                StatusMessage = "请指定协议配置文件路径";
                return;
            }

            try
            {
                var success = await Task.Run(() => _protocolService.LoadProtocol("protocol_name", protocolFile));
                StatusMessage = success ? "协议加载成功" : "协议加载失败";
                if (success) await RefreshProtocolListAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载协议异常：{ex.Message}";
            }
        }

        /// <summary>进入新增协议模式</summary>
        [RelayCommand]
        private void AddProtocol()
        {
            ClearForm();
            EditMode = "Add";
            IsEditing = true;
            StatusMessage = string.Empty;
        }

        /// <summary>进入编辑协议模式</summary>
        [RelayCommand]
        private void EditProtocol()
        {
            if (SelectedProtocol == null)
            {
                StatusMessage = "请先选择要编辑的协议";
                return;
            }

            FormProtocolId = SelectedProtocol.ProtocolId;
            FormProtocolName = SelectedProtocol.ProtocolName;
            FormProtocolType = SelectedProtocol.ProtocolType;
            FormProtocolVersion = SelectedProtocol.ProtocolVersion;
            FormConfigContent = SelectedProtocol.ConfigContent;
            FormIsActive = SelectedProtocol.IsActive == 1;

            EditMode = "Edit";
            IsEditing = true;
            StatusMessage = string.Empty;
        }

        /// <summary>保存协议配置</summary>
        [RelayCommand]
        private async Task SaveProtocolAsync()
        {
            if (string.IsNullOrWhiteSpace(FormProtocolName))
            {
                StatusMessage = "协议名称不能为空";
                return;
            }

            StatusMessage = EditMode == "Add" ? "新协议已创建" : "协议已更新";
            IsEditing = false;
            await RefreshProtocolListAsync();
        }

        /// <summary>删除选中协议</summary>
        [RelayCommand]
        private async Task DeleteProtocolAsync()
        {
            if (SelectedProtocol == null) return;
            Protocols.Remove(SelectedProtocol);
            SelectedProtocol = null;
            Commands.Clear();
            StatusMessage = "协议已删除";
            await Task.CompletedTask;
        }

        /// <summary>测试协议消息解析</summary>
        [RelayCommand]
        private async Task ParseTestMessageAsync()
        {
            if (SelectedProtocol == null) { StatusMessage = "请先选择协议"; return; }
            if (string.IsNullOrWhiteSpace(TestRawData)) { StatusMessage = "请输入待解析的原始数据"; return; }

            try
            {
                byte[] rawBytes = HexStringToBytes(TestRawData);
                var result = await Task.Run(() =>
                    _protocolService.ParseMessage(SelectedProtocol.ProtocolName, TestCanId, rawBytes));

                if (result != null)
                {
                    ParseResult = string.Join("\n", result.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                    StatusMessage = "解析成功";
                }
                else
                {
                    ParseResult = "无法解析该消息";
                    StatusMessage = "解析失败";
                }
            }
            catch (Exception ex)
            {
                ParseResult = string.Empty;
                StatusMessage = $"解析异常：{ex.Message}";
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>刷新协议列表</summary>
        private async Task RefreshProtocolListAsync()
        {
            try
            {
                var protocols = await Task.Run(() => _protocolService.ListProtocols());
                Protocols.Clear();
                if (protocols != null)
                {
                    foreach (var p in protocols) Protocols.Add(p);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolManagementVM] 刷新失败：{ex.Message}");
            }
        }

        /// <summary>选中协议变更时更新命令列表</summary>
        partial void OnSelectedProtocolChanged(ProtocolConfig? value)
        {
            Commands.Clear();
            if (value?.Commands != null)
            {
                foreach (var cmd in value.Commands) Commands.Add(cmd);
            }
        }

        /// <summary>十六进制字符串转字节数组</summary>
        private static byte[] HexStringToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "");
            if (hex.Length % 2 != 0) throw new ArgumentException("无效的十六进制字符串长度");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }

        /// <summary>清空编辑表单</summary>
        private void ClearForm()
        {
            FormProtocolId = string.Empty;
            FormProtocolName = string.Empty;
            FormProtocolType = "device";
            FormProtocolVersion = string.Empty;
            FormConfigContent = "{}";
            FormIsActive = true;
        }

        #endregion
    }
}
