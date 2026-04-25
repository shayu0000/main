// ============================================================
// 文件名: SystemEnums.cs
// 用途: 系统枚举类型定义，集中管理系统中使用的所有枚举类型
// ============================================================

using System;

namespace ChargeDischargeSystem.Common.Enums
{
    /// <summary>
    /// 设备类型枚举，定义系统支持的充放电设备类型
    /// </summary>
    public enum DeviceType
    {
        /// <summary>
        /// 储能变流器(PCS)
        /// </summary>
        PCS,

        /// <summary>
        /// 电池管理系统(BMS)
        /// </summary>
        BMS,

        /// <summary>
        /// 逆变器
        /// </summary>
        Inverter,

        /// <summary>
        /// 电表/传感器
        /// </summary>
        Meter,

        /// <summary>
        /// 充电桩
        /// </summary>
        Charger
    }

    /// <summary>
    /// 设备状态枚举，定义设备的运行状态
    /// </summary>
    public enum DeviceStatus
    {
        /// <summary>
        /// 在线状态，设备正常运行且通信正常
        /// </summary>
        Online,

        /// <summary>
        /// 离线状态，设备与系统断开通信
        /// </summary>
        Offline,

        /// <summary>
        /// 故障状态，设备发生异常
        /// </summary>
        Fault,

        /// <summary>
        /// 维护中状态，设备正在进行维护操作
        /// </summary>
        Maintenance
    }

    /// <summary>
    /// 数据质量枚举，定义采集数据的质量等级
    /// </summary>
    public enum DataQuality
    {
        /// <summary>
        /// 良好，数据可靠可用
        /// </summary>
        Good,

        /// <summary>
        /// 不确定，数据可能存在问题
        /// </summary>
        Uncertain,

        /// <summary>
        /// 差，数据不可用需重新采集
        /// </summary>
        Bad
    }

    /// <summary>
    /// 告警级别枚举，定义告警的严重程度等级
    /// </summary>
    public enum AlarmLevel
    {
        /// <summary>
        /// 严重 - 需要立即停机处理
        /// </summary>
        Critical,

        /// <summary>
        /// 重大 - 需要降功率运行并尽快处理
        /// </summary>
        Major,

        /// <summary>
        /// 轻微 - 需要通知操作员关注
        /// </summary>
        Minor,

        /// <summary>
        /// 警告 - 仅记录日志无需操作
        /// </summary>
        Warning
    }

    /// <summary>
    /// 校准类型枚举，定义设备校准的不同类型
    /// </summary>
    public enum CalibrationType
    {
        /// <summary>
        /// 零点校准，消除零偏误差
        /// </summary>
        Zero,

        /// <summary>
        /// 量程校准，调整满量程精度
        /// </summary>
        Span,

        /// <summary>
        /// 线性校准(多点)，多点线性拟合校准
        /// </summary>
        Linear,

        /// <summary>
        /// 电流精确校准，电流通道专项校准
        /// </summary>
        VICurrent,

        /// <summary>
        /// 电压精确校准，电压通道专项校准
        /// </summary>
        VIVoltage
    }

    /// <summary>
    /// 校准状态枚举，定义校准过程的当前状态
    /// </summary>
    public enum CalibrationStatus
    {
        /// <summary>
        /// 进行中，校准尚未完成
        /// </summary>
        InProgress,

        /// <summary>
        /// 已完成，校准成功结束
        /// </summary>
        Completed,

        /// <summary>
        /// 失败，校准异常终止
        /// </summary>
        Failed
    }

    /// <summary>
    /// 充放电会话类型枚举，定义不同类型的充放电测试任务
    /// </summary>
    public enum SessionType
    {
        /// <summary>
        /// 充电会话
        /// </summary>
        Charge,

        /// <summary>
        /// 放电会话
        /// </summary>
        Discharge,

        /// <summary>
        /// 循环充放会话
        /// </summary>
        Cycle,

