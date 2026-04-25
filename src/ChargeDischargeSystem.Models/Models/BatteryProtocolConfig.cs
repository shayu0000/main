// ============================================================
// 文件名: BatteryProtocolConfig.cs
// 用途: 电池协议配置实体类，对应数据库表 battery_protocol_config
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 电池协议配置实体类，存储动力电池通信协议的定义和配置
    /// </summary>
    public class BatteryProtocolConfig
    {
        /// <summary>
        /// 获取或设置配置 ID，主键
        /// </summary>
        public string ConfigId { get; set; }

        /// <summary>
        /// 获取或设置协议名称
        /// </summary>
        public string ProtocolName { get; set; }

        /// <summary>
        /// 获取或设置 BMS 供应商名称
        /// </summary>
        public string BmsVendor { get; set; }

        /// <summary>
        /// 获取或设置协议版本号
        /// </summary>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// 获取或设置 PGN 定义，JSON 格式存储参数组编号及字段映射
        /// </summary>
        public string PgnDefinition { get; set; }

        /// <summary>
        /// 获取或设置故障码映射表，JSON 格式存储故障码与故障描述的对应关系
        /// </summary>
        public string FaultCodeMap { get; set; }

        /// <summary>
        /// 获取或设置是否启用: 1=启用, 0=禁用，默认为启用
        /// </summary>
        public int IsActive { get; set; } = 1;

        /// <summary>
        /// 获取或设置创建时间
        /// </summary>
        public string CreatedAt { get; set; }
    }
}
