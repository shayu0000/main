// ============================================================
// 文件名: IDataLogService.cs
// 用途: 数据记录服务接口，提供测量数据的记录、查询和自动备份功能
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Core.Models;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 数据记录服务接口
    /// 负责充放电过程中所有测量数据的记录和查询：
    ///   1. 启动/停止数据记录会话
    ///   2. 使用数据缓冲区批量写入数据库
    ///   3. 按照时间区间和聚合方式查询历史数据
    ///   4. 定期自动备份数据库文件
    /// </summary>
    public interface IDataLogService
    {
        /// <summary>
        /// 记录状态变化事件，当记录会话状态变更时触发
        /// </summary>
        event Action<string, string> OnRecordingStatusChanged;

        /// <summary>
        /// 启动数据记录会话
        /// 开始记录指定设备的数据，启动定时缓冲刷新机制
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="dataTypes">要记录的数据类型列表（voltage/current/temperature/soc等），为null则记录所有</param>
        /// <param name="sampleIntervalMs">采样间隔（毫秒），默认1000ms</param>
        /// <returns>启动是否成功</returns>
        bool StartRecording(string sessionId, List<string> dataTypes = null, int sampleIntervalMs = 1000);

        /// <summary>
        /// 停止数据记录会话，刷新缓冲区中剩余数据并关闭记录
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>停止是否成功</returns>
        bool StopRecording(string sessionId);

        /// <summary>
        /// 查询历史数据
        /// 按照设备、参数列表、时间区间和聚合方式查询测量数据
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="parameterNames">参数名称列表（voltage/current/temperature/soc/power）</param>
        /// <param name="startTime">查询起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">查询结束时间（Unix毫秒时间戳）</param>
        /// <param name="aggregation">聚合方式: none(原始值)/avg(均值)/min(最小值)/max(最大值)</param>
        /// <returns>满足条件的测量数据列表</returns>
        List<MeasurementData> QueryData(string deviceId, List<string> parameterNames, long startTime, long endTime, string aggregation = "none");

        /// <summary>
        /// 获取记录会话列表
        /// </summary>
        /// <param name="startTime">查询起始时间</param>
        /// <param name="endTime">查询结束时间</param>
        /// <returns>会话列表</returns>
        List<ChargeSession> GetSessionList(long startTime, long endTime);

        /// <summary>
        /// 删除指定时间之前的旧数据，用于定期清理过期数据节省存储空间
        /// </summary>
        /// <param name="beforeTime">删除此时间之前的数据（Unix毫秒时间戳）</param>
        /// <returns>删除的数据条数</returns>
        long DeleteOldData(long beforeTime);
    }
}
