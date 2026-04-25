// ============================================================
// 文件名: IDeviceMonitorService.cs
// 用途: 设备监控服务接口，提供设备数据采集、告警检测和实时数据查询功能
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Core.Models;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 设备监控服务接口
    /// 负责定期轮询设备数据、解析CAN原始数据为物理参数、检测告警阈值与配置对比触发告警
    /// </summary>
    public interface IDeviceMonitorService
    {
        /// <summary>
        /// 数据更新事件，当收到设备新数据时触发
        /// </summary>
        event Action<Dictionary<string, DeviceDataPoint>> OnDataUpdated;

        /// <summary>
        /// 告警触发事件，当检测到参数超过告警阈值时触发
        /// </summary>
        event Action<DeviceAlarm> OnAlarmRaised;

        /// <summary>
        /// 开始监控所有或指定设备，启动内部定时器定期通过CAN总线轮询设备数据
        /// </summary>
        /// <param name="deviceIds">要监控的设备ID列表，为null则监控所有已注册设备</param>
        /// <returns>启动是否成功</returns>
        bool StartMonitoring(List<string> deviceIds = null);

        /// <summary>
        /// 停止所有设备监控，停止定时器不再轮询设备数据
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// 获取指定设备的最新数据快照
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备最新数据点，若设备离线或无数据则返回null</returns>
        DeviceDataPoint GetLatestData(string deviceId);

        /// <summary>
        /// 查询告警历史记录
        /// </summary>
        /// <param name="startTime">查询起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">查询结束时间（Unix毫秒时间戳）</param>
        /// <param name="alarmLevel">告警级别过滤（CRITICAL/MAJOR/MINOR/WARNING），为null则不过滤</param>
        /// <returns>满足条件的告警记录列表</returns>
        List<DeviceAlarm> GetAlarmHistory(long startTime, long endTime, string alarmLevel = null);

        /// <summary>
        /// 获取当前所有活跃（未确认/未清除）的告警
        /// </summary>
        /// <returns>活跃告警列表</returns>
        List<DeviceAlarm> GetActiveAlarms();

        /// <summary>
        /// 确认告警，操作员对指定告警进行确认操作，记录确认人和时间
        /// </summary>
        /// <param name="alarmId">告警ID</param>
        /// <param name="userId">确认操作的用户ID</param>
        /// <returns>确认是否成功</returns>
        bool AcknowledgeAlarm(string alarmId, string userId);

        /// <summary>
        /// 获取监控服务是否正在运行
        /// </summary>
        bool IsRunning { get; }
    }

    // DeviceDataPoint 和 FaultTriggerConfig 定义在 ServiceModels.cs 中
}
