using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Core.Services;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 主窗口视图模型
// 说明: 管理应用程序主窗口的导航、用户状态和侧边栏菜单
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 菜单项数据模型
    /// 定义侧边栏导航菜单的每一项，包含标题、图标、页面名称和所需权限
    /// </summary>
    public class MenuItem
    {
        /// <summary>
        /// 菜单项标题（显示文本）
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 菜单项图标字形（用于图标字体显示）
        /// </summary>
        public string IconGlyph { get; set; } = string.Empty;

        /// <summary>
        /// 导航目标页面名称
        /// 用于 NavigationService 定位对应的 ViewModel
        /// </summary>
        public string PageName { get; set; } = string.Empty;

        /// <summary>
        /// 访问该菜单项所需的权限名称
        /// 例如：device:read、calibration:execute 等
        /// </summary>
        public string RequiredPermission { get; set; } = string.Empty;
    }

    /// <summary>
    /// 主窗口视图模型
    /// 作为应用程序的主 ViewModel，管理：
    ///   1. 页面导航控制 - 根据 pageName 切换 CurrentViewModel
    ///   2. 当前登录用户信息 - CurrentUser、UserRole、IsLoggedIn
    ///   3. 侧边栏菜单管理 - 根据用户权限动态构建 SidebarMenuItems
    ///   4. 用户登出流程
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        #region 字段

        private readonly IUserService _userService;
        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region 可观察属性

        /// <summary>
        /// 当前显示的页面 ViewModel
        /// 导航时动态切换此属性以更新 UI 内容区域
        /// </summary>
        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        /// <summary>
        /// 当前登录的用户账户信息
        /// </summary>
        [ObservableProperty]
        private UserAccount? _currentUser;

        /// <summary>
        /// 当前用户的角色名称
        /// 例如：admin（管理员）、operator（操作员）、engineer（工程师）、viewer（查看者）
        /// </summary>
        [ObservableProperty]
        private string _userRole = string.Empty;

        /// <summary>
        /// 用户是否已登录
        /// </summary>
        [ObservableProperty]
        private bool _isLoggedIn = false;

        /// <summary>
        /// 侧边栏菜单项集合
        /// 根据用户角色和权限动态生成，仅显示用户有权访问的菜单项
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MenuItem> _sidebarMenuItems = new ObservableCollection<MenuItem>();

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 MainViewModel 实例
        /// </summary>
        /// <param name="userService">用户服务接口（通过 DI 依赖注入）</param>
        /// <param name="serviceProvider">服务提供者接口（通过 DI 依赖注入）</param>
        public MainViewModel(IUserService userService, IServiceProvider serviceProvider)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // 构建默认侧边栏菜单（未登录状态）
            BuildDefaultMenu();
        }

        #endregion

        #region 命令

        /// <summary>
        /// 导航到指定页面
        /// 根据传入的页面名称切换 CurrentViewModel
        /// </summary>
        /// <param name="pageName">目标页面名称，对应各 ViewModel 的注册键值</param>
        [RelayCommand]
        private void Navigate(string pageName)
        {
            if (!IsLoggedIn || CurrentUser == null)
                return;

            // 检查权限：从菜单项中查找该页面对应的权限要求并验证
            var menuItem = SidebarMenuItems.FirstOrDefault(item => item.PageName == pageName);
            if (menuItem == null || (!string.IsNullOrEmpty(menuItem.RequiredPermission) && !_userService.CheckPermission(CurrentUser.UserId, menuItem.RequiredPermission)))
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 无权限访问页面：{pageName}");
                return;
            }

            // 根据页面名称获取对应的ViewModel实例
            ObservableObject? viewModel = null;
            switch (pageName)
            {
                case "Dashboard":
                    viewModel = _serviceProvider.GetRequiredService<DashboardViewModel>();
                    break;
                case "DeviceManagement":
                    viewModel = _serviceProvider.GetRequiredService<DeviceManagementViewModel>();
                    break;
                case "Calibration":
                    viewModel = _serviceProvider.GetRequiredService<CalibrationViewModel>();
                    break;
                case "ProtocolManagement":
                    viewModel = _serviceProvider.GetRequiredService<ProtocolManagementViewModel>();
                    break;
                case "BatteryProtocol":
                    viewModel = _serviceProvider.GetRequiredService<BatteryProtocolViewModel>();
                    break;
                case "DataRecord":
                    viewModel = _serviceProvider.GetRequiredService<DataRecordViewModel>();
                    break;
                case "FirmwareUpgrade":
                    viewModel = _serviceProvider.GetRequiredService<FirmwareUpgradeViewModel>();
                    break;
                case "FaultRecord":
                    viewModel = _serviceProvider.GetRequiredService<FaultRecordViewModel>();
                    break;
                case "UserManagement":
                    viewModel = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                    break;
                case "Report":
                    viewModel = _serviceProvider.GetRequiredService<ReportViewModel>();
                    break;
                case "Settings":
                    viewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] 未知页面：{pageName}");
                    return;
            }

            // 设置当前ViewModel
            CurrentViewModel = viewModel;
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] 导航到页面：{pageName}");
        }

        /// <summary>
        /// 执行用户登出操作
        /// 清除当前用户信息，重置视图状态和菜单
        /// </summary>
        [RelayCommand]
        private void Logout()
        {
            if (CurrentUser == null)
                return;

            try
            {
                // 实际项目中应调用 _userService.Logout(sessionId) 使会话失效
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 用户 {CurrentUser.Username} 已登出");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 登出异常：{ex.Message}");
            }
            finally
            {
                // 重置用户状态
                CurrentUser = null;
                IsLoggedIn = false;
                UserRole = string.Empty;
                CurrentViewModel = null;

                // 重置为默认菜单
                BuildDefaultMenu();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置登录成功后的用户状态
        /// 由 LoginViewModel 的 OnLoginSuccess 事件触发调用
        /// </summary>
        /// <param name="user">登录成功的用户账户信息</param>
        /// <param name="role">用户角色名称</param>
        public void SetUserLogin(UserAccount user, string role)
        {
            CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
            UserRole = role ?? string.Empty;
            IsLoggedIn = true;

            // 根据用户角色和权限重新构建侧边栏菜单
            BuildSidebarMenuForUser(CurrentUser.UserId);
        }

        /// <summary>
        /// 根据用户权限构建侧边栏菜单
        /// 仅显示用户有权访问的菜单项，过滤掉无权限的项
        /// </summary>
        /// <param name="userId">当前登录用户的ID</param>
        private void BuildSidebarMenuForUser(string userId)
        {
            var items = new ObservableCollection<MenuItem>();

            // 定义所有可用的菜单项及其所需权限
            var allMenuItems = new[]
            {
                new MenuItem { Title = "设备监控",      IconGlyph = "\uE400", PageName = "Dashboard",           RequiredPermission = "device:read" },
                new MenuItem { Title = "设备管理",      IconGlyph = "\uE401", PageName = "DeviceManagement",     RequiredPermission = "device:write" },
                new MenuItem { Title = "设备校准",      IconGlyph = "\uE402", PageName = "Calibration",          RequiredPermission = "calibration:execute" },
                new MenuItem { Title = "协议管理",      IconGlyph = "\uE403", PageName = "ProtocolManagement",   RequiredPermission = "protocol:read" },
                new MenuItem { Title = "电池协议",      IconGlyph = "\uE404", PageName = "BatteryProtocol",      RequiredPermission = "protocol:read" },
                new MenuItem { Title = "数据记录",      IconGlyph = "\uE405", PageName = "DataRecord",           RequiredPermission = "data:read" },
                new MenuItem { Title = "固件升级",      IconGlyph = "\uE406", PageName = "FirmwareUpgrade",      RequiredPermission = "firmware:execute" },
                new MenuItem { Title = "故障录波",      IconGlyph = "\uE407", PageName = "FaultRecord",          RequiredPermission = "fault:read" },
                new MenuItem { Title = "用户管理",      IconGlyph = "\uE408", PageName = "UserManagement",       RequiredPermission = "user:write" },
                new MenuItem { Title = "测试报告",      IconGlyph = "\uE409", PageName = "Report",               RequiredPermission = "report:read" },
                new MenuItem { Title = "系统设置",      IconGlyph = "\uE40A", PageName = "Settings",             RequiredPermission = "system:write" },
            };

            // 根据用户权限过滤菜单项：空权限表示无需权限即可访问
            foreach (var menuItem in allMenuItems)
            {
                if (string.IsNullOrEmpty(menuItem.RequiredPermission) ||
                    _userService.CheckPermission(userId, menuItem.RequiredPermission))
                {
                    items.Add(menuItem);
                }
            }

            SidebarMenuItems = items;
        }

        /// <summary>
        /// 构建默认菜单（未登录状态）
        /// 未登录时侧边栏为空
        /// </summary>
        private void BuildDefaultMenu()
        {
            SidebarMenuItems = new ObservableCollection<MenuItem>();
        }

        #endregion
    }
}
