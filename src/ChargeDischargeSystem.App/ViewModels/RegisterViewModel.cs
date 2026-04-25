using System;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Core.Services;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 用户注册视图模型
// 说明: 处理新用户注册流程，包括表单验证、密码加密和数据库写入
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 用户注册视图模型
    /// 负责新用户注册的全流程管理：
    ///   1. 用户输入表单的验证（用户名、密码、确认密码等）
    ///   2. 密码加盐哈希处理（SHA256）
    ///   3. 用户信息写入数据库
    ///   4. 注册成功/失败的事件通知
    /// </summary>
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly IUserService _userService;

        /// <summary>用户名</summary>
        [ObservableProperty]
        private string _username = string.Empty;

        /// <summary>密码</summary>
        [ObservableProperty]
        private string _password = string.Empty;

        /// <summary>确认密码</summary>
        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        /// <summary>显示名称</summary>
        [ObservableProperty]
        private string _displayName = string.Empty;

        /// <summary>部门</summary>
        [ObservableProperty]
        private string _department = string.Empty;

        /// <summary>联系电话</summary>
        [ObservableProperty]
        private string _phone = string.Empty;

        /// <summary>错误提示消息</summary>
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        /// <summary>注册成功事件</summary>
        public event Action? OnRegisterSuccess;

        /// <summary>取消注册事件</summary>
        public event Action? OnCancel;

        /// <summary>
        /// 构造函数——注入用户服务依赖
        /// </summary>
        /// <param name="userService">用户管理服务</param>
        public RegisterViewModel(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        /// <summary>
        /// 执行用户注册
        /// 验证表单输入→生成密码盐值→计算哈希→写入数据库
        /// </summary>
        [RelayCommand]
        private void Register()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "请输入用户名";
                return;
            }

            if (Username.Length < 3)
            {
                ErrorMessage = "用户名长度不能少于3个字符";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "请输入密码";
                return;
            }

            if (Password.Length < 6)
            {
                ErrorMessage = "密码长度不能少于6个字符";
                return;
            }

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "两次输入的密码不一致";
                return;
            }

            try
            {
                var salt = GenerateSalt();
                var passwordHash = HashPassword(Password, salt);

                var userId = Guid.NewGuid().ToString();
                var roleId = "ROLE_OPERATOR";

                var connectionString = $"Data Source={System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "mw_scada.db")}";
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO user_account (user_id, username, password_hash, salt, display_name, phone, role_id, status, created_at, updated_at)
                            VALUES (@user_id, @username, @password_hash, @salt, @display_name, @phone, @role_id, 'active', datetime('now'), datetime('now'))";
                        cmd.Parameters.AddWithValue("@user_id", userId);
                        cmd.Parameters.AddWithValue("@username", Username);
                        cmd.Parameters.AddWithValue("@password_hash", passwordHash);
                        cmd.Parameters.AddWithValue("@salt", salt);
                        cmd.Parameters.AddWithValue("@display_name", string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName);
                        cmd.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(Phone) ? "" : Phone);
                        cmd.Parameters.AddWithValue("@role_id", roleId);
                        cmd.ExecuteNonQuery();
                    }
                }

                OnRegisterSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UNIQUE constraint failed: user_account.username"))
                {
                    ErrorMessage = "注册失败：用户名已存在，请使用其他用户名";
                }
                else
                {
                    ErrorMessage = $"注册失败：{ex.Message}";
                }
            }
        }

        /// <summary>
        /// 取消注册，触发取消事件返回登录界面
        /// </summary>
        [RelayCommand]
        private void Cancel()
        {
            OnCancel?.Invoke();
        }

        /// <summary>
        /// 生成密码盐值（32字节加密随机数）
        /// </summary>
        /// <returns>Base64编码的盐值字符串</returns>
        private string GenerateSalt()
        {
            var saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// 使用SHA256算法对密码加盐哈希
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="salt">盐值字符串</param>
        /// <returns>Base64编码的哈希值</returns>
        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var combinedBytes = Encoding.UTF8.GetBytes(password + salt);
                var hashBytes = sha256.ComputeHash(combinedBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}