// ============================================================
// 文件名: DeviceStatus.cs
// 用途: 设备状态和告警实体类，对应 device_status 和 device_alarm 表
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 设备状态数据记录实体类，存储设备实时状态参数数据
    /// </summary>
    public class DeviceStatusRecord
    {
        /// <summary>
        /// 获取或设置记录 ID，自增主键
        /// </summary>
        public long RecordId { get; set; }

        /// <summary>
        /// 获取或设置设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置参数名称: voltage(电压)/current(电流)/temperature(温度)/soc(荷电状态)
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// 获取或设置参数值
        /// </summary>
        public double ParameterValue { get; set; }

        /// <summary>
        /// 获取或设置单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 获取或设置数据质量: GOOD(良好)/UNCERTAIN(不确定)/BAD(差)，默认为 GOOD
        /// </summary>
        public string Quality { get; set; } = "GOOD";

        /// <summary>
        /// 获取或设置 Unix 毫秒时间戳
        /// </summary>
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// 设备告警实体类，存储设备告警信息
    /// </summary>
    public class DeviceAlarm
    {
        /// <summary>
        /// 获取或设置告警 ID，主键
        /// </summary>
        public string AlarmId { get; set; }

        /// <summary>
        /// 获取或设置设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置告警编码
        /// </summary>
        public string AlarmCode { get; set; }

        /// <summary>
        /// 获取或设置告警级别: CRITICAL(严重)/MAJOR(主要)/MINOR(次要)/WARNING(警告)
        /// </summary>
        public string AlarmLevel { get; set; }

        /// <summary>
        /// 获取或设置告警描述信息
        /// </summary>
        public string AlarmMessage { get; set; }

        /// <summary>
        /// 获取或设置触发告警的参数名称
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// 获取或设置告警阈值
        /// </summary>
        public double ThresholdValue { get; set; }

        /// <summary>
        /// 获取或设置实际测量值
        /// </summary>
        public double ActualValue { get; set; }

        /// <summary>
        /// 获取或设置是否仍处于活跃状态: 1=活跃, 0=已清除，默认为活跃
        /// </summary>
        public int IsActive { get; set; } = 1;

        /// <summary>
        /// 获取或设置告警触发时间，Unix 毫秒时间戳
        /// </summary>
        public long RaisedAt { get; set; }

        /// <summary>
        /// 获取或设置告警确认时间，Unix 毫秒时间戳
        /// </summary>
        public long? AcknowledgedAt { get; set; }

        /// <summary>
        /// 获取或设置确认人用户 ID
        /// </summary>
        public string AcknowledgedBy { get; set; }

        /// <summary>
        /// 获取或设置告警清除时间，Unix 毫秒时间戳
        /// </summary>
        public long? ClearedAt { get; set; }
    }
}
