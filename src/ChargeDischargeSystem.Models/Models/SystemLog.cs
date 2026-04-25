// ============================================================
// 文件名: SystemLog.cs
// 用途: 系统日志实体类，对应数据库表 system_log，存储系统运行日志
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 系统日志实体类，存储系统运行过程中产生的各级别日志信息
    /// 用于系统运维、问题排查和审计追溯
    /// </summary>
    public class SystemLog
    {
        /// <summary>
        /// 获取或设置日志 ID，自增主键
        /// </summary>
        public long LogId { get; set; }

        /// <summary>
        /// 获取或设置日志时间戳，Unix 毫秒时间戳
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 获取或设置日志级别: DEBUG(调试)/INFO(信息)/WARNING(警告)/ERROR(错误)/CRITICAL(严重)
        /// </summary>
        public string LogLevel { get; set; }

        /// <summary>
        /// 获取或设置日志来源模块名称
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// 获取或设置日志消息正文
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 获取或设置日志详细信息，JSON 格式存储扩展字段
        /// </summary>
        public string Details { get; set; }

        /// <summary>
        /// 获取或设置关联用户 ID，可为空
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 获取或设置关联设备 ID，可为空
        /// </summary>
        public string DeviceId { get; set; }
    }
}
