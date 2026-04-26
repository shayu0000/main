using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Core.Services;
using Microsoft.Data.Sqlite;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 用户管理视图模型
// 说明: 管理用户账户的增删改查、角色分配和密码管理
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 用户管理视图模型
    /// 负责系统用户账户的全生命周期管理：
    ///   1. 用户列表的加载和刷新
    ///   2. 新增、编辑、删除用户
    ///   3. 用户角色分配
    ///   4. 密码修改
    ///   5. 表单验证和状态反馈
    /// </summary>
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly IUserService _userService;

        /// <summary>用户列表</summary>
        [ObservableProperty]
        private ObservableCollection<UserAccount> _userList = new ObservableCollection<UserAccount>();

        /// <summary>当前选中的用户</summary>
        [ObservableProperty]
        private UserAccount? _selectedUser;

        /// <summary>角色列表</summary>
        [ObservableProperty]
        private ObservableCollection<UserRole> _roleList = new ObservableCollection<UserRole>();

        /// <summary>是否正在编辑</summary>
        [ObservableProperty]
        private bool _isEditing;

        /// <summary>编辑对话框标题</summary>
        [ObservableProperty]
        private string _editTitle = "新增用户";

        /// <summary>编辑中的用户名</summary>
        [ObservableProperty]
        private string _editUsername = string.Empty;

        /// <summary>编辑中的密码</summary>
        [ObservableProperty]
        private string _editPassword = string.Empty;

        /// <summary>编辑中的显示名称</summary>
        [ObservableProperty]
        private string _editDisplayName = string.Empty;

        /// <summary>编辑中的邮箱</summary>
        [ObservableProperty]
        private string _editEmail = string.Empty;

        /// <summary>编辑中的联系电话</summary>
        [ObservableProperty]
        private string _editPhone = string.Empty;

        /// <summary>编辑中的角色ID</summary>
        [ObservableProperty]
        private string _editRoleId = string.Empty;

        /// <summary>状态提示消息</summary>
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        /// <summary>
        /// 构造函数——注入用户服务依赖并初始化数据
        /// </summary>
        /// <param name="userService">用户管理服务</param>
        public UserManagementViewModel(IUserService userService)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            LoadRoles();
            LoadUsers();
        }

        /// <summary>
        /// 新增用户——初始化编辑表单并进入编辑模式
        /// </summary>
        [RelayCommand]
        private void AddUser()
        {
            EditTitle = "新增用户";
            EditUsername = string.Empty;
            EditPassword = string.Empty;
            EditDisplayName = string.Empty;
            EditEmail = string.Empty;
            EditPhone = string.Empty;
            EditRoleId = RoleList.FirstOrDefault()?.RoleId ?? "ROLE_OPERATOR";
            IsEditing = true;
            StatusMessage = string.Empty;
        }

        /// <summary>
        /// 编辑用户——加载选中用户的信息到编辑表单
        /// </summary>
        [RelayCommand]
        private void EditUser()
        {
            if (SelectedUser == null)
            {
                StatusMessage = "请先选择要编辑的用户";
                return;
            }
            EditTitle = "编辑用户";
            EditUsername = SelectedUser.Username;
            EditPassword = string.Empty;
            EditDisplayName = SelectedUser.DisplayName ?? string.Empty;
            EditEmail = SelectedUser.Email ?? string.Empty;
            EditPhone = SelectedUser.Phone ?? string.Empty;
            EditRoleId = SelectedUser.RoleId ?? "ROLE_OPERATOR";
            IsEditing = true;
            StatusMessage = string.Empty;
        }

        /// <summary>
        /// 删除用户——从系统和列表中移除选中用户
        /// </summary>
        [RelayCommand]
        private void DeleteUser()
        {
            if (SelectedUser == null)
            {
                StatusMessage = "请先选择要删除的用户";
                return;
            }

            try
            {
                var success = _userService.DeleteUser(SelectedUser.UserId);
                if (success)
                {
                    UserList.Remove(SelectedUser);
                    SelectedUser = null;
                    IsEditing = false;
                    StatusMessage = "用户已删除";
                }
                else
                {
                    StatusMessage = "删除失败：无法删除用户";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 保存用户——新增或更新用户信息
        /// 含表单验证、密码哈希、角色分配等完整流程
        /// </summary>
        [RelayCommand]
        private void SaveUser()
        {
            if (string.IsNullOrWhiteSpace(EditUsername))
            {
                StatusMessage = "用户名不能为空";
                return;
            }

            if (EditUsername.Length < 3)
            {
                StatusMessage = "用户名长度不能少于3个字符";
                return;
            }

            if (EditTitle == "新增用户" && string.IsNullOrWhiteSpace(EditPassword))
            {
                StatusMessage = "密码不能为空";
                return;
            }

            if (!string.IsNullOrWhiteSpace(EditPassword) && EditPassword.Length < 6)
            {
                StatusMessage = "密码长度不能少于6个字符";
                return;
            }

            try
            {
                if (EditTitle == "新增用户")
                {
                    var userId = _userService.CreateUser(EditUsername, EditPassword, EditRoleId, string.IsNullOrWhiteSpace(EditDisplayName) ? EditUsername : EditDisplayName, string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        StatusMessage = "用户创建成功";
                    }
                    else
                    {
                        StatusMessage = "创建失败：无法创建用户";
                    }
                }
                else
                {
                    if (SelectedUser == null) return;

                    var updates = new Dictionary<string, object>
                    {
                        { "display_name", string.IsNullOrWhiteSpace(EditDisplayName) ? EditUsername : EditDisplayName },
                        { "email", string.IsNullOrWhiteSpace(EditEmail) ? "" : EditEmail },
                        { "phone", string.IsNullOrWhiteSpace(EditPhone) ? "" : EditPhone },
                        { "role_id", EditRoleId }
                    };

                    var success = _userService.UpdateUser(SelectedUser.UserId, updates);
                    if (success)
                    {
                        if (!string.IsNullOrWhiteSpace(EditPassword))
                        {
                            _userService.ChangePassword(SelectedUser.UserId, "", EditPassword);
                        }
                        StatusMessage = "用户信息已更新";
                    }
                    else
                    {
                        StatusMessage = "更新失败：无法更新用户信息";
                    }
                }

                IsEditing = false;
                LoadUsers();
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 取消编辑——退出编辑模式并清除状态消息
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
            StatusMessage = string.Empty;
        }

        /// <summary>
        /// 刷新——重新加载用户和角色列表
        /// </summary>
        [RelayCommand]
        private void Refresh()
        {
            LoadUsers();
            LoadRoles();
            StatusMessage = "已刷新";
        }

        /// <summary>
        /// 加载用户列表——从SQLite读取用户数据并解析角色名称
        /// </summary>
        private void LoadUsers()
        {
            try
            {
                var roleMap = RoleList.ToDictionary(r => r.RoleId, r => r.RoleName);
                var users = _userService.ListUsers();
                UserList.Clear();
                foreach (var user in users)
                {
                    roleMap.TryGetValue(user.RoleId ?? "", out var roleName);
                    user.RoleName = roleName ?? user.RoleId;
                    UserList.Add(user);
                }
                StatusMessage = $"已加载 {UserList.Count} 个用户";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载用户失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 加载角色列表
        /// </summary>
        private void LoadRoles()
        {
            try
            {
                var roles = _userService.ListRoles();
                RoleList.Clear();
                foreach (var role in roles)
                {
                    RoleList.Add(role);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载角色失败：{ex.Message}";
            }
        }
    }
}