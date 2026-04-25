// ============================================================
// 文件名: ICalibrationService.cs
// 用途: 校准服务接口，提供设备零点校准、量程校准和线性校准功能
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Core.Models;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 校准服务接口
    /// 负责充放电设备的电压/电流/功率等参数的校准：
    ///   1. 零点校准(ZERO) - 消除零偏误差
    ///   2. 量程校准(SPAN) - 使用标准参考值校准量程
    ///   3. 线性校准(LINEAR) - 多点校准，最小二乘法拟合
    /// </summary>
    public interface ICalibrationService
    {
        /// <summary>
        /// 启动校准流程，根据校准类型执行对应的校准流程（零点/量程/线性）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="calibrationType">校准类型: ZERO/SPAN/LINEAR/VI_CURRENT/VI_VOLTAGE</param>
        /// <param name="referenceValue">参考值（量程校准时使用）</param>
        /// <param name="calibrationPoints">线性校准的参考值列表（线性校准时使用）</param>
        /// <returns>校准会话ID，用于后续查询状态和确认校准</returns>
        string StartCalibration(string deviceId, string calibrationType, double? referenceValue = null, List<double> calibrationPoints = null);

        /// <summary>
        /// 获取校准会话的当前状态
        /// </summary>
        /// <param name="sessionId">校准会话ID</param>
        /// <returns>校准状态信息（IN_PROGRESS/COMPLETED/FAILED）</returns>
        string GetCalibrationStatus(string sessionId);

        /// <summary>
        /// 应用校准结果，将校准参数写入设备并更新数据库记录
        /// </summary>
        /// <param name="sessionId">校准会话ID</param>
        /// <returns>应用是否成功</returns>
        bool ApplyCalibration(string sessionId);

        /// <summary>
        /// 获取设备的校准历史记录
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="startTime">起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">结束时间（Unix毫秒时间戳）</param>
        /// <returns>校准记录列表</returns>
        List<CalibrationRecord> GetCalibrationHistory(string deviceId, long startTime, long endTime);

        /// <summary>
        /// 执行零点校准，采集零输入时的测量值作为零偏用于后续补偿
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>校准会话ID</returns>
        string PerformZeroCalibration(string deviceId);

        /// <summary>
        /// 执行量程校准，使用已知标准参考值校准设备的量程系数
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValue">标准参考值</param>
        /// <returns>校准会话ID</returns>
        string PerformSpanCalibration(string deviceId, double referenceValue);

        /// <summary>
        /// 执行线性校准（多点最小二乘法拟合）
        /// 使用多组参考值与测量值进行线性回归，得到增益和偏移系数
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValues">标准参考值集合</param>
        /// <param name="measuredValues">设备测量值集合（与参考值一一对应）</param>
        /// <returns>校准会话ID</returns>
        string PerformLinearCalibration(string deviceId, List<double> referenceValues, List<double> measuredValues);
    }
}
