using System;
using System.Collections.Generic;
using Dapper;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Database;

// ============================================================
// 命名空间: ChargeDischargeSystem.Data.Repositories
// 功能描述: 电池协议数据仓库——封装电池协议配置的数据库操作
// 说明: 使用 Dapper 访问 SQLite 数据库。提供电池协议配置的CRUD操作。
//       电池协议配置定义了BMS通信的PGN定义、故障码映射等关键参数。
// ============================================================
namespace ChargeDischargeSystem.Data.Repositories
{
    /// <summary>
    /// 电池协议数据仓库
    /// 提供对 battery_protocol_config（电池协议配置）表的完整数据访问。
    /// 电池协议定义了不同BMS供应商的通信规范，包括PGN参数组定义
    /// 和故障码映射关系，是充放电系统与电池BMS通信的核心配置。
    /// </summary>
    public class BatteryProtocolRepository
    {
        /// <summary>
        /// 获取所有电池协议配置列表
        /// 从 battery_protocol_config 表查询全部协议配置，
        /// 按创建时间倒序排列。
        /// </summary>
        /// <returns>电池协议配置列表</returns>
        public List<BatteryProtocolConfig> GetAllBatteryProtocols()
        {
            try
            {
                const string sql = @"
                    SELECT config_id, protocol_name, bms_vendor, protocol_version,
                           pgn_definition, fault_code_map, is_active, created_at
                    FROM battery_protocol_config
                    ORDER BY created_at DESC;";

                var connection = DatabaseManager.Instance.Connection;
                var configs = connection.Query<BatteryProtocolConfig>(sql).AsList();
                return configs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolRepository.GetAllBatteryProtocols] 错误: {ex.Message}");
                return new List<BatteryProtocolConfig>();
            }
        }

        /// <summary>
        /// 根据配置ID获取单个电池协议配置
        /// </summary>
        /// <param name="configId">配置唯一标识</param>
        /// <returns>电池协议配置对象，未找到返回 null</returns>
        public BatteryProtocolConfig GetBatteryProtocolById(string configId)
        {
            try
            {
                const string sql = @"
                    SELECT config_id, protocol_name, bms_vendor, protocol_version,
                           pgn_definition, fault_code_map, is_active, created_at
                    FROM battery_protocol_config
                    WHERE config_id = @config_id;";

                var connection = DatabaseManager.Instance.Connection;
                var config = connection.QuerySingleOrDefault<BatteryProtocolConfig>(sql, new { config_id = configId });
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolRepository.GetBatteryProtocolById] 错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 插入新的电池协议配置
        /// 向 battery_protocol_config 表添加一条新的电池协议配置。使用事务保证原子性。
        /// </summary>
        /// <param name="config">电池协议配置实体</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int InsertBatteryProtocol(BatteryProtocolConfig config)
        {
            try
            {
                const string sql = @"
                    INSERT INTO battery_protocol_config (
                        config_id, protocol_name, bms_vendor, protocol_version,
                        pgn_definition, fault_code_map, is_active, created_at
                    ) VALUES (
                        @config_id, @protocol_name, @bms_vendor, @protocol_version,
                        @pgn_definition, @fault_code_map, @is_active, @created_at
                    );";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        config_id = config.ConfigId,
                        protocol_name = config.ProtocolName,
                        bms_vendor = config.BmsVendor,
                        protocol_version = config.ProtocolVersion,
                        pgn_definition = config.PgnDefinition,
                        fault_code_map = config.FaultCodeMap,
                        is_active = config.IsActive,
                        created_at = config.CreatedAt ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolRepository.InsertBatteryProtocol] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 更新电池协议配置
        /// 根据配置ID更新电池协议的PGN定义、故障码映射、启用状态等信息。
        /// </summary>
        /// <param name="config">电池协议配置实体（需包含有效的 ConfigId）</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int UpdateBatteryProtocol(BatteryProtocolConfig config)
        {
            try
            {
                const string sql = @"
                    UPDATE battery_protocol_config SET
                        protocol_name = @protocol_name,
                        bms_vendor = @bms_vendor,
                        protocol_version = @protocol_version,
                        pgn_definition = @pgn_definition,
                        fault_code_map = @fault_code_map,
                        is_active = @is_active
                    WHERE config_id = @config_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new
                    {
                        config_id = config.ConfigId,
                        protocol_name = config.ProtocolName,
                        bms_vendor = config.BmsVendor,
                        protocol_version = config.ProtocolVersion,
                        pgn_definition = config.PgnDefinition,
                        fault_code_map = config.FaultCodeMap,
                        is_active = config.IsActive
                    }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolRepository.UpdateBatteryProtocol] 错误: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 删除指定的电池协议配置
        /// 从 battery_protocol_config 表中删除对应配置。
        /// </summary>
        /// <param name="configId">配置唯一标识</param>
        /// <returns>受影响的行数，失败时返回 -1</returns>
        public int DeleteBatteryProtocol(string configId)
        {
            try
            {
                const string sql = @"
                    DELETE FROM battery_protocol_config
                    WHERE config_id = @config_id;";

                var connection = DatabaseManager.Instance.Connection;
                using (var transaction = connection.BeginTransaction())
                {
                    int result = connection.Execute(sql, new { config_id = configId }, transaction);
                    transaction.Commit();
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolRepository.DeleteBatteryProtocol] 错误: {ex.Message}");
                return -1;
            }
        }
    }
}
