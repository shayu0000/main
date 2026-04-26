using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Data.Repositories;

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
    ///   - 线性校准(LINEAR): 多点最小二乘法拟合校准曲线 y = gain * x + offset
    ///   - 电流校准(VI_CURRENT): 复用零点校准逻辑的电流专项校准
    ///   - 电压校准(VI_VOLTAGE): 复用量程校准逻辑的电压专项校准
    /// 
    /// 校准结果验证标准（参见 4.3.3 开发文档）：
    ///   - 增益系数范围: 0.95 ~ 1.05
    ///   - 偏移量绝对值 &lt; 100
    ///   - 线性度 R² >= 0.999
    /// </summary>
    public class CalibrationService : ICalibrationService
    {
        #region -- 字段定义 --

        /// <summary>CAN通信服务引用，用于向设备发送校准命令</summary>
        private readonly ICanCommunicationService _canService;

        /// <summary>设备监控服务引用，用于读取设备实时测量数据</summary>
        private readonly IDeviceMonitorService _monitorService;

        /// <summary>校准数据仓库，持久化校准记录和校准点</summary>
        private readonly CalibrationRepository _calibrationRepository;

        /// <summary>设备数据仓库，校验设备存在性及获取设备信息</summary>
        private readonly DeviceRepository _deviceRepository;

        /// <summary>线程安全校准会话字典（Key: SessionId, Value: 校准记录）</summary>
        private readonly ConcurrentDictionary<string, CalibrationRecord> _sessions = new();

        /// <summary>读写锁，用于保护复合访问操作</summary>
        private readonly ReaderWriterLockSlim _rwLock = new();

        #endregion

        #region -- 构造函数 --

        /// <summary>
        /// 构造校准服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务（不可为null）</param>
        /// <param name="monitorService">设备监控服务（不可为null）</param>
        /// <param name="calibrationRepository">校准数据仓库（不可为null）</param>
        /// <param name="deviceRepository">设备数据仓库（不可为null）</param>
        public CalibrationService(
            ICanCommunicationService canService,
            IDeviceMonitorService monitorService,
            CalibrationRepository calibrationRepository,
            DeviceRepository deviceRepository)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
            _monitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
            _calibrationRepository = calibrationRepository ?? throw new ArgumentNullException(nameof(calibrationRepository));
            _deviceRepository = deviceRepository ?? throw new ArgumentNullException(nameof(deviceRepository));
        }

        #endregion

        #region -- 校准流程管理 --

        /// <summary>
        /// 启动校准流程，根据校准类型分发到对应的校准执行方法
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="calibrationType">校准类型: ZERO/SPAN/LINEAR/VI_CURRENT/VI_VOLTAGE</param>
        /// <param name="referenceValue">参考值（ZERO/SPAN/VI_VOLTAGE时使用）</param>
        /// <param name="calibrationPoints">线性校准的参考值列表（LINEAR时使用）</param>
        /// <returns>校准会话ID</returns>
        public string StartCalibration(string deviceId, string calibrationType,
            double? referenceValue = null, List<double> calibrationPoints = null)
        {
            // 校验设备是否存在
            var device = _deviceRepository.GetDeviceById(deviceId);
            if (device == null)
                throw new ArgumentException($"设备不存在: {deviceId}");

            var type = calibrationType?.ToUpper() ?? throw new ArgumentException("校准类型不能为空");

            switch (type)
            {
                case "ZERO":
                    return PerformZeroCalibrationInternal(deviceId, "voltage");
                case "SPAN":
                    if (!referenceValue.HasValue || referenceValue.Value <= 0)
                        throw new ArgumentException("量程校准需要提供正数参考值");
                    return PerformSpanCalibrationInternal(deviceId, referenceValue.Value, "voltage");
                case "LINEAR":
                    var refVals = calibrationPoints ?? new List<double>();
                    if (refVals.Count < 3)
                        throw new ArgumentException("线性校准至少需要3个校准点");
                    var measuredValues = CollectMeasuredValuesForLinearCalibration(deviceId, refVals);
                    return PerformLinearCalibration(deviceId, refVals, measuredValues);
                case "VI_CURRENT":
                    return PerformZeroCalibrationInternal(deviceId, "current");
                case "VI_VOLTAGE":
                    if (!referenceValue.HasValue || referenceValue.Value <= 0)
                        throw new ArgumentException("电压校准需要提供正数参考值");
                    return PerformSpanCalibrationInternal(deviceId, referenceValue.Value, "voltage");
                default:
                    throw new ArgumentException($"不支持的校准类型: {calibrationType}");
            }
        }

        /// <summary>
        /// 获取校准会话的当前状态
        /// 优先查询内存中的活动会话，若不存在则从数据库查询历史记录
        /// </summary>
        /// <param name="sessionId">校准会话ID</param>
        /// <returns>校准状态: in_progress / completed / failed / NOT_FOUND</returns>
        public string GetCalibrationStatus(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return "NOT_FOUND";

            // 先从内存会话中查找
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return session.CalibrationStatus;
            }

            // 内存中没有则从数据库查询
            var dbRecord = _calibrationRepository.GetCalibrationById(sessionId);
            if (dbRecord != null)
            {
                // 将数据库记录缓存到内存（历史记录复用）
                _sessions.TryAdd(sessionId, dbRecord);
                return dbRecord.CalibrationStatus;
            }

            return "NOT_FOUND";
        }

        /// <summary>
        /// 应用校准结果到设备
        /// 1. 验证校准会话存在且已完成
        /// 2. 通过CAN总线发送校准参数（增益、偏移）到设备
        /// 3. 更新数据库中校准记录状态为已应用
        /// </summary>
        /// <param name="sessionId">校准会话ID</param>
        /// <returns>应用是否成功</returns>
        public bool ApplyCalibration(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return false;

            _rwLock.EnterReadLock();
            try
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

                // 通过CAN总线向设备发送校准参数
                bool canResult = SendCalibrationParametersToDevice(session);
                if (!canResult)
                {
                    System.Diagnostics.Debug.WriteLine($"[CalibrationService] CAN发送校准参数失败: {sessionId}");
                    return false;
                }

                // 更新数据库中的记录为已应用
                int updateResult = _calibrationRepository.UpdateCalibrationStatus(sessionId, "completed");
                if (updateResult <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CalibrationService] 数据库更新校准状态失败: {sessionId}");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[CalibrationService] 校准已应用到设备: {sessionId}, 设备: {session.DeviceId}");
                return true;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 获取设备的校准历史记录
        /// 从数据库中按时间范围查询指定设备的校准历史，包含关联的校准点数据
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="startTime">起始时间（Unix毫秒时间戳）</param>
        /// <param name="endTime">结束时间（Unix毫秒时间戳）</param>
        /// <returns>校准记录列表（按时间倒序排列）</returns>
        public List<CalibrationRecord> GetCalibrationHistory(string deviceId, long startTime, long endTime)
        {
            if (string.IsNullOrEmpty(deviceId))
                return new List<CalibrationRecord>();

            // 校验设备存在
            var device = _deviceRepository.GetDeviceById(deviceId);
            if (device == null)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationService] 设备不存在: {deviceId}");
                return new List<CalibrationRecord>();
            }

            var history = _calibrationRepository.GetCalibrationHistory(deviceId, startTime, endTime);

            System.Diagnostics.Debug.WriteLine($"[CalibrationService] 查询校准历史: {deviceId}, 时间: {startTime}~{endTime}, 记录数: {history.Count}");
            return history;
        }

        #endregion

        #region -- 零点校准 --

        /// <summary>
        /// 执行零点校准（电压参数）
        /// 采集零输入状态下多个样本，计算平均值作为零偏值
        /// 对样本进行稳定性验证，确保采集数据可靠
        /// 校准结果持久化到数据库
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>校准会话ID</returns>
        public string PerformZeroCalibration(string deviceId)
        {
            return PerformZeroCalibrationInternal(deviceId, "voltage");
        }

        /// <summary>
        /// 执行零点校准（内部实现，支持指定参数名称）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="parameterName">校准参数名称: voltage / current</param>
        /// <returns>校准会话ID</returns>
        private string PerformZeroCalibrationInternal(string deviceId, string parameterName)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (string.IsNullOrEmpty(parameterName))
                parameterName = "voltage";

            string sessionId = CryptoHelper.GenerateUuid();

            var record = new CalibrationRecord
            {
                CalibrationId = sessionId,
                DeviceId = deviceId,
                CalibrationType = parameterName == "current" ? "VI_CURRENT" : "ZERO",
                CalibrationStatus = "in_progress",
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PerformedBy = "SYSTEM",
                IsValid = 1
            };

            _sessions.TryAdd(sessionId, record);

            // 持久化初始记录到数据库
            _calibrationRepository.InsertCalibrationRecord(record);

            try
            {
                // ---- 第一步：采集零输入数据 ----
                int sampleCount = CalibrationConstants.ZeroCalSamplesCount;
                var samples = new List<double>(sampleCount);

                for (int i = 0; i < sampleCount; i++)
                {
                    double value = ReadDeviceMeasurement(deviceId, parameterName);
                    samples.Add(value);
                    Thread.Sleep(10);
                }

                // ---- 第二步：稳定性判断 ----
                if (!IsStable(samples, out double stabilityDeviation))
                {
                    record.CalibrationStatus = "failed";
                    record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    record.IsValid = 0;
                    record.Notes = $"零点校准失败：数据不稳定（标准差={stabilityDeviation:F4}）";
                    UpdateAndPersist(record, sessionId);
                    return sessionId;
                }

                // ---- 第三步：计算零偏值（平均值） ----
                double zeroOffset = samples.Average();

                // ---- 第四步：零偏值范围验证 ----
                bool isOffsetValid = Math.Abs(zeroOffset) < 100.0;
                bool isStrictPass = Math.Abs(zeroOffset) < 0.5;

                // ---- 第五步：记录校准点 ----
                record.CalibrationPoints = new List<CalibrationPoint>
                {
                    new CalibrationPoint
                    {
                        CalibrationId = sessionId,
                        ParameterName = parameterName,
                        PointIndex = 0,
                        ReferenceValue = 0,
                        MeasuredValue = zeroOffset,
                        CorrectedValue = 0,
                        DeviationPercent = Math.Abs(zeroOffset),
                        IsPass = isStrictPass ? 1 : 0
                    }
                };

                // ---- 第六步：持久化校准点 ----
                _calibrationRepository.InsertCalibrationPoints(record.CalibrationPoints);

                // ---- 第七步：更新记录状态 ----
                record.CalibrationStatus = isOffsetValid ? "completed" : "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                record.ValidUntil = DateTimeOffset.UtcNow.AddDays(CalibrationConstants.ValidityPeriodDays).ToUnixTimeMilliseconds();
                record.IsValid = isOffsetValid ? 1 : 0;
                record.Notes = isOffsetValid
                    ? $"零点校准完成：零偏={zeroOffset:F4}（参数: {parameterName}）"
                    : $"零点校准失败：零偏={zeroOffset:F4}超出允许范围";

                UpdateAndPersist(record, sessionId);
            }
            catch (Exception ex)
            {
                record.CalibrationStatus = "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                record.IsValid = 0;
                record.Notes = $"零点校准异常: {ex.Message}";
                UpdateAndPersist(record, sessionId);
            }

            return sessionId;
        }

        #endregion

        #region -- 量程校准 --

        /// <summary>
        /// 执行量程校准（电压参数）
        /// 使用已知标准参考值校准设备的增益系数
        /// 采集多组测量数据，计算增益 = 参考值 / 测量均值
        /// 验证增益系数是否在 [0.95, 1.05] 范围内
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValue">标准参考值（必须为正数）</param>
        /// <returns>校准会话ID</returns>
        public string PerformSpanCalibration(string deviceId, double referenceValue)
        {
            return PerformSpanCalibrationInternal(deviceId, referenceValue, "voltage");
        }

        /// <summary>
        /// 执行量程校准（内部实现，支持指定参数名称）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValue">标准参考值（必须为正数）</param>
        /// <param name="parameterName">校准参数名称: voltage / current</param>
        /// <returns>校准会话ID</returns>
        private string PerformSpanCalibrationInternal(string deviceId, double referenceValue, string parameterName)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (referenceValue <= 0)
                throw new ArgumentException("参考值必须为正数", nameof(referenceValue));

            if (string.IsNullOrEmpty(parameterName))
                parameterName = "voltage";

            string sessionId = CryptoHelper.GenerateUuid();

            var record = new CalibrationRecord
            {
                CalibrationId = sessionId,
                DeviceId = deviceId,
                CalibrationType = parameterName == "current" ? "VI_CURRENT" : "SPAN",
                CalibrationStatus = "in_progress",
                StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                PerformedBy = "SYSTEM",
                IsValid = 1
            };

            _sessions.TryAdd(sessionId, record);
            _calibrationRepository.InsertCalibrationRecord(record);

            try
            {
                // ---- 第一步：采集测量数据 ----
                int sampleCount = CalibrationConstants.SpanCalSamplesCount;
                var samples = new List<double>(sampleCount);

                for (int i = 0; i < sampleCount; i++)
                {
                    double value = ReadDeviceMeasurement(deviceId, parameterName);
                    samples.Add(value);
                    Thread.Sleep(20);
                }

                // ---- 第二步：稳定性判断 ----
                if (!IsStable(samples, out double stabilityDeviation))
                {
                    record.CalibrationStatus = "failed";
                    record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    record.IsValid = 0;
                    record.Notes = $"量程校准失败：数据不稳定（标准差={stabilityDeviation:F4}）";
                    UpdateAndPersist(record, sessionId);
                    return sessionId;
                }

                // ---- 第三步：计算增益系数 ----
                double measuredAvg = samples.Average();
                double gain = referenceValue / measuredAvg;

                // ---- 第四步：增益范围验证（0.95 ~ 1.05） ----
                bool isGainValid = gain >= 0.95 && gain <= 1.05;

                // ---- 第五步：记录校准点 ----
                record.CalibrationPoints = new List<CalibrationPoint>
                {
                    new CalibrationPoint
                    {
                        CalibrationId = sessionId,
                        ParameterName = parameterName,
                        PointIndex = 0,
                        ReferenceValue = referenceValue,
                        MeasuredValue = measuredAvg,
                        CorrectedValue = measuredAvg * gain,
                        DeviationPercent = Math.Abs(1.0 - gain) * 100.0,
                        IsPass = isGainValid ? 1 : 0
                    }
                };

                _calibrationRepository.InsertCalibrationPoints(record.CalibrationPoints);

                // ---- 第六步：更新校准状态 ----
                record.CalibrationStatus = isGainValid ? "completed" : "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                record.ValidUntil = isGainValid
                    ? DateTimeOffset.UtcNow.AddDays(CalibrationConstants.ValidityPeriodDays).ToUnixTimeMilliseconds()
                    : 0;
                record.IsValid = isGainValid ? 1 : 0;
                record.Notes = isGainValid
                    ? $"量程校准完成：参考值={referenceValue:F2}, 测量均值={measuredAvg:F4}, 增益={gain:F6}（参数: {parameterName}）"
                    : $"量程校准失败：增益={gain:F6}超出范围[0.95, 1.05]（参数: {parameterName}）";

                UpdateAndPersist(record, sessionId);
            }
            catch (Exception ex)
            {
                record.CalibrationStatus = "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                record.IsValid = 0;
                record.Notes = $"量程校准异常: {ex.Message}";
                UpdateAndPersist(record, sessionId);
            }

            return sessionId;
        }

        #endregion

        #region -- 线性校准（最小二乘法） --

        /// <summary>
        /// 执行线性校准（多点最小二乘法拟合）
        /// 使用最小二乘法对多组(参考值, 测量值)进行线性回归：
        ///   y = gain * x + offset
        /// 
        /// 公式：
        ///   gain  = (n*Σxy - Σx*Σy) / (n*Σx² - (Σx)²)
        ///   offset = (Σy - gain*Σx) / n
        ///   R²    = 1 - SS_res / SS_tot   （线性度指标）
        /// 
        /// 验证标准：
        ///   - 增益: 0.95 ~ 1.05
        ///   - 偏移: |offset| &lt; 100
        ///   - 线性度: R² >= 0.999
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValues">标准参考值集合</param>
        /// <param name="measuredValues">设备测量值集合（与参考值一一对应）</param>
        /// <returns>校准会话ID</returns>
        public string PerformLinearCalibration(string deviceId, List<double> referenceValues, List<double> measuredValues)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (referenceValues == null || measuredValues == null)
                throw new ArgumentException("参考值和测量值不能为null");

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
                PerformedBy = "SYSTEM",
                IsValid = 1
            };

            _sessions.TryAdd(sessionId, record);
            _calibrationRepository.InsertCalibrationRecord(record);

            try
            {
                int n = referenceValues.Count;

                // ---- 第一步：计算最小二乘法回归参数 ----
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
                    record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    record.IsValid = 0;
                    record.Notes = "线性校准失败：参考值方差为零，无法进行线性拟合";
                    UpdateAndPersist(record, sessionId);
                    return sessionId;
                }

                // 增益 gain = (n*Σxy - Σx*Σy) / (n*Σx² - Σx*Σx)
                double gain = (n * sumXY - sumX * sumY) / denominator;

                // 偏移 offset = (Σy - gain*Σx) / n
                double offset = (sumY - gain * sumX) / n;

                // ---- 第二步：计算线性度 R² ----
                double yMean = sumY / n;
                double ssRes = 0; // 残差平方和 Σ(y_actual - y_predicted)²
                double ssTot = 0; // 总平方和 Σ(y_actual - y_mean)²
                for (int i = 0; i < n; i++)
                {
                    double predicted = gain * referenceValues[i] + offset;
                    double residual = measuredValues[i] - predicted;
                    ssRes += residual * residual;
                    ssTot += (measuredValues[i] - yMean) * (measuredValues[i] - yMean);
                }

                double rSquared = ssTot > 1e-10 ? 1.0 - ssRes / ssTot : 0;

                // ---- 第三步：验证校准结果 ----
                bool isGainValid = gain >= 0.95 && gain <= 1.05;
                bool isOffsetValid = Math.Abs(offset) < 100;
                bool isLinearityValid = rSquared >= 0.999;

                // ---- 第四步：构建校准点记录 ----
                var points = new List<CalibrationPoint>();
                for (int i = 0; i < n; i++)
                {
                    double corrected = gain * measuredValues[i] + offset;
                    double deviation = Math.Abs(referenceValues[i] - measuredValues[i]);
                    double deviationPercent;
                    if (Math.Abs(referenceValues[i]) > 1e-10)
                        deviationPercent = deviation / Math.Abs(referenceValues[i]) * 100.0;
                    else
                        deviationPercent = deviation;

                    points.Add(new CalibrationPoint
                    {
                        CalibrationId = sessionId,
                        ParameterName = "voltage",
                        PointIndex = i,
                        ReferenceValue = referenceValues[i],
                        MeasuredValue = measuredValues[i],
                        CorrectedValue = corrected,
                        DeviationPercent = deviationPercent,
                        IsPass = deviationPercent < 1.0 ? 1 : 0
                    });
                }
                record.CalibrationPoints = points;

                _calibrationRepository.InsertCalibrationPoints(points);

                // ---- 第五步：判断整体校准是否通过 ----
                bool isOverallPass = isGainValid && isOffsetValid && isLinearityValid;

                record.CalibrationStatus = isOverallPass ? "completed" : "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                record.ValidUntil = isOverallPass
                    ? DateTimeOffset.UtcNow.AddDays(CalibrationConstants.ValidityPeriodDays).ToUnixTimeMilliseconds()
                    : 0;
                record.IsValid = isOverallPass ? 1 : 0;

                if (isOverallPass)
                {
                    record.Notes = $"线性校准完成：增益={gain:F6}, 偏移={offset:F4}, 线性度R²={rSquared:F6}";
                }
                else
                {
                    var failureReasons = new List<string>();
                    if (!isGainValid) failureReasons.Add($"增益不合格({gain:F6})");
                    if (!isOffsetValid) failureReasons.Add($"偏移过大({offset:F4})");
                    if (!isLinearityValid) failureReasons.Add($"线性度不足(R²={rSquared:F6})");
                    record.Notes = $"线性校准失败：{string.Join(", ", failureReasons)}";
                }

                UpdateAndPersist(record, sessionId);
            }
            catch (Exception ex)
            {
                record.CalibrationStatus = "failed";
                record.CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                record.IsValid = 0;
                record.Notes = $"线性校准异常: {ex.Message}";
                UpdateAndPersist(record, sessionId);
            }

            return sessionId;
        }

        #endregion

        #region -- 辅助方法 --

        /// <summary>
        /// 从设备读取单个测量值
        /// 通过设备监控服务获取设备最新数据快照，按参数名提取对应的物理参数值
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="parameterName">参数名称: voltage / current / power / temperature / soc / soh</param>
        /// <returns>测量值，设备离线或无数据时返回0</returns>
        private double ReadDeviceMeasurement(string deviceId, string parameterName)
        {
            var data = _monitorService.GetLatestData(deviceId);
            if (data != null)
            {
                switch (parameterName.ToLower())
                {
                    case "voltage":
                        return data.Voltage;
                    case "current":
                        return data.Current;
                    case "power":
                        return data.Power;
                    case "temperature":
                        return data.Temperature;
                    case "soc":
                        return data.Soc;
                    case "soh":
                        return data.Soh;
                    default:
                        return data.Value;
                }
            }
            return 0;
        }

        /// <summary>
        /// 判断采集数据的稳定性
        /// 计算样本集的变异系数（标准差/均值绝对值）
        /// 变异系数 < 0.5% 视为数据稳定
        /// </summary>
        /// <param name="samples">样本数据集合</param>
        /// <param name="standardDeviation">输出：样本标准差</param>
        /// <returns>数据是否稳定</returns>
        private bool IsStable(List<double> samples, out double standardDeviation)
        {
            standardDeviation = 0;
            if (samples == null || samples.Count < 2)
                return true;

            double mean = samples.Average();
            double sumSquaredDiff = samples.Sum(s => Math.Pow(s - mean, 2));
            standardDeviation = Math.Sqrt(sumSquaredDiff / (samples.Count - 1));

            // 变异系数 = 标准差 / |均值|
            double coefficientOfVariation;
            if (Math.Abs(mean) > 1e-10)
                coefficientOfVariation = standardDeviation / Math.Abs(mean);
            else
                coefficientOfVariation = standardDeviation;

            // 稳定标准：变异系数 < 0.005 (0.5%)
            return coefficientOfVariation < 0.005;
        }

        /// <summary>
        /// 通过CAN总线向设备发送校准参数
        /// 构建校准配置CAN帧，包含校准类型、增益系数和偏移量
        /// CAN ID根据设备CAN地址计算：基地址(0x100) | (CAN地址 << 8) | 校准命令码(0x03)
        /// 数据格式：[命令类型1B] [增益4B float] [偏移4B float]
        /// </summary>
        /// <param name="record">校准记录（包含校准点和计算结果）</param>
        /// <returns>发送是否成功</returns>
        private bool SendCalibrationParametersToDevice(CalibrationRecord record)
        {
            try
            {
                // 获取设备CAN地址
                var device = _deviceRepository.GetDeviceById(record.DeviceId);
                if (device == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CalibrationService] 设备不存在，无法发送校准参数");
                    return false;
                }

                int canAddress = device.CanAddress > 0 ? device.CanAddress : 1;

                // 计算CAN ID: 基地址 | 地址偏移 | 命令码
                uint canId = 0x100 | ((uint)canAddress << 8) | 0x03;

                // 从校准点数据中提取增益和偏移
                double gain = 1.0;
                double offset = 0;

                if (record.CalibrationPoints != null && record.CalibrationPoints.Count > 0)
                {
                    if (record.CalibrationType == "ZERO" || record.CalibrationType == "VI_CURRENT")
                    {
                        // 零点校准：偏移 = 第一个校准点的测量值
                        offset = -record.CalibrationPoints[0].MeasuredValue;
                    }
                    else if (record.CalibrationType == "SPAN" || record.CalibrationType == "VI_VOLTAGE")
                    {
                        // 量程校准：增益 = 参考值 / 测量值
                        var pt = record.CalibrationPoints[0];
                        gain = Math.Abs(pt.MeasuredValue) > 1e-10 ? pt.ReferenceValue / pt.MeasuredValue : 1.0;
                    }
                    else if (record.CalibrationType == "LINEAR")
                    {
                        // 线性校准：从多点数据中推算增益和偏移
                        gain = CalculateGainFromPoints(record.CalibrationPoints);
                        offset = CalculateOffsetFromPoints(record.CalibrationPoints);
                    }
                }

                // 构建CAN数据帧: [类型码1B] [增益4B float LE] [偏移4B float LE]
                byte[] data = new byte[9];
                byte typeCode = record.CalibrationType switch
                {
                    "ZERO" => 0x00,
                    "SPAN" => 0x01,
                    "LINEAR" => 0x02,
                    "VI_CURRENT" => 0x03,
                    "VI_VOLTAGE" => 0x04,
                    _ => 0x00
                };
                data[0] = typeCode;

                byte[] gainBytes = BitConverter.GetBytes((float)gain);
                byte[] offsetBytes = BitConverter.GetBytes((float)offset);
                Buffer.BlockCopy(gainBytes, 0, data, 1, 4);
                Buffer.BlockCopy(offsetBytes, 0, data, 5, 4);

                // 发送CAN消息（标准帧11位ID，非FD模式）
                bool result = _canService.SendCanMessage(canId, data, false, false);
                System.Diagnostics.Debug.WriteLine($"[CalibrationService] 发送校准参数 CAN ID=0x{canId:X4}, 类型={record.CalibrationType}, 增益={gain:F6}, 偏移={offset:F4}, 结果={result}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalibrationService] 发送校准参数异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从校准点集合中推算增益系数
        /// 对 ZERO/SPAN 类型取唯一点计算，对 LINEAR 类型使用多点最小二乘法
        /// </summary>
        private double CalculateGainFromPoints(List<CalibrationPoint> points)
        {
            if (points == null || points.Count == 0)
                return 1.0;

            if (points.Count == 1)
            {
                // 单点：gain = ref / measured
                var pt = points[0];
                return Math.Abs(pt.MeasuredValue) > 1e-10 ? pt.ReferenceValue / pt.MeasuredValue : 1.0;
            }

            // 多点：最小二乘法
            double sumX = points.Sum(p => p.ReferenceValue);
            double sumY = points.Sum(p => p.MeasuredValue);
            double sumXY = points.Sum(p => p.ReferenceValue * p.MeasuredValue);
            double sumX2 = points.Sum(p => p.ReferenceValue * p.ReferenceValue);
            int n = points.Count;

            double denominator = n * sumX2 - sumX * sumX;
            return Math.Abs(denominator) > 1e-10 ? (n * sumXY - sumX * sumY) / denominator : 1.0;
        }

        /// <summary>
        /// 从校准点集合中推算偏移量
        /// offset = (Σy - gain*Σx) / n
        /// </summary>
        private double CalculateOffsetFromPoints(List<CalibrationPoint> points)
        {
            if (points == null || points.Count == 0)
                return 0;

            double gain = CalculateGainFromPoints(points);
            double sumX = points.Sum(p => p.ReferenceValue);
            double sumY = points.Sum(p => p.MeasuredValue);
            return (sumY - gain * sumX) / points.Count;
        }

        /// <summary>
        /// 线性校准时采集设备测量值
        /// 对应每个参考值点，从设备读取当前测量数据
        /// 每个校准点采集多次取均值以提高精度
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="referenceValues">标准参考值列表</param>
        /// <returns>与参考值一一对应的测量值列表</returns>
        private List<double> CollectMeasuredValuesForLinearCalibration(string deviceId, List<double> referenceValues)
        {
            var measuredValues = new List<double>();
            int samplesPerPoint = 5; // 每个校准点采集5次取均值

            for (int i = 0; i < referenceValues.Count; i++)
            {
                // 等待设备信号稳定后再采集
                Thread.Sleep(50);

                var pointSamples = new List<double>(samplesPerPoint);
                for (int j = 0; j < samplesPerPoint; j++)
                {
                    double value = ReadDeviceMeasurement(deviceId, "voltage");
                    pointSamples.Add(value);
                    Thread.Sleep(10);
                }

                double measuredAvg = pointSamples.Average();
                measuredValues.Add(measuredAvg);

                System.Diagnostics.Debug.WriteLine($"[CalibrationService] 线性校准采集点{i}: 参考值={referenceValues[i]:F2}, 测量均值={measuredAvg:F4}");
            }

            return measuredValues;
        }

        /// <summary>
        /// 更新内存中的校准记录并持久化到数据库
        /// 同时更新校准状态和完成时间
        /// </summary>
        /// <param name="record">校准记录</param>
        /// <param name="sessionId">会话ID</param>
        private void UpdateAndPersist(CalibrationRecord record, string sessionId)
        {
            _sessions.AddOrUpdate(sessionId, record, (_, _) => record);
            _calibrationRepository.UpdateCalibrationStatus(sessionId, record.CalibrationStatus);

            System.Diagnostics.Debug.WriteLine($"[CalibrationService] 校准结果: {sessionId}, 状态={record.CalibrationStatus}, {record.Notes}");
        }

        #endregion
    }
}
