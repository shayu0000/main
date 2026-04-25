// ============================================================
// 文件名: MeasurementData.cs
// 用途: 测量数据实体类，对应数据库表 measurement_data，核心时序数据表
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 测量数据实体类，存储充放电过程中设备上报的实时测量数据
    /// </summary>
    public class MeasurementData
    {
        /// <summary>
        /// 获取或设置记录 ID，自增主键
        /// </summary>
        public long RecordId { get; set; }

        /// <summary>
        /// 获取或设置 Unix 毫秒时间戳，记录数据采集时间
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 获取或设置设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置会话 ID，外键关联 charge_session，可为空
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 获取或设置参数名称: voltage(电压)/current(电流)/power(功率)/temperature(温度)/soc(荷电状态)
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// 获取或设置测量值
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// 获取或设置测量单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 获取或设置数据质量: GOOD(良好)/UNCERTAIN(不确定)/BAD(差)，默认为 GOOD
        /// </summary>
        public string Quality { get; set; } = "GOOD";
    }
}
