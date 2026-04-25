using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Core.Services;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 登录视图模型
// 说明: 处理用户登录认证逻辑，包括验证、锁定和错误处理
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 登录设置数据模型
    /// 用于持久化保存用户的登录偏好设置（记住用户名/密码）
    /// </summary>
    public class LoginSettings
    {
        /// <summary>用户名</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>密码</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>是否记住用户名</summary>
        public bool RememberUsername { get; set; }

        /// <summary>是否记住密码</summary>
        public bool RememberPassword { get; set; }
    }

    /// <summary>
    /// 登录视图模型
    /// 负责用户登录认证的全流程管理：
    ///   1. 用户名/密码输入验证
    ///   2. 调用认证服务进行登录验证
    ///   3. 登录失败锁定机制（最大尝试次数限制）
    ///   4. 记住用户名/密码的本地持久化
    ///   5. 登录成功/失败的事件通知
    /// </summary>
    public partial class LoginViewModel : ObservableObject
    {
        #region 常量

        /// <summary>最大登录尝试次数</summary>
        private const int MaxLoginAttempts = 5;

        /// <summary>锁定持续时间（分钟）</summary>
        private const int LockoutDurationMinutes = 30;

        /// <summary>登录设置文件名</summary>
        private const string SettingsFileName = "login_settings.json";

        #endregion

        #region 字段

        private readonly IUserService _userService;
        private int _loginFailCount = 0;
        private DateTime? _lockoutEndTime = null;
        private string _settingsFilePath;

        #endregion

        #region 可观察属性

        /// <summary>用户名</summary>
        [ObservableProperty]
        private string _username = string.Empty;

        /// <summary>密码</summary>
        [ObservableProperty]
        private string _password = string.Empty;

        /// <summary>错误提示消息</summary>
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        /// <summary>是否正在登录中</summary>
        [ObservableProperty]
        private bool _isLoggingIn = false;

        /// <summary>是否记住用户名</summary>
        [ObservableProperty]
        private bool _rememberUsername = false;

        partial void OnRememberUsernameChanged(bool value)
        {
            SaveSettings();
        }

        /// <summary>是否记住密码</summary>
        [ObservableProperty]
        private bool _rememberPassword = false;

        partial void OnRememberPasswordChanged(bool value)
        {
            SaveSettings();
        }

        /// <summary>状态提示消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        /// <summary>是否有错误</summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        #endregion

        #region 事件

        /// <summary>登录成功事件（携带用户信息和令牌）</summary>
        public event Action<UserAccount, string>? OnLoginSuccess;

        /// <summary>关闭登录窗口事件</summary>
        public event Action? OnClose;

        /// <summary>打开注册对话框事件</summary>
        public event Action? OnOpenRegisterDialog;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数——注入用户服务依赖并加载本地保存的设置
        /// </summary>
        /// <param name="userService">用户管理服务</param>
        public LoginViewModel(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            LoadSettings();
            UpdateStatusMessage();
        }

        #endregion

        #region 命令

        /// <summary>
        /// 异步执行用户登录
        /// 验证输入→检查锁定状态→调用认证服务→处理结果
        /// 登录失败达到最大次数后将账户临时锁定
        /// </summary>
        [RelayCommand]
        private async Task LoginAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "请输入用户名";
                return;
            }

            if (_lockoutEndTime.HasValue && DateTime.Now < _lockoutEndTime.Value)
            {
                var remainingMinutes = (_lockoutEndTime.Value - DateTime.Now).TotalMinutes;
                ErrorMessage = $"账户已被锁定，请在 {remainingMinutes:F1} 分钟后重试";
                return;
            }

            if (_lockoutEndTime.HasValue && DateTime.Now >= _lockoutEndTime.Value)
            {
                _lockoutEndTime = null;
                _loginFailCount = 0;
            }

            IsLoggingIn = true;
            UpdateStatusMessage();

            try
            {
                var token = await Task.Run(() => _userService.Authenticate(Username, Password));

                if (!string.IsNullOrEmpty(token))
                {
                    _loginFailCount = 0;
                    _lockoutEndTime = null;
                    ErrorMessage = string.Empty;

                    SaveSettings();

                    var user = new UserAccount
                    {
                        UserId = Username == "admin" ? "admin-001" : Guid.NewGuid().ToString(),
                        Username = Username,
                        RoleId = "admin"
                    };

                    OnLoginSuccess?.Invoke(user, token);
                }
                else
                {
                    _loginFailCount++;
                    ErrorMessage = $"用户名或密码错误（剩余尝试次数：{MaxLoginAttempts - _loginFailCount}）";
                    StatusMessage = "登录失败";

                    if (_loginFailCount >= MaxLoginAttempts)
                    {
                        _lockoutEndTime = DateTime.Now.AddMinutes(LockoutDurationMinutes);
                        ErrorMessage = $"登录失败次数过多，账户已被锁定 {LockoutDurationMinutes} 分钟";
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"登录过程发生异常：{ex.Message}";
                StatusMessage = "登录异常";
            }
            finally
            {
                IsLoggingIn = false;
                UpdateStatusMessage();
            }
        }

        /// <summary>
        /// 打开注册对话框
        /// </summary>
        [RelayCommand]
        private void OpenRegisterDialog()
        {
            OnOpenRegisterDialog?.Invoke();
        }

        /// <summary>
        /// 关闭登录窗口
        /// </summary>
        [RelayCommand]
        private void Close()
        {
            OnClose?.Invoke();
        }

        /// <summary>
        /// 退出应用程序
        /// 保存登录设置后关闭整个应用
        /// </summary>
        [RelayCommand]
        private void Exit()
        {
            SaveSettings();
            System.Windows.Application.Current.Shutdown();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 更新状态提示消息
        /// 根据登录状态和错误信息显示对应的提示文本
        /// </summary>
        private void UpdateStatusMessage()
        {
            if (IsLoggingIn)
            {
                StatusMessage = "正在登录...";
            }
            else if (!string.IsNullOrEmpty(ErrorMessage))
            {
                StatusMessage = "登录失败";
            }
            else if (HasError)
            {
                StatusMessage = "请检查输入";
            }
            else
            {
                StatusMessage = "请输入用户名和密码";
            }
        }

        /// <summary>
        /// 从本地JSON文件加载登录设置
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<LoginSettings>(json);
                    if (settings != null)
                    {
                        RememberUsername = settings.RememberUsername;
                        RememberPassword = settings.RememberPassword;
                        Username = settings.RememberUsername ? settings.Username : string.Empty;
                        Password = settings.RememberPassword ? settings.Password : string.Empty;
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 将登录设置保存到本地JSON文件
        /// 用于记住用户名和密码功能
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var settings = new LoginSettings
                {
                    Username = RememberUsername ? Username : string.Empty,
                    Password = RememberPassword ? Password : string.Empty,
                    RememberUsername = RememberUsername,
                    RememberPassword = RememberPassword
                };
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 重置登录表单的所有输入和状态
        /// </summary>
        public void Reset()
        {
            Username = string.Empty;
            Password = string.Empty;
            ErrorMessage = string.Empty;
            IsLoggingIn = false;
        }

        #endregion
    }
}