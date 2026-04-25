using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 协议管理数据仓库——封装通信协议配置及协议命令的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供协议配置的CRUD、
//       协议命令的管理、启用状态的控制等功能。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 协议管理数据仓库
    /// 提供对 protocol_config（协议配置）和 protocol_command（协议命令）
    /// 两张表的完整数据访问。协议定义了设备或电池的通信规范，
    /// 每条协议包含多个通信命令。
    /// </summary>
    public class ProtocolRepository
    {
        /// <summary>
        /// 获取所有协议配置列表
        /// 从 protocol_config 表中查询全部协议，按加载时间倒序排列。
        /// </summary>
        /// <returns>协议配置列表</returns>
        public List<ProtocolConfig> GetAllProtocols()
        {
            try
            {
                const string sql = @"
                    SELECT protocol_id, protocol_name, protocol_type, protocol_version,
                           config_content, is_active, loaded_at, updated_at
                    FROM protocol_config
                    ORDER BY loaded_at DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var protocols = connection.Query<ProtocolConfig>(sql).AsList();
                return protocols;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolRepository.GetAllProtocols] 错误: {ex.Message}");
                return new List<ProtocolConfig>();
            }
        }

        /// <summary>
        /// 根据协议ID获取单个协议配置
        /// 同时加载该协议关联的所有命令列表。
        /// </summary>
        /// <param name="protocolId">协议唯一标识</param>
        /// <returns>协议配置对象（含命令集合），未找到返回 null</returns>
        public ProtocolConfig GetProtocolById(string protocolId)
        {
            try
            {
                const string sql = @"
                    SELECT protocol_id, protocol_name, protocol_type, protocol_version,
                           config_content, is_active, loaded_at, updated_at
                    FROM protocol_config
                    WHERE protocol_id = @protocol_id;";

                var connection = DatabaseManager.Instance.Connection;
                var protocol = connection.QuerySingleOrDefault<ProtocolConfig>(sql, new { protocol_id = protocolId });

                if (protocol != null)
                {
                    protocol.Commands = GetProtocolCommands(protocolId);
                }

                return protocol;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolRepository.GetProtocolById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 插入新的协议配置
        /// 向 protocol_config 表添加一条新的协议配置记录。使用事务保证原子性。
        /// </summary>
        /// <param name="config">协议配置实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertProtocol(ProtocolConfig config)
        {
            try
            {
                const string sql = @"
                    INSERT INTO protocol_config (
                        protocol_id, protocol_name, protocol_type, protocol_version,
                        config_content, is_active, loaded_at, updated_at
                    ) VALUES (
                        @protocol_id, @protocol_name, @protocol_type, @protocol_version,
                        @config_content, @is_active, @loaded_at, @updated_at
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        protocol_id = config.ProtocolId,
                        protocol_name = config.ProtocolName,
                        protocol_type = config.ProtocolType,
                        protocol_version = config.ProtocolVersion,
                        config_content = config.ConfigContent,
                        is_active = config.IsActive,
                        loaded_at = config.LoadedAt,
                        updated_at = config.UpdatedAt
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolRepository.InsertProtocol] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新协议配置
        /// 根据协议ID更新协议的配置内容、版本和启用状态等信息。
        /// </summary>
        /// <param name="config">协议配置实体（需包含有效的 ProtocolId）</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateProtocol(ProtocolConfig config)
        {
            try
            {
                const string sql = @"
                    UPDATE protocol_config SET
                        protocol_name = @protocol_name,
                        protocol_type = @protocol_type,
                        protocol_version = @protocol_version,
                        config_content = @config_content,
                        is_active = @is_active,
                        updated_at = @updated_at
                    WHERE protocol_id = @protocol_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        protocol_id = config.ProtocolId,
                        protocol_name = config.ProtocolName,
                        protocol_type = config.ProtocolType,
                        protocol_version = config.ProtocolVersion,
                        config_content = config.ConfigContent,
                        is_active = config.IsActive,
                        updated_at = config.UpdatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolRepository.UpdateProtocol] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 删除指定协议配置
        /// 从 protocol_config 表中删除对应协议。注意：删除前应确保没有外部引用。
        /// </summary>
        /// <param name="protocolId">协议唯一标识</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int DeleteProtocol(string protocolId)
        {
            try
            {
                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    // 先删除该协议下的所有命令
                    const string deleteCommandsSql = @"
                        DELETE FROM protocol_command
                        WHERE protocol_id = @protocol_id;";
                    connection.Execute(deleteCommandsSql, new { protocol_id = protocolId }, transaction);

                    // 再删除协议本身
                    const string deleteProtocolSql = @"
                        DELETE FROM protocol_config
                        WHERE protocol_id = @protocol_id;";
                    int result = connection.Execute(deleteProtocolSql, new { protocol_id = protocolId }, transaction);

                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolRepository.DeleteProtocol] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 获取指定协议的所有命令
        /// 根据协议ID查询 protocol_command 表中属于该协议的所有命令。
        /// 结果按命令码升序排列。
        /// </summary>
        /// <param name="protocolId">协议唯一标识</param>
        /// <returns>协议命令列表</returns>
        public List<ProtocolCommand> GetProtocolCommands(string protocolId)
        {
            try
            {
                const string sql = @"
                    SELECT command_id, protocol_id, command_name, command_code,
                           request_params, response_params, timeout_ms, retry_count, description
                    FROM protocol_command
                    WHERE protocol_id = @protocol_id
                    ORDER BY command_code ASC;";

                var connection = DatabaseManager.Instance.Connection;
                var commands = connection.Query<ProtocolCommand>(sql, new { protocol_id = protocolId }).AsList();
                return commands;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolRepository.GetProtocolCommands] 错误: {ex.Message}");
                return new List<ProtocolCommand>();
            }
        }

        /// <summary>
        /// 插入新的协议命令
        /// 向 protocol_command 表添加一条命令定义。
        /// </summary>
        /// <param name="cmd">协议命令实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertProtocolCommand(ProtocolCommand cmd)
        {
            try
            {
                const string sql = @"
                    INSERT INTO protocol_command (
                        command_id, protocol_id, command_name, command_code,
                        request_params, response_params, timeout_ms, retry_count, description
                    ) VALUES (
                        @command_id, @protocol_id, @command_name, @command_code,
                        @request_params, @response_params, @timeout_ms, @retry_count, @description
                    );";

                var connection = DatabaseManager.Instance.Connection;
                int result = connection.Execute(sql, new
                {
                    command_id = cmd.CommandId,
                    protocol_id = cmd.ProtocolId,
                    command_name = cmd.CommandName,
                    command_code = cmd.CommandCode,
                    request_params = cmd.RequestParams,
                    response_params = cmd.ResponseParams,
                    timeout_ms = cmd.TimeoutMs,
                    retry_count = cmd.RetryCount,
                    description = cmd.Description
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolRepository.InsertProtocolCommand] 错误: {ex.Message}");
                return -1;
            }
        }
    }
}
