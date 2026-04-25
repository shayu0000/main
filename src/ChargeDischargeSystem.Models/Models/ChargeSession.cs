// ============================================================
// 文件名: ChargeSession.cs
// 用途: 充放电会话实体类，对应数据库表 charge_session
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 充放电会话实体类，存储充放电测试会话的完整信息
    /// </summary>
    public class ChargeSession
    {
        /// <summary>
        /// 获取或设置会话 ID，主键
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 获取或设置设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置电池协议名称
        /// </summary>
        public string BatteryProtocol { get; set; }

        /// <summary>
        /// 获取或设置会话类型: charge(充电)/discharge(放电)/cycle(循环)/formation(化成)
        /// </summary>
        public string SessionType { get; set; }

        /// <summary>
        /// 获取或设置会话状态: running(运行中)/paused(暂停)/completed(完成)/aborted(中止)/fault(故障)，默认为 running
        /// </summary>
        public string Status { get; set; } = "running";

        /// <summary>
        /// 获取或设置开始时间，Unix 毫秒时间戳
        /// </summary>
        public long StartTime { get; set; }

        /// <summary>
        /// 获取或设置结束时间，Unix 毫秒时间戳
        /// </summary>
        public long? EndTime { get; set; }

        // ---- 设定参数 ----

        /// <summary>
        /// 获取或设置目标电压，单位 V
        /// </summary>
        public double? TargetVoltageV { get; set; }

        /// <summary>
        /// 获取或设置目标电流，单位 A
        /// </summary>
        public double? TargetCurrentA { get; set; }

        /// <summary>
        /// 获取或设置目标功率，单位 kW
        /// </summary>
        public double? TargetPowerKw { get; set; }

        /// <summary>
        /// 获取或设置目标 SOC 百分比
        /// </summary>
        public double? TargetSocPercent { get; set; }

        /// <summary>
        /// 获取或设置目标持续时长，单位秒
        /// </summary>
        public int? TargetDurationS { get; set; }

        /// <summary>
        /// 获取或设置截止电压，单位 V
        /// </summary>
        public double? CutoffVoltageV { get; set; }

        // ---- 累计数据 ----

        /// <summary>
        /// 获取或设置累计能量，单位 kWh
        /// </summary>
        public double TotalEnergyKwh { get; set; }

        /// <summary>
        /// 获取或设置累计安时，单位 Ah
        /// </summary>
        public double TotalChargeAh { get; set; }

        /// <summary>
        /// 获取或设置最高电压，单位 V
        /// </summary>
        public double? MaxVoltageV { get; set; }

        /// <summary>
        /// 获取或设置最低电压，单位 V
        /// </summary>
        public double? MinVoltageV { get; set; }

        /// <summary>
        /// 获取或设置最大电流，单位 A
        /// </summary>
        public double? MaxCurrentA { get; set; }

        /// <summary>
        /// 获取或设置最高温度，单位 °C
        /// </summary>
        public double? MaxTemperatureC { get; set; }

        /// <summary>
        /// 获取或设置创建人用户 ID
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// 获取或设置备注信息
        /// </summary>
        public string Notes { get; set; }
    }
}
