// ============================================================
// 文件名: SystemConstants.cs
// 用途: 系统全局常量定义，方便集中修改和管理所有固定常量值
// ============================================================

using System;

namespace ChargeDischargeSystem.Common.Constants
{
    /// <summary>
    /// 系统全局常量，定义应用程序基础信息相关常量
    /// </summary>
    public static class SystemConstants
    {
        /// <summary>
        /// 应用程序名称
        /// </summary>
        public const string AppName = "MW级充放电上位机系统";

        /// <summary>
        /// 应用程序版本
        /// </summary>
        public const string AppVersion = "1.0.0";

        /// <summary>
        /// 默认数据库路径
        /// </summary>
        public const string DefaultDbPath = "Data/mw_scada.db";

        /// <summary>
        /// 默认配置文件目录
        /// </summary>
        public const string DefaultConfigDir = "config";

        /// <summary>
        /// 默认日志目录
        /// </summary>
        public const string DefaultLogDir = "logs";

        /// <summary>
        /// 数据库备份目录
        /// </summary>
        public const string DefaultBackupDir = "Data/backup";

        /// <summary>
        /// 固件存储目录
        /// </summary>
        public const string DefaultFirmwareDir = "Data/firmware";

        /// <summary>
        /// 报告输出目录
        /// </summary>
        public const string DefaultReportDir = "Data/reports";
    }

    /// <summary>
    /// CAN 总线通信常量，定义与周立功 CAN 卡通信相关的固定参数
    /// </summary>
    public static class CanConstants
    {
        /// <summary>
        /// 默认 CAN 总线波特率 500kbps
        /// </summary>
        public const int DefaultBitrate = 500000;

        /// <summary>
        /// CAN FD 数据段默认波特率 2Mbps
        /// </summary>
        public const int DefaultDataBitrate = 2000000;

        /// <summary>
        /// CAN 消息发送默认超时(毫秒)
        /// </summary>
        public const int DefaultSendTimeoutMs = 1000;

        /// <summary>
        /// CAN 消息接收默认超时(毫秒)
        /// </summary>
        public const int DefaultRecvTimeoutMs = 5000;

        /// <summary>
        /// 固件升级数据块大小(CAN 2.0B)
        /// </summary>
        public const int Can20BlockSize = 6;

        /// <summary>
        /// 固件升级数据块大小(CAN FD)
        /// </summary>
        public const int CanFdBlockSize = 62;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public const int MaxRetryCount = 3;
    }

    /// <summary>
    /// 数据记录常量，定义数据采样和存储相关常量
    /// </summary>
    public static class DataLogConstants
    {
        /// <summary>
        /// 默认采样间隔(毫秒)
        /// </summary>
        public const int DefaultSampleIntervalMs = 1000;

        /// <summary>
        /// 快速采样间隔(毫秒) - 故障时使用
        /// </summary>
        public const int FastSampleIntervalMs = 100;

        /// <summary>
        /// 数据缓冲区最大条目数
        /// </summary>
        public const int MaxBufferSize = 1000;

        /// <summary>
        /// 缓冲区刷新间隔(毫秒)
        /// </summary>
        public const int FlushIntervalMs = 5000;

        /// <summary>
        /// 高分辨率数据保留天数
        /// </summary>
        public const int HighResRetentionDays = 30;

        /// <summary>
        /// 1分钟聚合数据保留天数
        /// </summary>
        public const int Aggregated1MinRetentionDays = 365;
    }

    /// <summary>
    /// 校准常量，定义校准相关参数
    /// </summary>
    public static class CalibrationConstants
    {
        /// <summary>
        /// 零点校准采样点数
        /// </summary>
        public const int ZeroCalSamplesCount = 100;

        /// <summary>
        /// 量程校准采样点数
        /// </summary>
        public const int SpanCalSamplesCount = 50;

        /// <summary>
        /// 校准有效期(天)
        /// </summary>
        public const int ValidityPeriodDays = 90;

        /// <summary>
        /// 校准到期提醒提前天数
        /// </summary>
        public const int ReminderDays = 7;

        /// <summary>
        /// 默认允许误差百分比
        /// </summary>
        public const double DefaultTolerancePercent = 0.1;
    }

    /// <summary>
    /// 用户管理常量，定义用户认证和账户管理相关常量
    /// </summary>
    public static class UserConstants
    {
        /// <summary>
        /// 会话超时时间(分钟) - 8小时
        /// </summary>
        public const int SessionTimeoutMinutes = 480;

        /// <summary>
        /// 最大登录失败次数
        /// </summary>
        public const int MaxLoginAttempts = 5;

        /// <summary>
        /// 账户锁定时间(分钟)
        /// </summary>
        public const int AccountLockoutMinutes = 30;

        /// <summary>
        /// 密码最小长度
        /// </summary>
        public const int PasswordMinLength = 8;
    }
}
