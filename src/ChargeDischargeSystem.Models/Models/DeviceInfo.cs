// ============================================================
// 文件名: DeviceInfo.cs
// 用途: 设备信息实体类，对应数据库表 device_info
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 设备信息实体类，存储充放电设备的基本信息
    /// </summary>
    public class DeviceInfo
    {
        /// <summary>
        /// 获取或设置设备唯一标识
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置设备类型: PCS/BMS/Inverter/Meter/Charger
        /// </summary>
        public string DeviceType { get; set; }

        /// <summary>
        /// 获取或设置设备名称
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 获取或设置制造商
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// 获取或设置型号
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// 获取或设置序列号
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// 获取或设置额定功率，单位 kW
        /// </summary>
        public double RatedPowerKw { get; set; }

        /// <summary>
        /// 获取或设置额定电压，单位 V
        /// </summary>
        public double RatedVoltageV { get; set; }

        /// <summary>
        /// 获取或设置额定电流，单位 A
        /// </summary>
        public double RatedCurrentA { get; set; }

        /// <summary>
        /// 获取或设置 CAN 总线地址
        /// </summary>
        public int CanAddress { get; set; }

        /// <summary>
        /// 获取或设置通信协议名称
        /// </summary>
        public string ProtocolName { get; set; }

        /// <summary>
        /// 获取或设置当前固件版本
        /// </summary>
        public string FirmwareVersion { get; set; }

        /// <summary>
        /// 获取或设置设备状态: Online/Offline/Fault/Maintenance，默认为 Offline
        /// </summary>
        public string Status { get; set; } = "Offline";

        /// <summary>
        /// 获取或设置注册时间
        /// </summary>
        public string RegisteredAt { get; set; }

        /// <summary>
        /// 获取或设置最后在线时间
        /// </summary>
        public string LastOnlineTime { get; set; }

        /// <summary>
        /// 获取或设置备注信息
        /// </summary>
        public string Notes { get; set; }
    }
}