        /// <summary>
        /// 化成会话，电池首次充放电激活
        /// </summary>
        Formation
    }

    /// <summary>
    /// 会话状态枚举，定义充放电会话的生命周期状态
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>
        /// 运行中，会话正在执行
        /// </summary>
        Running,

        /// <summary>
        /// 暂停，会话暂时停止
        /// </summary>
        Paused,

        /// <summary>
        /// 已完成，会话正常结束
        /// </summary>
        Completed,

        /// <summary>
        /// 已中止，会话被人为取消
        /// </summary>
        Aborted,

        /// <summary>
        /// 故障终止，因设备故障而终止
        /// </summary>
        Fault
    }

    /// <summary>
    /// 固件升级状态枚举，定义固件升级任务的执行状态
    /// </summary>
    public enum UpgradeStatus
    {
        /// <summary>
        /// 等待中，升级任务排队等待执行
        /// </summary>
        Pending,

        /// <summary>
        /// 进行中，固件正在升级中
        /// </summary>
        InProgress,

        /// <summary>
        /// 已完成，固件升级成功
        /// </summary>
        Completed,

        /// <summary>
        /// 失败，固件升级异常
        /// </summary>
        Failed,

        /// <summary>
        /// 已取消，升级任务被取消
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// CAN 总线状态枚举，定义 CAN 总线通信的当前状态
    /// </summary>
    public enum CanBusStatus
    {
        /// <summary>
        /// 正常，CAN 总线通信正常
        /// </summary>
        OK,

        /// <summary>
        /// 错误，CAN 总线通信出现异常
        /// </summary>
        Error,

        /// <summary>
        /// 总线关闭，CAN 总线已断开
        /// </summary>
        BusOff,

        /// <summary>
        /// 未知，无法确定 CAN 总线状态
        /// </summary>
        Unknown
    }

    /// <summary>
    /// 数据记录状态枚举，定义数据记录器的工作状态
    /// </summary>
    public enum RecordingStatus
    {
        /// <summary>
        /// 空闲，数据记录器未工作
        /// </summary>
        Idle,

        /// <summary>
        /// 记录中，数据记录器正在采集数据
        /// </summary>
        Recording,

        /// <summary>
        /// 错误，数据记录器发生异常
        /// </summary>
        Error
    }

    /// <summary>
    /// 报告类型枚举，定义系统可生成的各类报告类型
    /// </summary>
    public enum ReportType
    {
        /// <summary>
        /// 充电测试报告
        /// </summary>
        ChargeTest,

        /// <summary>
        /// 放电测试报告
        /// </summary>
        DischargeTest,

        /// <summary>
        /// 循环测试报告
        /// </summary>
        CycleTest,

        /// <summary>
        /// 化成测试报告
        /// </summary>
        FormationTest,

        /// <summary>
        /// 校准报告
        /// </summary>
        CalibrationReport,

        /// <summary>
        /// 故障分析报告
        /// </summary>
        FaultAnalysis,

        /// <summary>
        /// 日报，每日运行数据汇总
        /// </summary>
        DailySummary,

        /// <summary>
        /// 月报，每月运行数据汇总
        /// </summary>
        MonthlySummary
    }

    /// <summary>
    /// 用户状态枚举，定义用户账户的运行状态
    /// </summary>
    public enum UserStatus
    {
        /// <summary>
        /// 活跃，用户账户正常可用
        /// </summary>
        Active,

        /// <summary>
        /// 禁用，用户账户被管理员禁用
        /// </summary>
        Disabled,

        /// <summary>
        /// 锁定，用户账户因多次登录失败被锁定
        /// </summary>
        Locked
    }

    /// <summary>
    /// CAN 消息帧类型枚举，定义 CAN 总线帧格式类型
    /// </summary>
    public enum CanFrameType
    {
        /// <summary>
        /// 标准帧(11位ID)
        /// </summary>
        Standard,

        /// <summary>
        /// 扩展帧(29位ID)
        /// </summary>
        Extended
    }
}
