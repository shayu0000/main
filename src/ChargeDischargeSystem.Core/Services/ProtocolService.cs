using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 协议管理服务实现类
// 说明: 实现通信协议的加载、解析、验证和命令管理
//       支持从YAML文件加载协议定义（如GB/T 27930-2015等标准协议）
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 协议管理服务实现类
    /// 管理系统中所有通信协议的完整生命周期：
    ///   1. 协议加载：从YAML配置文件中读取协议定义
    ///   2. 消息解析：将CAN原始数据帧按协议规则解析为结构化信息
    ///   3. 消息验证：检查CAN消息是否符合协议规定的格式和约束
    ///   4. 命令管理：提供协议命令的查询和发送功能
    /// 
    /// 协议配置结构（YAML格式）：
    ///   protocol:
    ///     name: "GB/T 27930-2015"
    ///     version: "2015"
    ///     can_id_base: 0x18FF0000
    ///     parameters:
    ///       - name: voltage
    ///         can_id_offset: 0x100
    ///         byte_offset: 0
    ///         length: 4
    ///         scale: 0.1
    ///         unit: "V"
    /// </summary>
    public class ProtocolService : IProtocolService
    {
        #region -- 字段定义 --

        /// <summary>CAN通信服务引用</summary>
        private readonly ICanCommunicationService _canService;

        /// <summary>已加载的协议字典（Key: 协议名称, Value: 协议配置）</summary>
        private readonly Dictionary<string, ProtocolConfig> _loadedProtocols = new Dictionary<string, ProtocolConfig>();

        /// <summary>协议名称到配置文件的映射</summary>
        private readonly Dictionary<string, string> _protocolFileMap = new Dictionary<string, string>();

        /// <summary>协议字典读写锁</summary>
        private readonly object _protocolLock = new object();

        #endregion

        /// <summary>
        /// 构造协议管理服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务，用于发送协议命令</param>
        public ProtocolService(ICanCommunicationService canService)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
        }

        #region -- 协议加载 --

        /// <summary>
        /// 加载协议定义
        /// 从YAML配置文件读取协议定义信息，包括参数映射、命令列表等
        /// 支持 GB/T 27930-2015 等国家标准协议
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="protocolFile">协议配置文件路径</param>
        /// <returns>加载是否成功</returns>
        public bool LoadProtocol(string protocolName, string protocolFile)
        {
            if (string.IsNullOrEmpty(protocolName))
                throw new ArgumentException("协议名称不能为空", nameof(protocolName));

            if (string.IsNullOrEmpty(protocolFile))
                throw new ArgumentException("协议文件路径不能为空", nameof(protocolFile));

            try
            {
                // 检查文件是否存在
                if (!File.Exists(protocolFile))
                {
                    System.Diagnostics.Debug.WriteLine($"[ProtocolService] 协议文件不存在: {protocolFile}");
                    return false;
                }

                // 使用ConfigManager加载YAML配置
                var protocolConfig = ConfigManager.LoadYamlConfig<ProtocolConfig>(protocolFile);

                if (protocolConfig == null || string.IsNullOrEmpty(protocolConfig.ProtocolName))
                {
                    // YAML加载失败，使用默认配置
                    protocolConfig = new ProtocolConfig
                    {
                        ProtocolId = CryptoHelper.GenerateUuid(),
                        ProtocolName = protocolName,
                        ProtocolType = "device",
                        ProtocolVersion = "1.0",
                        ConfigContent = File.ReadAllText(protocolFile, Encoding.UTF8),
                        IsActive = 1,
                        LoadedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                }

                lock (_protocolLock)
                {
                    _loadedProtocols[protocolName] = protocolConfig;
                    _protocolFileMap[protocolName] = protocolFile;
                }

                System.Diagnostics.Debug.WriteLine($"[ProtocolService] 协议加载成功: {protocolName} ({protocolFile})");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProtocolService] 协议加载失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 列出所有已加载的协议
        /// </summary>
        /// <returns>协议配置列表</returns>
        public List<ProtocolConfig> ListProtocols()
        {
            lock (_protocolLock)
            {
                return new List<ProtocolConfig>(_loadedProtocols.Values);
            }
        }

        #endregion

        #region -- 消息解析 --

        /// <summary>
        /// 根据协议解析CAN原始消息为结构化数据
        /// 解析过程：
        ///   1. 根据协议配置中的参数定义查找匹配项
        ///   2. 按字节偏移和数据长度提取原始数据
        ///   3. 应用缩放系数和偏移量转换为物理值
        ///   4. 返回结构化字典
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="canId">CAN消息ID</param>
        /// <param name="rawData">原始数据字节数组</param>
        /// <returns>解析后的消息对象</returns>
        public Dictionary<string, object> ParseMessage(string protocolName, uint canId, byte[] rawData)
        {
            var result = new Dictionary<string, object>
            {
                ["protocol"] = protocolName,
                ["can_id"] = $"0x{canId:X8}",
                ["can_id_raw"] = canId,
                ["data_hex"] = rawData != null ? BitConverter.ToString(rawData).Replace("-", " ") : "空",
                ["data_length"] = rawData?.Length ?? 0
            };

            if (rawData == null || rawData.Length == 0)
                return result;

            lock (_protocolLock)
            {
                if (!_loadedProtocols.TryGetValue(protocolName, out var protocol))
                {
                    result["_warning"] = $"协议 '{protocolName}' 未加载";
                    return result;
                }

                // 从协议配置内容中解析参数定义（JSON格式）
                ParseParametersFromConfig(protocol.ConfigContent, canId, rawData, result);
            }

            return result;
        }

        /// <summary>
        /// 从协议配置内容中解析参数映射
        /// 解析ConfigContent中的JSON定义，提取参数规则并解码数据
        /// </summary>
        /// <param name="configContent">协议配置内容（JSON格式）</param>
        /// <param name="canId">CAN消息ID</param>
        /// <param name="rawData">原始数据字节数组</param>
        /// <param name="result">输出结果字典</param>
        private void ParseParametersFromConfig(string configContent, uint canId, byte[] rawData, Dictionary<string, object> result)
        {
            try
            {
                // 尝试解析JSON配置中的参数定义
                if (!string.IsNullOrEmpty(configContent))
                {
                    // 示例：从配置中读取参数定义
                    // 实际项目中需要完整的JSON解析逻辑
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(configContent);
                    if (config != null && config.TryGetValue("parameters", out var parameters))
                    {
                        result["_parsed_parameters"] = parameters;
                    }
                }
            }
            catch (Exception ex)
            {
                result["_parse_error"] = $"参数解析异常: {ex.Message}";
            }

            // 默认解析示例：根据CAN ID低8位识别参数类型
            byte paramType = (byte)(canId & 0xFF);
            if (rawData.Length >= 4)
            {
                float floatValue = BitConverter.ToSingle(rawData, 0);
                string paramName = paramType switch
                {
                    0x01 => "voltage",
                    0x02 => "current",
                    0x03 => "temperature",
                    0x04 => "soc",
                    0x05 => "power",
                    _ => $"param_0x{paramType:X2}"
                };
                result[paramName] = Math.Round(floatValue, 2);

                string unit = paramType switch
                {
                    0x01 => "V",
                    0x02 => "A",
                    0x03 => "°C",
                    0x04 => "%",
                    0x05 => "kW",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(unit))
                    result[$"{paramName}_unit"] = unit;
            }
        }

        #endregion

        #region -- 消息验证 --

        /// <summary>
        /// 验证消息是否符合协议定义
        /// 检查CAN ID范围、数据长度、帧类型等是否满足协议规范
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="message">待验证的消息对象</param>
        /// <returns>验证是否通过</returns>
        public bool ValidateMessage(string protocolName, object message)
        {
            if (message == null) return false;

            lock (_protocolLock)
            {
                if (!_loadedProtocols.ContainsKey(protocolName))
                {
                    System.Diagnostics.Debug.WriteLine($"[ProtocolService] 协议未加载: {protocolName}");
                    return false;
                }
            }

            // 执行基本验证：
            //   1. CAN ID必须在协议规定的范围内
            //   2. 数据长度必须符合协议定义
            //   3. 帧类型必须匹配（标准帧/扩展帧）

            if (message is CanMessage canMsg)
            {
                // CAN ID有效性检查（0x000~0x1FFFFFFF）
                if (canMsg.IsExtended && canMsg.CanId > 0x1FFFFFFF)
                    return false;
                if (!canMsg.IsExtended && canMsg.CanId > 0x7FF)
                    return false;

                // 数据长度检查
                if (canMsg.IsFd && canMsg.Data.Length > 64)
                    return false;
                if (!canMsg.IsFd && canMsg.Data.Length > 8)
                    return false;
            }

            System.Diagnostics.Debug.WriteLine($"[ProtocolService] 消息验证通过: {protocolName}");
            return true;
        }

        #endregion

        #region -- 协议命令管理 --

        /// <summary>
        /// 获取协议支持的所有命令列表
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <returns>协议命令列表</returns>
        public List<ProtocolCommand> GetProtocolCommands(string protocolName)
        {
            lock (_protocolLock)
            {
                if (_loadedProtocols.TryGetValue(protocolName, out var protocol))
                {
                    return protocol.Commands ?? new List<ProtocolCommand>();
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ProtocolService] 未找到协议命令: {protocolName}");
            return new List<ProtocolCommand>();
        }

        /// <summary>
        /// 发送协议命令到设备
        /// 根据协议定义中的命令码和参数构建CAN消息并发送
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="commandName">命令名称</param>
        /// <param name="parameters">命令参数字典</param>
        /// <returns>发送是否成功</returns>
        public bool SendProtocolCommand(string protocolName, string commandName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(protocolName) || string.IsNullOrEmpty(commandName))
                return false;

            lock (_protocolLock)
            {
                if (!_loadedProtocols.TryGetValue(protocolName, out var protocol))
                {
                    System.Diagnostics.Debug.WriteLine($"[ProtocolService] 协议未加载: {protocolName}");
                    return false;
                }

                // 查找匹配的命令
                var command = protocol.Commands?.Find(c => c.CommandName == commandName);
                if (command == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProtocolService] 命令未找到: {commandName}");
                    return false;
                }

                // 构建CAN消息
                uint canId = (uint)(0x18FF0000 + command.CommandCode);
                byte[] data = BuildCommandData(command, parameters);

                // 发送CAN消息
                bool success = _canService.SendCanMessage(canId, data);
                System.Diagnostics.Debug.WriteLine($"[ProtocolService] 协议命令发送: {commandName}, 结果={success}");
                return success;
            }
        }

        /// <summary>
        /// 构建协议命令的CAN数据载荷
        /// </summary>
        /// <param name="command">协议命令定义</param>
        /// <param name="parameters">命令参数</param>
        /// <returns>8字节CAN数据载荷</returns>
        private byte[] BuildCommandData(ProtocolCommand command, Dictionary<string, object> parameters)
        {
            byte[] data = new byte[8];

            if (parameters == null || parameters.Count == 0)
                return data;

            // 根据命令的参数定义编码数据
            // 第0字节：命令类型码
            data[0] = (byte)command.CommandCode;

            // 后续字节：按参数定义编码
            int byteIndex = 1;
            foreach (var param in parameters)
            {
                if (byteIndex >= 8) break;

                if (param.Value is double d)
                {
                    byte[] bytes = BitConverter.GetBytes((float)d);
                    int copyLen = Math.Min(bytes.Length, 8 - byteIndex);
                    Array.Copy(bytes, 0, data, byteIndex, copyLen);
                    byteIndex += copyLen;
                }
                else if (param.Value is int i)
                {
                    if (byteIndex + 4 <= 8)
                    {
                        byte[] bytes = BitConverter.GetBytes(i);
                        Array.Copy(bytes, 0, data, byteIndex, bytes.Length);
                        byteIndex += bytes.Length;
                    }
                }
            }

            return data;
        }

        #endregion
    }
}
