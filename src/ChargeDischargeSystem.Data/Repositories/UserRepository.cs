using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 用户管理数据仓库——封装用户、角色、权限和会话的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供用户CRUD、登录信息管理、
//       账户锁定、角色权限查询以及会话生命周期管理等功能。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 用户管理数据仓库
    /// 提供对 user_account（用户账户）、user_role（用户角色）、
    /// permission（权限）、role_permission（角色权限关联）以及
    /// user_session（用户会话）五张表的完整数据访问。
    /// </summary>
    public class UserRepository
    {
        #region 用户账户管理

        /// <summary>
        /// 根据用户名获取用户信息
        /// 通过唯一用户名查询用户账户记录，常用于登录验证。
        /// </summary>
        /// <param name="username">用户名（唯一）</param>
        /// <returns>用户账户对象，未找到返回 null</returns>
        public UserAccount GetUserByUsername(string username)
        {
            try
            {
                const string sql = @"
                    SELECT user_id, username, password_hash, salt,
                           display_name, email, phone, role_id,
                           status, last_login_time, login_fail_count,
                           created_at, updated_at
                    FROM user_account
                    WHERE username = @username;";

                var connection = DatabaseManager.Instance.Connection;
                var user = connection.QuerySingleOrDefault<UserAccount>(sql, new { username = username });
                return user;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.GetUserByUsername] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据用户ID获取用户信息
        /// </summary>
        /// <param name="userId">用户唯一标识（UUID格式）</param>
        /// <returns>用户账户对象，未找到返回 null</returns>
        public UserAccount GetUserById(string userId)
        {
            try
            {
                const string sql = @"
                    SELECT user_id, username, password_hash, salt,
                           display_name, email, phone, role_id,
                           status, last_login_time, login_fail_count,
                           created_at, updated_at
                    FROM user_account
                    WHERE user_id = @user_id;";

                var connection = DatabaseManager.Instance.Connection;
                var user = connection.QuerySingleOrDefault<UserAccount>(sql, new { user_id = userId });
                return user;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.GetUserById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有用户列表
        /// 查询全部用户账户，按创建时间倒序排列。
        /// </summary>
        /// <returns>用户账户列表</returns>
        public List<UserAccount> GetAllUsers()
        {
            try
            {
                const string sql = @"
                    SELECT user_id, username, password_hash, salt,
                           display_name, email, phone, role_id,
                           status, last_login_time, login_fail_count,
                           created_at, updated_at
                    FROM user_account
                    ORDER BY created_at DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var users = connection.Query<UserAccount>(sql).AsList();
                return users;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.GetAllUsers] 错误: {ex.Message}");
                return new List<UserAccount>();
            }
        }

        /// <summary>
        /// 插入新用户
        /// 向 user_account 表添加一个新用户。使用事务保证原子性。
        /// </summary>
        /// <param name="user">用户账户实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertUser(UserAccount user)
        {
            try
            {
                const string sql = @"
                    INSERT INTO user_account (
                        user_id, username, password_hash, salt,
                        display_name, email, phone, role_id,
                        status, last_login_time, login_fail_count,
                        created_at, updated_at
                    ) VALUES (
                        @user_id, @username, @password_hash, @salt,
                        @display_name, @email, @phone, @role_id,
                        @status, @last_login_time, @login_fail_count,
                        @created_at, @updated_at
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        user_id = user.UserId,
                        username = user.Username,
                        password_hash = user.PasswordHash,
                        salt = user.Salt,
                        display_name = user.DisplayName,
                        email = user.Email,
                        phone = user.Phone,
                        role_id = user.RoleId,
                        status = user.Status ?? "active",
                        last_login_time = user.LastLoginTime,
                        login_fail_count = user.LoginFailCount,
                        created_at = user.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                        updated_at = user.UpdatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.InsertUser] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新用户信息
        /// 根据用户ID更新用户的显示名称、邮箱、电话、角色和状态等信息。
        /// 注意：不会更新密码相关字段，密码修改应通过专门的方法处理。
        /// </summary>
        /// <param name="user">用户账户实体（需包含有效的 UserId）</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateUser(UserAccount user)
        {
            try
            {
                const string sql = @"
                    UPDATE user_account SET
                        display_name = @display_name,
                        email = @email,
                        phone = @phone,
                        role_id = @role_id,
                        status = @status,
                        updated_at = @updated_at
                    WHERE user_id = @user_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        user_id = user.UserId,
                        display_name = user.DisplayName,
                        email = user.Email,
                        phone = user.Phone,
                        role_id = user.RoleId,
                        status = user.Status,
                        updated_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.UpdateUser] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 删除指定用户
        /// 从 user_account 表中删除用户。注意：应确保没有外部引用。
        /// </summary>
        /// <param name="userId">用户唯一标识</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int DeleteUser(string userId)
        {
            try
            {
                const string sql = @"
                    DELETE FROM user_account
                    WHERE user_id = @user_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new { user_id = userId }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.DeleteUser] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新用户登录信息
        /// 在用户成功登录后更新最后登录时间和可选的IP地址。
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="loginTime">登录时间字符串</param>
        /// <param name="ip">登录IP地址，可选</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateLoginInfo(string userId, string loginTime, string ip = null)
        {
            try
            {
                const string sql = @"
                    UPDATE user_account SET
                        last_login_time = @last_login_time
                    WHERE user_id = @user_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    user_id = userId,
                    last_login_time = loginTime
                });

                // IP地址通常记录在会话表中，这里不做处理
                // 如有需要可扩展此方法
                _ = ip;

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.UpdateLoginInfo] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 递增登录失败次数
        /// 当用户登录失败时调用，将 login_fail_count 加 1。
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int IncrementLoginFailCount(string userId)
        {
            try
            {
                const string sql = @"
                    UPDATE user_account SET
                        login_fail_count = login_fail_count + 1
                    WHERE user_id = @user_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new { user_id = userId });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.IncrementLoginFailCount] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 重置登录失败次数
        /// 当用户成功登录后调用，将 login_fail_count 重置为 0。
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int ResetLoginFailCount(string userId)
        {
            try
            {
                const string sql = @"
                    UPDATE user_account SET
                        login_fail_count = 0
                    WHERE user_id = @user_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new { user_id = userId });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.ResetLoginFailCount] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 锁定用户账户
        /// 将用户状态设置为 locked，通常因为连续登录失败超过阈值。
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int LockUserAccount(string userId)
        {
            try
            {
                const string sql = @"
                    UPDATE user_account SET
                        status = 'locked',
                        updated_at = @updated_at
                    WHERE user_id = @user_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        user_id = userId,
                        updated_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.LockUserAccount] 错误: {ex.Message}");
                return -1;
            }
        }

        #endregion

        #region 用户会话管理

        /// <summary>
        /// 创建用户会话
        /// 在用户成功登录后向 user_session 表创建一条新会话记录。
        /// </summary>
        /// <param name="session">用户会话实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int CreateSession(UserSession session)
        {
            try
            {
                const string sql = @"
                    INSERT INTO user_session (
                        session_id, user_id, login_time, logout_time,
                        ip_address, token_hash, is_active
                    ) VALUES (
                        @session_id, @user_id, @login_time, @logout_time,
                        @ip_address, @token_hash, @is_active
                    );";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    session_id = session.SessionId,
                    user_id = session.UserId,
                    login_time = session.LoginTime ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    logout_time = session.LogoutTime,
                    ip_address = session.IpAddress,
                    token_hash = session.TokenHash,
                    is_active = session.IsActive
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.CreateSession] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 结束用户会话
        /// 将指定的用户会话标记为非活跃，并记录登出时间。
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int EndSession(string sessionId)
        {
            try
            {
                const string sql = @"
                    UPDATE user_session SET
                        logout_time = @logout_time,
                        is_active = 0
                    WHERE session_id = @session_id;";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    session_id = sessionId,
                    logout_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.EndSession] 错误: {ex.Message}");
                return -1;
            }
        }

        #endregion

        #region 角色与权限管理

        /// <summary>
        /// 获取所有角色列表
        /// 查询 user_role 表中的全部角色定义。
        /// </summary>
        /// <returns>角色列表</returns>
        public List<UserRole> GetAllRoles()
        {
            try
            {
                const string sql = @"
                    SELECT role_id, role_name, description, created_at
                    FROM user_role
                    ORDER BY created_at ASC;";

                var connection = DatabaseManager.Instance.Connection;
                var roles = connection.Query<UserRole>(sql).AsList();
                return roles;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.GetAllRoles] 错误: {ex.Message}");
                return new List<UserRole>();
            }
        }

        /// <summary>
        /// 根据角色ID获取该角色关联的所有权限
        /// 通过 role_permission 关联表查询权限列表。
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns>权限列表</returns>
        public List<Permission> GetPermissionsByRoleId(string roleId)
        {
            try
            {
                const string sql = @"
                    SELECT p.permission_id, p.permission_name, p.resource, p.action, p.description
                    FROM permission p
                    INNER JOIN role_permission rp ON p.permission_id = rp.permission_id
                    WHERE rp.role_id = @role_id
                    ORDER BY p.resource, p.action;";

                var connection = DatabaseManager.Instance.Connection;
                var permissions = connection.Query<Permission>(sql, new { role_id = roleId }).AsList();
                return permissions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.GetPermissionsByRoleId] 错误: {ex.Message}");
                return new List<Permission>();
            }
        }

        /// <summary>
        /// 获取指定用户的所有权限
        /// 根据用户的角色ID，通过 role_permission 关联表返回该用户拥有的完整权限集合。
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>权限列表；用户不存在或角色无权限时返回空列表</returns>
        public List<Permission> GetUserPermissions(string userId)
        {
            try
            {
                const string sql = @"
                    SELECT p.permission_id, p.permission_name, p.resource, p.action, p.description
                    FROM permission p
                    INNER JOIN role_permission rp ON p.permission_id = rp.permission_id
                    INNER JOIN user_account u ON u.role_id = rp.role_id
                    WHERE u.user_id = @user_id
                    ORDER BY p.resource, p.action;";

                var connection = DatabaseManager.Instance.Connection;
                var permissions = connection.Query<Permission>(sql, new { user_id = userId }).AsList();
                return permissions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserRepository.GetUserPermissions] 错误: {ex.Message}");
                return new List<Permission>();
            }
        }

        #endregion
    }
}
