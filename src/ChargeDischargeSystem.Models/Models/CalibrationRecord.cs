// ============================================================
// 文件名: CalibrationRecord.cs
// 用途: 校准记录和校准点实体类，对应 calibration_record 和 calibration_point 表
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 校准记录实体类，存储设备校准记录信息
    /// </summary>
    public class CalibrationRecord
    {
        /// <summary>
        /// 获取或设置校准记录 ID，主键
        /// </summary>
        public string CalibrationId { get; set; }

        /// <summary>
        /// 获取或设置设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置校准类型: ZERO(零点校准)/SPAN(量程校准)/LINEAR(线性校准)/VI_CURRENT(电流校准)/VI_VOLTAGE(电压校准)
        /// </summary>
        public string CalibrationType { get; set; }

        /// <summary>
        /// 获取或设置校准状态: in_progress(进行中)/completed(已完成)/failed(失败)
        /// </summary>
        public string CalibrationStatus { get; set; }

        /// <summary>
        /// 获取或设置校准开始时间，Unix 毫秒时间戳
        /// </summary>
        public long StartedAt { get; set; }

        /// <summary>
        /// 获取或设置校准完成时间，Unix 毫秒时间戳
        /// </summary>
        public long? CompletedAt { get; set; }

        /// <summary>
        /// 获取或设置执行校准的用户 ID
        /// </summary>
        public string PerformedBy { get; set; }

        /// <summary>
        /// 获取或设置批准校准的用户 ID
        /// </summary>
        public string ApprovedBy { get; set; }

        /// <summary>
        /// 获取或设置校准是否有效: 1=有效, 0=无效，默认为有效
        /// </summary>
        public int IsValid { get; set; } = 1;

        /// <summary>
        /// 获取或设置校准有效期截止时间，Unix 毫秒时间戳
        /// </summary>
        public long ValidUntil { get; set; }

        /// <summary>
        /// 获取或设置备注信息
        /// </summary>
        public string Notes { get; set; }

        /// <summary>
        /// 获取或设置校准点集合，导航属性
        /// </summary>
        public List<CalibrationPoint> CalibrationPoints { get; set; } = new List<CalibrationPoint>();
    }

    /// <summary>
    /// 校准数据点实体类，记录单个校准点的数据
    /// </summary>
    public class CalibrationPoint
    {
        /// <summary>
        /// 获取或设置校准点 ID，自增主键
        /// </summary>
        public long PointId { get; set; }

        /// <summary>
        /// 获取或设置校准记录 ID，外键关联 calibration_record
        /// </summary>
        public string CalibrationId { get; set; }

        /// <summary>
        /// 获取或设置参数名称: voltage(电压)/current(电流)/power(功率)
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// 获取或设置校准点序号，从 0 开始
        /// </summary>
        public int PointIndex { get; set; }

        /// <summary>
        /// 获取或设置标准参考值，从高精度仪表读取
        /// </summary>
        public double ReferenceValue { get; set; }

        /// <summary>
        /// 获取或设置设备测量值，校准前的原始测量值
        /// </summary>
        public double MeasuredValue { get; set; }

        /// <summary>
        /// 获取或设置校准后的修正值
        /// </summary>
        public double? CorrectedValue { get; set; }

        /// <summary>
        /// 获取或设置偏差百分比
        /// </summary>
        public double? DeviationPercent { get; set; }

        /// <summary>
        /// 获取或设置该校准点是否合格: 1=合格, 0=不合格
        /// </summary>
        public int? IsPass { get; set; }
    }
}
