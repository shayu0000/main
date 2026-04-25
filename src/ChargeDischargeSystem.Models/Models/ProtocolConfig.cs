// ============================================================
// 文件名: ProtocolConfig.cs
// 用途: 协议配置和协议命令实体类，对应 protocol_config 和 protocol_command 表
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 协议配置实体类，存储设备或电池通信协议的定义信息
    /// </summary>
    public class ProtocolConfig
    {
        /// <summary>
        /// 获取或设置协议 ID，主键
        /// </summary>
        public string ProtocolId { get; set; }

        /// <summary>
        /// 获取或设置协议名称，唯一
        /// </summary>
        public string ProtocolName { get; set; }

        /// <summary>
        /// 获取或设置协议类型: device(设备协议)/battery(电池协议)
        /// </summary>
        public string ProtocolType { get; set; }

        /// <summary>
        /// 获取或设置协议版本号
        /// </summary>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// 获取或设置协议配置内容，JSON 格式存储完整的协议定义
        /// </summary>
        public string ConfigContent { get; set; }

        /// <summary>
        /// 获取或设置是否启用: 1=启用, 0=禁用，默认为启用
        /// </summary>
        public int IsActive { get; set; } = 1;

        /// <summary>
        /// 获取或设置协议加载时间
        /// </summary>
        public string LoadedAt { get; set; }

        /// <summary>
        /// 获取或设置协议最后更新时间
        /// </summary>
        public string UpdatedAt { get; set; }

        /// <summary>
        /// 获取或设置协议命令集合，导航属性
        /// </summary>
        public List<ProtocolCommand> Commands { get; set; } = new List<ProtocolCommand>();
    }

    /// <summary>
    /// 协议命令实体类，定义协议中的通信命令
    /// </summary>
    public class ProtocolCommand
    {
        /// <summary>
        /// 获取或设置命令 ID，主键
        /// </summary>
        public string CommandId { get; set; }

        /// <summary>
        /// 获取或设置协议 ID，外键关联 protocol_config
        /// </summary>
        public string ProtocolId { get; set; }

        /// <summary>
        /// 获取或设置命令名称
        /// </summary>
        public string CommandName { get; set; }

        /// <summary>
        /// 获取或设置 CAN 总线命令码
        /// </summary>
        public int CommandCode { get; set; }

        /// <summary>
        /// 获取或设置请求参数定义，JSON 格式
        /// </summary>
        public string RequestParams { get; set; }

        /// <summary>
        /// 获取或设置响应参数定义，JSON 格式
        /// </summary>
        public string ResponseParams { get; set; }

        /// <summary>
        /// 获取或设置命令超时时间，单位毫秒，默认 1000ms
        /// </summary>
        public int TimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 获取或设置重试次数，默认 3 次
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// 获取或设置命令描述信息
        /// </summary>
        public string Description { get; set; }
    }
}
