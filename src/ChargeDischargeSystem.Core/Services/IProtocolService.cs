// ============================================================
// 文件名: IProtocolService.cs
// 用途: 协议管理服务接口，提供通信协议的加载、解析、验证和命令管理功能
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Core.Models;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 协议管理服务接口
    /// 负责通信协议的加载、管理和使用：
    ///   1. 从YAML文件加载协议定义
    ///   2. 根据协议解析CAN原始消息为结构化数据
    ///   3. 验证消息格式合法性
    ///   4. 提供协议定义的命令列表
    ///   5. 支持GB/T 27930-2015等标准协议
    /// </summary>
    public interface IProtocolService
    {
        /// <summary>
        /// 加载协议定义，从YAML配置文件加载指定协议的定义信息
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="protocolFile">协议配置文件路径</param>
        /// <returns>加载是否成功</returns>
        bool LoadProtocol(string protocolName, string protocolFile);

        /// <summary>
        /// 列出所有已加载的协议
        /// </summary>
        /// <returns>协议配置列表</returns>
        List<ProtocolConfig> ListProtocols();

        /// <summary>
        /// 根据协议解析CAN原始消息
        /// 将CAN报文的原始字节数据按照协议定义解析为结构化对象
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="canId">CAN消息ID</param>
        /// <param name="rawData">原始数据字节数组</param>
        /// <returns>解析后的消息对象（字典格式，Key=字段名, Value=字段值）</returns>
        Dictionary<string, object> ParseMessage(string protocolName, uint canId, byte[] rawData);

        /// <summary>
        /// 验证消息是否符合协议定义
        /// 检查消息的CAN ID、数据长度、帧格式等是否满足协议规范
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="message">待验证的消息对象</param>
        /// <returns>验证是否通过</returns>
        bool ValidateMessage(string protocolName, object message);

        /// <summary>
        /// 获取协议支持的所有命令列表
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <returns>协议命令列表</returns>
        List<ProtocolCommand> GetProtocolCommands(string protocolName);

        /// <summary>
        /// 发送协议命令到设备
        /// 根据协议定义构建CAN消息并通过CAN总线发送
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="commandName">命令名称</param>
        /// <param name="parameters">命令参数字典（Key=参数名, Value=参数值）</param>
        /// <returns>发送是否成功</returns>
        bool SendProtocolCommand(string protocolName, string commandName, Dictionary<string, object> parameters);
    }
}
