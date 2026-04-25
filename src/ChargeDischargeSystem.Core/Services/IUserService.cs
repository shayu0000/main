// ============================================================
// 文件名: IUserService.cs
// 用途: 用户管理服务接口，提供用户认证、账户管理和权限控制功能
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Core.Models;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 用户管理服务接口
    /// 负责系统用户的认证、账户管理和权限控制：
    ///   1. 用户登录/登出与会话管理
    ///   2. 用户账户的创建、更新、删除
    ///   3. 密码修改与复杂度验证
    ///   4. 角色与权限查询
    ///   5. 登录失败次数限制和账户锁定
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// 用户登录认证，验证用户名和密码，成功后返回会话令牌
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">明文密码</param>
        /// <returns>会话令牌（鉴权成功），失败返回null</returns>
        string Authenticate(string username, string password);

        /// <summary>
        /// 创建新用户，验证密码复杂度并创建用户账户
        /// </summary>
        /// <param name="username">用户名（唯一）</param>
        /// <param name="password">明文密码</param>
        /// <param name="roleId">角色ID</param>
        /// <param name="displayName">显示名称</param>
        /// <param name="email">电子邮箱（可选）</param>
        /// <returns>创建的用户ID，失败返回null</returns>
        string CreateUser(string username, string password, string roleId, string displayName, string email = null);

        /// <summary>
        /// 更新用户信息，修改用户的显示名称、邮箱、角色等非敏感信息
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="updates">要更新的字段字典</param>
        /// <returns>更新是否成功</returns>
        bool UpdateUser(string userId, Dictionary<string, object> updates);

        /// <summary>
        /// 删除用户账户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>删除是否成功</returns>
        bool DeleteUser(string userId);

        /// <summary>
        /// 检查用户是否拥有指定权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="permissionName">权限名称（如 device:read / calibration:execute）</param>
        /// <returns>是否拥有该权限</returns>
        bool CheckPermission(string userId, string permissionName);

        /// <summary>
        /// 列出所有用户
        /// </summary>
        /// <returns>用户账户列表</returns>
        List<UserAccount> ListUsers();

        /// <summary>
        /// 列出所有角色
        /// </summary>
        /// <returns>角色列表</returns>
        List<UserRole> ListRoles();

        /// <summary>
        /// 修改用户密码，验证旧密码正确性后更新为新密码
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="oldPassword">旧密码</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改是否成功</returns>
        bool ChangePassword(string userId, string oldPassword, string newPassword);

        /// <summary>
        /// 用户登出，使指定会话失效
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>登出是否成功</returns>
        bool Logout(string sessionId);
    }
}
