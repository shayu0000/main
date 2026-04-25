using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 校准服务实现类
// 说明: 实现设备的零点校准、量程校准和线性校准功能
//       包含数据采集、稳定性判断、最小二乘法拟合和结果验证
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 校准服务实现类
    /// 负责充放电设备各类参数的校准工作：
    ///   - 零点校准(ZERO): 采集零输入偏移量
    ///   - 量程校准(SPAN): 使用标准源校准增益系数
    ///   - 线性校准(LINEAR): 多点最小二乘法拟合校准曲线
    /// 
    /// 校准结果验证标准：
    ///   - 增益系数范围: 0.95 ~ 1.05
    ///   - 线性度 R² >= 0.999
    /// </summary>
    public class CalibrationService : ICalibrationService
    {
        #region -- 字段定义 --

        /// <summary>CAN通信服务引用</summary>
        private readonly ICanCommunicationService _canService;

        /// <summary>设备监控服务引用</summary>
        private readonly IDeviceMonitorService _monitorService;

        /// <summary>校准会话字典（Key: SessionId, Value: 校准记录）</summary>
        private readonly Dictionary<string, CalibrationRecord> _sessions = new Dictionary<string, CalibrationRecord>();

        /// <summary>会话锁</summary>
        private readonly object _sessionLock = new object();

        #endregion

        /// <summary>
        /// 构造校准服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务</param>
        /// <param name="monitorService">设备监控服务</param>
        public CalibrationService(ICanCommunicationService canService, IDeviceMonitorService monitorService)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
        }

        #region -- 校准流程管理 --

        /// <summary>
        /// 启动校准流程（根据校准类型分发）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="calibrationType">校准类型</param>
        /// <param name="referenceValue">参考值</param>
        /// <param name="calibrationPoints">线性校准点</param>
        /// <returns>校准会话ID</returns>
        public string StartCalibration(string deviceId, string calibrationType,
            double? referenceValue = null, List<double> calibrationPoints = null)
        {
            return calibrationType?.ToUpper() switch
            {
                "ZERO" => PerformZeroCalibration(deviceId),
                "SPAN" => PerformSpanCalibration(deviceId, referenceValue ?? 0),
                "LINEAR" => PerformLinearCalibration(deviceId, calibrationPoints ?? new List<double>(),
                    new List<double>()),
                "VI_CURRENT" => PerformZeroCalibration(deviceId), // 电流校准可复用零点校准逻辑
                "VI_VOLTAGE" => PerformSpanCalibration(deviceId, referenceValue ?? 0), // 电压校准可复用量程校准逻辑
                _ => throw new ArgumentException($"不支持的校准类型: {calibrationType}")
            };
        }

        /// <summary>
        /// 获取校准会话的当前状态
        /// </summary>
        /// <param name="sessionId">校准会话ID</param>
        /// <returns>校准状态</returns>
        public string GetCalibrationStatus(string sessionId)
        {
            lock (_sessionLock)
            {
                return _sessions.TryGetValue(sessionId, out var session)
                    ? session.CalibrationStatus
                    : "NOT_FOUND";
            }
        }

        /// <summary>
        /// 应用校准结果
        /// 将校准参数写入设备并通过CAN发送配置命令
        /// </summary>
        /// <param name="sessionId">校准会话ID</param>
        /// <returns>应用是否成功</returns>
        public bool ApplyCalibration(string sessionId)
        {
            lock (_sessionLock)
            {
                if (!_sessions.TryGetValue(sessionId, out var session))
                {
                    System.Diagnostics.Debug.WriteLine($"[CalibrationService] 校准会话不存在: {sessionId}");
                    return false;
                }

                if (session.CalibrationStatus != "completed")
                {
                    System.Diagnostics.Debug.WriteLine($"[CalibrationService] 校准未完成，无法应用: {sessionId}");
                    return false;
                }

                // 将校准参数写入设备（通过CAN发送校准参数）
                // TODO: 发送校准参数到设备
                System.Diagnostics.Debug.WriteLine($"[CalibrationService] 校准已应用: {sessionId}, 设备: {session.DeviceId}");
                return true;
            }
        }

        /// <summary>
        /// 获取设备的校准历史记录
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="startTime">起始时间戳</param>
        /// <param name="endTime">结束时间戳</param>
        /// <returns>校准记录列表</returns>
        public List<CalibrationRecord> GetCalibrationHistory(string deviceId, long startTime, long endTime)
        {
            // TODO: 注入CalibrationRepository查询历史记录
            System.Diagnostics.Debug.WriteLine($"[CalibrationService] 查询校准历史: {deviceId}, {startTime}~{endTime}");
            return new List<CalibrationRecord>();
        }

        #endregion

        #region -- 零点校准 --

        /// <summary>
        /// 执行零点校准
        /// 采集零输入状态下多个样本，取平均值作为零偏值
        /// 采集过程中进行稳定性判断，确保数据可靠
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>校准会话ID</returns>
        public string PerformZeroCalibration(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            string sessionId = CryptoHelper.GenerateUuid();

            // 创建校准记录
            var record = new CalibrationRecord
            {
                CalibrationId = sessionId,
                DeviceId = deviceId,
                CalibrationType = "ZERO",
                CalibrationStatus = "in_progress",
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PerformedBy = "SYSTEM"
            };

            lock (_sessionLock) { _sessions[sessionId] = record; }

            try
            {
                // ---- 第一步：采集零输入数据 ----
                int sampleCount = CalibrationConstants.ZeroCalSamplesCount;
                var samples = new List<double>(sampleCount);

                for (int i = 0; i < sampleCount; i++)
                {
                    // 从设备读取零输入时的测量值
                    double value = ReadDeviceMeasurement(deviceId, "voltage");
                    samples.Add(value);

                    // 采集间隔10ms
                    Thread.Sleep(10);
                }

                // ---- 第二步：稳定性判断 ----
                if (!IsStable(samples, out double stabilityDeviation))
                {
                    record.CalibrationStatus = "failed";
                    record.Notes = $"零点校准失败：数据不稳定（标准差={stabilityDeviation:F4}）";
                    System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
                    return sessionId;
                }

                // ---- 第三步：计算零偏值（平均值） ----
                double zeroOffset = samples.Average();

                // ---- 第四步：记录校准点 ----
                record.CalibrationPoints = new List<CalibrationPoint>
                {
                    new CalibrationPoint
                    {
                        CalibrationId = sessionId,
                        ParameterName = "voltage",
                        PointIndex = 0,
                        ReferenceValue = 0,
                        MeasuredValue = zeroOffset,
                        CorrectedValue = 0,
                        DeviationPercent = Math.Abs(zeroOffset),
                        IsPass = Math.Abs(zeroOffset) < 0.5 ? 1 : 0 // 零偏小于0.5V视为合格
                    }
                };

                // ---- 第五步：更新记录状态 ----
                record.CalibrationStatus = "completed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                record.ValidUntil = DateTimeOffset.UtcNow.AddDays(CalibrationConstants.ValidityPeriodDays).ToUnixTimeMilliseconds();
                record.Notes = $"零点校准完成：零偏={zeroOffset:F4}V";

                System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
            }
            catch (Exception ex)
            {
                record.CalibrationStatus = "failed";
                record.Notes = $"零点校准异常: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
            }

            return sessionId;
        }

        #endregion

        #region -- 量程校准 --

        /// <summary>
        /// 执行量程校准
        /// 使用已知标准参考值校准设备的增益系数
        /// 采集多组数据，计算测量值平均值与参考值的比值作为增益
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValue">标准参考值</param>
        /// <returns>校准会话ID</returns>
        public string PerformSpanCalibration(string deviceId, double referenceValue)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (referenceValue <= 0)
                throw new ArgumentException("参考值必须为正数", nameof(referenceValue));

            string sessionId = CryptoHelper.GenerateUuid();

            var record = new CalibrationRecord
            {
                CalibrationId = sessionId,
                DeviceId = deviceId,
                CalibrationType = "SPAN",
                CalibrationStatus = "in_progress",
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PerformedBy = "SYSTEM"
            };

            lock (_sessionLock) { _sessions[sessionId] = record; }

            try
            {
                // ---- 第一步：采集测量数据 ----
                int sampleCount = CalibrationConstants.SpanCalSamplesCount;
                var samples = new List<double>(sampleCount);

                for (int i = 0; i < sampleCount; i++)
                {
                    double value = ReadDeviceMeasurement(deviceId, "voltage");
                    samples.Add(value);
                    Thread.Sleep(20);
                }

                // ---- 第二步：稳定性判断 ----
                if (!IsStable(samples, out double stabilityDeviation))
                {
                    record.CalibrationStatus = "failed";
                    record.Notes = $"量程校准失败：数据不稳定（标准差={stabilityDeviation:F4}）";
                    System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
                    return sessionId;
                }

                // ---- 第三步：计算增益系数 ----
                double measuredAvg = samples.Average();
                double gain = referenceValue / measuredAvg;

                // ---- 第四步：增益范围验证 ----
                bool isGainValid = gain >= 0.95 && gain <= 1.05;

                record.CalibrationPoints = new List<CalibrationPoint>
                {
                    new CalibrationPoint
                    {
                        CalibrationId = sessionId,
                        ParameterName = "voltage",
                        PointIndex = 0,
                        ReferenceValue = referenceValue,
                        MeasuredValue = measuredAvg,
                        CorrectedValue = measuredAvg * gain,
                        DeviationPercent = Math.Abs(1.0 - gain) * 100.0,
                        IsPass = isGainValid ? 1 : 0
                    }
                };

                record.CalibrationStatus = isGainValid ? "completed" : "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (isGainValid)
                    record.ValidUntil = DateTimeOffset.UtcNow.AddDays(CalibrationConstants.ValidityPeriodDays).ToUnixTimeMilliseconds();
                record.Notes = isGainValid
                    ? $"量程校准完成：参考值={referenceValue:F2}, 测量均值={measuredAvg:F4}, 增益={gain:F6}"
                    : $"量程校准失败：增益系数超出范围(0.95~1.05)，当前增益={gain:F6}";

                System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
            }
            catch (Exception ex)
            {
                record.CalibrationStatus = "failed";
                record.Notes = $"量程校准异常: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
            }

            return sessionId;
        }

        #endregion

        #region -- 线性校准（最小二乘法） --

        /// <summary>
        /// 执行线性校准（多点最小二乘法拟合）
        /// 使用最小二乘法对多组(参考值, 测量值)进行线性回归：
        ///   y = a * x + b
        ///   其中 a = 增益, b = 偏移量
        /// 
        /// 公式：
        ///   a = (n*Σxy - Σx*Σy) / (n*Σx² - (Σx)²)
        ///   b = (Σy - a*Σx) / n
        ///   R² = 1 - SS_res / SS_tot (线性度指标)
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValues">标准参考值集合</param>
        /// <param name="measuredValues">设备测量值集合</param>
        /// <returns>校准会话ID</returns>
        public string PerformLinearCalibration(string deviceId, List<double> referenceValues, List<double> measuredValues)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (referenceValues == null || measuredValues == null)
                throw new ArgumentNullException("参考值和测量值不能为null");

            if (referenceValues.Count != measuredValues.Count || referenceValues.Count < 3)
                throw new ArgumentException("线性校准至少需要3个校准点，且参考值与测量值数量必须一致");

            string sessionId = CryptoHelper.GenerateUuid();

            var record = new CalibrationRecord
            {
                CalibrationId = sessionId,
                DeviceId = deviceId,
                CalibrationType = "LINEAR",
                CalibrationStatus = "in_progress",
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PerformedBy = "SYSTEM"
            };

            lock (_sessionLock) { _sessions[sessionId] = record; }

            try
            {
                int n = referenceValues.Count;

                // ---- 第一步：计算最小二乘法参数 ----
                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = referenceValues[i];
                    double y = measuredValues[i];
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumX2 += x * x;
                    sumY2 += y * y;
                }

                double denominator = n * sumX2 - sumX * sumX;
                if (Math.Abs(denominator) < 1e-10)
                {
                    record.CalibrationStatus = "failed";
                    record.Notes = "线性校准失败：参考值方差为零，无法进行线性拟合";
                    return sessionId;
                }

                // 增益 a = (n*Σxy - Σx*Σy) / (n*Σx² - (Σx)²)
                double gain = (n * sumXY - sumX * sumY) / denominator;

                // 偏移 b = (Σy - a*Σx) / n
                double offset = (sumY - gain * sumX) / n;

                // ---- 第二步：计算线性度 R² ----
                double yMean = sumY / n;
                double ssRes = 0; // 残差平方和
                double ssTot = 0; // 总平方和
                for (int i = 0; i < n; i++)
                {
                    double predicted = gain * referenceValues[i] + offset;
                    ssRes += Math.Pow(measuredValues[i] - predicted, 2);
                    ssTot += Math.Pow(measuredValues[i] - yMean, 2);
                }

                double rSquared = ssTot > 0 ? 1.0 - ssRes / ssTot : 0;

                // ---- 第三步：验证校准结果 ----
                bool isGainValid = gain >= 0.95 && gain <= 1.05;
                bool isLinearityValid = rSquared >= 0.999;

                // ---- 第四步：记录校准点 ----
                var points = new List<CalibrationPoint>();
                for (int i = 0; i < n; i++)
                {
                    double corrected = gain * measuredValues[i] + offset;
                    double deviation = Math.Abs(referenceValues[i] - measuredValues[i]) / referenceValues[i] * 100;
                    points.Add(new CalibrationPoint
                    {
                        CalibrationId = sessionId,
                        ParameterName = "voltage",
                        PointIndex = i,
                        ReferenceValue = referenceValues[i],
                        MeasuredValue = measuredValues[i],
                        CorrectedValue = corrected,
                        DeviationPercent = deviation,
                        IsPass = deviation < 1.0 ? 1 : 0
                    });
                }
                record.CalibrationPoints = points;

                bool isPass = isGainValid && isLinearityValid;
                record.CalibrationStatus = isPass ? "completed" : "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (isPass)
                    record.ValidUntil = DateTimeOffset.UtcNow.AddDays(CalibrationConstants.ValidityPeriodDays).ToUnixTimeMilliseconds();

                record.Notes = isPass
                    ? $"线性校准完成：增益={gain:F6}, 偏移={offset:F4}, 线性度R²={rSquared:F6}"
                    : $"线性校准失败：增益={(isGainValid ? "合格" : $"不合格({gain:F6})")}, 线性度R²={(isLinearityValid ? "合格" : $"不合格({rSquared:F6})")}";

                System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
            }
            catch (Exception ex)
            {
                record.CalibrationStatus = "failed";
                record.Notes = $"线性校准异常: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[CalibrationService] {record.Notes}");
            }

            return sessionId;
        }

        #endregion

        #region -- 辅助方法 --

        /// <summary>
        /// 从设备读取单个测量值（通过CAN总线）
        /// 实际项目中通过CAN服务发送请求并等待设备响应
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="parameterName">参数名称</param>
        /// <returns>测量值</returns>
        private double ReadDeviceMeasurement(string deviceId, string parameterName)
        {
            // TODO: 通过CAN发送数据请求命令并解析响应
            // 这里返回模拟数据
            var data = _monitorService.GetLatestData(deviceId);
            if (data != null)
            {
                return parameterName switch
                {
                    "voltage" => data.Voltage,
                    "current" => data.Current,
                    "temperature" => data.Temperature,
                    _ => 0
                };
            }
            return 0;
        }

        /// <summary>
        /// 判断采集数据的稳定性
        /// 计算样本标准差，若标准差/均值 < 1% 则认为数据稳定
        /// </summary>
        /// <param name="samples">样本数据</param>
        /// <param name="standardDeviation">输出标准差</param>
        /// <returns>数据是否稳定</returns>
        private bool IsStable(List<double> samples, out double standardDeviation)
        {
            standardDeviation = 0;
            if (samples.Count < 2) return true;

            double mean = samples.Average();
            double sumSquaredDiff = samples.Sum(s => Math.Pow(s - mean, 2));
            standardDeviation = Math.Sqrt(sumSquaredDiff / (samples.Count - 1));

            // 稳定标准：变异系数 < 0.5%
            double coefficientOfVariation = Math.Abs(mean) > 1e-10 ? standardDeviation / Math.Abs(mean) : standardDeviation;
            return coefficientOfVariation < 0.005;
        }

        #endregion
    }
}
