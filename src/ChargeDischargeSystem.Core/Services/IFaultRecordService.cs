// ============================================================
// 文件名: IFaultRecordService.cs
// 用途: 故障录波服务接口，提供设备故障自动检测、录波存储和波形导出功能
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Core.Models;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 故障录波服务接口
    /// 负责设备故障事件的检测、波形数据记录和管理：
    ///   1. 循环缓冲区存储故障前后数据
    ///   2. 高速采样(1kHz)和正常采样(10Hz)双模式
    ///   3. 故障触发时自动保存波形数据
    ///   4. 支持波形数据导出为CSV格式
    /// </summary>
    public interface IFaultRecordService
    {
        /// <summary>
        /// 故障发生事件，当检测到故障时触发
        /// </summary>
        event Action<FaultEvent> OnFaultOccurred;

        /// <summary>
        /// 启用指定设备的故障录波功能，配置触发条件并启动循环缓冲区数据采集
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="config">故障触发配置</param>
        /// <returns>启用是否成功</returns>
        bool EnableFaultRecording(string deviceId, FaultTriggerConfig config);

        /// <summary>
        /// 禁用指定设备的故障录波功能，停止数据采集并清空循环缓冲区
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>禁用是否成功</returns>
        bool DisableFaultRecording(string deviceId);

        /// <summary>
        /// 获取故障事件的波形数据
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <returns>波形数据集合（Key: 通道名称, Value: 采样数据数组）</returns>
        Dictionary<string, double[]> GetWaveformData(string eventId);

        /// <summary>
        /// 查询故障事件列表
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="startTime">查询起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">查询结束时间（Unix毫秒时间戳）</param>
        /// <param name="faultLevel">故障等级过滤（CRITICAL/MAJOR/MINOR），为null则不过滤</param>
        /// <returns>故障事件列表</returns>
        List<FaultEvent> ListFaultEvents(string deviceId, long startTime, long endTime, string faultLevel = null);

        /// <summary>
        /// 导出故障波形数据，将指定故障事件的波形数据导出为文件
        /// </summary>
        /// <param name="eventId">故障事件ID</param>
        /// <param name="format">导出格式，支持 csv / json，默认csv</param>
        /// <returns>导出文件路径</returns>
        string ExportWaveform(string eventId, string format = "csv");
    }
}
