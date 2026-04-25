// ============================================================
// 文件名: IFirmwareService.cs
// 用途: 固件升级服务接口，提供设备固件远程升级全流程管理功能
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 固件升级服务接口
    /// 负责设备固件的远程升级全流程管理：
    ///   1. 升级状态机管理（IDLE→CHECK_VERSION→...→COMPLETED）
    ///   2. CAN固件升级协议命令发送
    ///   3. 固件数据分块传输和CRC16校验
    ///   4. 升级进度实时通知
    ///   5. 固件版本回滚
    /// </summary>
    public interface IFirmwareService
    {
        /// <summary>
        /// 升级进度更新事件，当升级状态或进度变化时触发
        /// </summary>
        event Action<string, UpgradeProgress> OnUpgradeProgressUpdated;

        /// <summary>
        /// 启动固件升级
        /// 执行完整的固件升级流程，包括版本检查、Bootloader进入、
        /// Flash擦除、数据传输、固件校验和设备重启
        /// </summary>
        /// <param name="deviceId">目标设备ID</param>
        /// <param name="firmwareFile">固件文件路径</param>
        /// <param name="firmwareVersion">固件版本号</param>
        /// <param name="forceUpgrade">是否强制升级（跳过版本检查），默认false</param>
        /// <returns>升级任务ID</returns>
        string StartUpgrade(string deviceId, string firmwareFile, string firmwareVersion, bool forceUpgrade = false);

        /// <summary>
        /// 获取固件升级任务的当前状态
        /// </summary>
        /// <param name="taskId">升级任务ID</param>
        /// <returns>升级进度信息</returns>
        UpgradeProgress GetUpgradeStatus(string taskId);

        /// <summary>
        /// 取消正在进行的固件升级
        /// </summary>
        /// <param name="taskId">升级任务ID</param>
        /// <returns>取消是否成功</returns>
        bool CancelUpgrade(string taskId);

        /// <summary>
        /// 获取设备的当前固件版本
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>固件版本号</returns>
        string GetFirmwareVersion(string deviceId);

        /// <summary>
        /// 回滚设备固件到上一个版本
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>回滚是否成功</returns>
        bool RollbackFirmware(string deviceId);
    }

    // UpgradeProgress 定义在 ServiceModels.cs 中
}
