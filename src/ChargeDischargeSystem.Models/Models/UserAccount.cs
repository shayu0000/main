// ============================================================
// 文件名: UserAccount.cs
// 用途: 定义用户账户、角色、权限和会话相关的数据模型实体
// ============================================================

// 引入系统基础类型库
using System;
// 引入泛型集合库
using System.Collections.Generic;

// 核心业务模型命名空间
namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 用户账户实体类，对应数据库表 user_account
    /// 存储系统用户的基本账户信息，包括认证凭据和账户状态
    /// </summary>
    public class UserAccount
    {
        /// <summary>用户ID，主键，采用UUID格式确保全局唯一</summary>
        public string UserId { get; set; }
        /// <summary>用户名，唯一索引，用于系统登录</summary>
        public string Username { get; set; }
        /// <summary>密码哈希值，使用SHA-256加盐哈希算法生成</summary>
        public string PasswordHash { get; set; }
        /// <summary>密码盐值，用于增强密码安全性，防止彩虹表攻击</summary>
        public string Salt { get; set; }
        /// <summary>显示名称，用于界面展示的用户昵称</summary>
        public string DisplayName { get; set; }
        /// <summary>电子邮箱地址，用于密码找回和通知</summary>
        public string Email { get; set; }
        /// <summary>联系电话号码</summary>
        public string Phone { get; set; }
        /// <summary>角色ID，外键关联user_role表</summary>
        public string RoleId { get; set; }
        /// <summary>角色名称，用于界面显示，非数据库持久化字段</summary>
        public string RoleName { get; set; }
        /// <summary>账户状态: active活跃/disabled禁用/locked锁定</summary>
        public string Status { get; set; } = "active";
        /// <summary>最后登录时间，格式为yyyy-MM-dd HH:mm:ss</summary>
        public string LastLoginTime { get; set; }
        /// <summary>登录失败次数累计，超过阈值将锁定账户</summary>
        public int LoginFailCount { get; set; }
        /// <summary>账户创建时间，默认为当前时间</summary>
        public string CreatedAt { get; set; }
        /// <summary>账户最后更新时间</summary>
        public string UpdatedAt { get; set; }
    }

    /// <summary>
    /// 用户角色实体类，对应数据库表 user_role
    /// 定义系统角色类型及其拥有的权限集合
    /// </summary>
    public class UserRole
    {
        /// <summary>角色ID，主键，唯一标识一个角色</summary>
        public string RoleId { get; set; }
        /// <summary>角色名称，唯一取值: admin管理员/operator操作员/engineer工程师/viewer查看者</summary>
        public string RoleName { get; set; }
        /// <summary>角色描述信息，说明角色的功能和权限范围</summary>
        public string Description { get; set; }
        /// <summary>角色创建时间</summary>
        public string CreatedAt { get; set; }
        /// <summary>角色拥有的权限集合，导航属性用于EF关联查询</summary>
        public List<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    /// <summary>
    /// 权限实体类，对应数据库表 permission
    /// 定义系统中的操作权限项，支持细粒度访问控制
    /// </summary>
    public class Permission
    {
        /// <summary>权限ID，主键，唯一标识一个权限</summary>
        public string PermissionId { get; set; }
        /// <summary>权限名称，唯一取值如device:read/calibration:execute</summary>
        public string PermissionName { get; set; }
        /// <summary>资源类型: device设备/calibration校准/protocol协议/firmware固件/fault故障/report报告/user用户/data数据/system系统</summary>
        public string Resource { get; set; }
        /// <summary>操作类型: read读取/write写入/execute执行/delete删除</summary>
        public string Action { get; set; }
        /// <summary>权限描述信息，详细说明该权限的用途和范围</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// 角色-权限关联实体类，对应数据库表 role_permission
    /// 建立角色与权限的多对多关联关系
    /// </summary>
    public class RolePermission
    {
        /// <summary>角色ID，联合主键之一，外键关联user_role表</summary>
        public string RoleId { get; set; }
        /// <summary>权限ID，联合主键之一，外键关联permission表</summary>
        public string PermissionId { get; set; }
    }

    /// <summary>
    /// 用户会话实体类，对应数据库表 user_session
    /// 记录用户登录会话信息，用于会话管理和安全审计
    /// </summary>
    public class UserSession
    {
        /// <summary>会话ID，主键，唯一标识一次登录会话</summary>
        public string SessionId { get; set; }
        /// <summary>用户ID，外键关联user_account表</summary>
        public string UserId { get; set; }
        /// <summary>登录时间，记录用户登录的时间点</summary>
        public string LoginTime { get; set; }
        /// <summary>登出时间，记录用户退出或会话过期的时间</summary>
        public string LogoutTime { get; set; }
        /// <summary>登录IP地址，用于安全审计和地理位置追踪</summary>
        public string IpAddress { get; set; }
        /// <summary>会话令牌哈希值，用于验证会话有效性</summary>
        public string TokenHash { get; set; }
        /// <summary>会话是否活跃: 1表示活跃可用，0表示已失效</summary>
        public int IsActive { get; set; } = 1;
    }
}
