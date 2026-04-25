// ============================================================
// 文件名: FirmwareVersion.cs
// 用途: 固件版本和升级任务实体类，对应 firmware_version 和 upgrade_task 表
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 固件版本实体类，存储设备的固件版本信息和固件文件路径
    /// </summary>
    public class FirmwareVersion
    {
        /// <summary>
        /// 获取或设置版本记录 ID，主键
        /// </summary>
        public string VersionId { get; set; }

        /// <summary>
        /// 获取或设置设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置固件版本号
        /// </summary>
        public string FirmwareVersionNumber { get; set; }

        /// <summary>
        /// 获取或设置固件文件存储路径
        /// </summary>
        public string FirmwareFilePath { get; set; }

        /// <summary>
        /// 获取或设置固件文件大小，单位字节
        /// </summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>
        /// 获取或设置 SHA-256 校验和，用于验证固件完整性
        /// </summary>
        public string Checksum { get; set; }

        /// <summary>
        /// 获取或设置发布说明或更新日志
        /// </summary>
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// 获取或设置发布时间
        /// </summary>
        public string ReleasedAt { get; set; }
    }

    /// <summary>
    /// 固件升级任务实体类，记录固件升级任务的执行状态和结果
    /// </summary>
    public class UpgradeTask
    {
        /// <summary>
        /// 获取或设置升级任务 ID，主键
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// 获取或设置目标设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置升级前固件版本号
        /// </summary>
        public string FromVersion { get; set; }

        /// <summary>
        /// 获取或设置目标固件版本号
        /// </summary>
        public string ToVersion { get; set; }

        /// <summary>
        /// 获取或设置使用的固件文件名
        /// </summary>
        public string FirmwareFile { get; set; }

        /// <summary>
        /// 获取或设置任务状态: pending(等待)/in_progress(进行中)/completed(完成)/failed(失败)/cancelled(已取消)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 获取或设置升级进度百分比，范围 0~100
        /// </summary>
        public double ProgressPercent { get; set; }

        /// <summary>
        /// 获取或设置任务开始时间，Unix 毫秒时间戳
        /// </summary>
        public long? StartedAt { get; set; }

        /// <summary>
        /// 获取或设置任务完成时间，Unix 毫秒时间戳
        /// </summary>
        public long? CompletedAt { get; set; }

        /// <summary>
        /// 获取或设置任务发起人用户 ID
        /// </summary>
        public string InitiatedBy { get; set; }

        /// <summary>
        /// 获取或设置失败原因或错误信息
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
