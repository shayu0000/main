using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Hardware.ZlgCanCard;
using ChargeDischargeSystem.Hardware.Mock;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: CAN通信服务实现类
// 说明: 封装周立功CAN卡的所有操作，支持真实CAN卡与模拟CAN卡切换
//       提供线程安全的消息收发、统计和事件通知机制
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// CAN通信服务实现类
    /// 负责CAN总线通信的完整生命周期管理：
    ///   1. 根据配置创建真实CAN卡或模拟CAN卡实例
    ///   2. 管理CAN通道的开启、关闭和消息收发
    ///   3. 通过事件机制向外部分发接收到的CAN消息
    ///   4. 提供线程安全的消息发送和统计功能
    /// </summary>
    public class CanCommunicationService : ICanCommunicationService, IDisposable
    {
        #region -- 字段定义 --

        /// <summary>CAN卡接口引用（运行时根据配置注入具体实现）</summary>
        private ICanCard _canCard;

        /// <summary>CAN配置参数段（从AppConfig中获取）</summary>
        private readonly CanConfigSection _canConfig;

        /// <summary>CAN通信统计信息</summary>
        private CanStatistics _statistics;

        /// <summary>服务运行状态标志</summary>
        private bool _isRunning;

        /// <summary>对象销毁标志，防止重复释放</summary>
        private bool _isDisposed;

        /// <summary>统计信息读写锁</summary>
        private readonly object _statsLock = new object();

        /// <summary>事件订阅者锁，确保事件操作线程安全</summary>
        private readonly object _eventLock = new object();

        #endregion

        #region -- 事件声明 --

        /// <summary>CAN消息接收事件</summary>
        public event Action<CanMessage> OnCanMessageReceived;

        /// <summary>CAN通信错误事件</summary>
        public event Action<string> OnCanError;

        /// <summary>连接状态变化事件</summary>
        public event Action<bool> OnConnectionStatusChanged;

        #endregion

        #region -- 公开属性 --

        /// <summary>CAN服务是否正在运行</summary>
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    // 触发连接状态变更通知
                    OnConnectionStatusChanged?.Invoke(value);
                }
            }
        }

        #endregion

        /// <summary>
        /// 构造CAN通信服务实例
        /// </summary>
        /// <param name="config">应用程序配置，从中读取CAN通信相关参数</param>
        public CanCommunicationService(AppConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _canConfig = config.CanConfig ?? new CanConfigSection();
            _statistics = new CanStatistics();
            _isRunning = false;
        }

        #region -- CAN服务生命周期管理 --

        /// <summary>
        /// 启动CAN服务
        /// 根据配置中的 UseMock 标志决定创建真实CAN卡或模拟CAN卡实例
        /// 初始化CAN卡、打开通道并订阅底层事件
        /// </summary>
        /// <returns>启动是否成功</returns>
        public bool StartCanService()
        {
            if (_isRunning)
            {
                RaiseError("CAN服务已在运行中，请先停止当前服务。");
                return false;
            }

            try
            {
                // ---- 第一步：根据配置创建CAN卡实例 ----
                if (_canConfig.UseMock)
                {
                    // 使用模拟CAN卡（测试/调试模式）
                    _canCard = new MockCanCard();
                    System.Diagnostics.Debug.WriteLine("[CanCommunicationService] 使用模拟CAN卡");
                }
                else
                {
                    // 使用真实周立功CAN卡（生产模式）
                    _canCard = new ZlgCanCard();
                    System.Diagnostics.Debug.WriteLine("[CanCommunicationService] 使用真实周立功CAN卡");
                }

                // ---- 第二步：构建CAN配置并初始化 ----
                var canConfig = new CanConfig
                {
                    DeviceIndex = _canConfig.DeviceIndex,
                    ChannelIndex = _canConfig.ChannelIndex,
                    Bitrate = _canConfig.Bitrate,
                    DataBitrate = _canConfig.DataBitrate,
                    EnableCanFd = _canConfig.EnableCanFd,
                    WorkMode = 0 // 正常模式
                };

                if (!_canCard.Initialize(canConfig))
                {
                    RaiseError("CAN卡初始化失败。");
                    return false;
                }

                // ---- 第三步：订阅底层CAN卡事件 ----
                _canCard.OnMessageReceived += HandleCanMessageReceived;
                _canCard.OnError += HandleCanError;

                // ---- 第四步：打开CAN通道 ----
                if (!_canCard.Open())
                {
                    RaiseError("CAN通道打开失败。");
                    return false;
                }

                // ---- 第五步：更新状态 ----
                IsRunning = true;
                System.Diagnostics.Debug.WriteLine("[CanCommunicationService] CAN服务启动成功");
                return true;
            }
            catch (Exception ex)
            {
                RaiseError($"CAN服务启动异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止CAN服务
        /// 取消底层事件订阅、关闭CAN通道、释放硬件资源
        /// </summary>
        public void StopCanService()
        {
            if (!_isRunning) return;

            try
            {
                // 取消事件订阅
                if (_canCard != null)
                {
                    _canCard.OnMessageReceived -= HandleCanMessageReceived;
                    _canCard.OnError -= HandleCanError;
                    _canCard.Close();
                    _canCard.Dispose();
                    _canCard = null;
                }

                IsRunning = false;
                System.Diagnostics.Debug.WriteLine("[CanCommunicationService] CAN服务已停止");
            }
            catch (Exception ex)
            {
                RaiseError($"CAN服务停止异常: {ex.Message}");
            }
        }

        #endregion

        #region -- CAN消息发送 --

        /// <summary>
        /// 发送单条CAN消息
        /// 构建CanMessage对象并通过底层CAN卡发送
        /// </summary>
        /// <param name="canId">CAN消息ID</param>
        /// <param name="data">数据载荷</param>
        /// <param name="isExtended">是否为扩展帧</param>
        /// <param name="isFd">是否为CAN FD帧</param>
        /// <returns>发送是否成功</returns>
        public bool SendCanMessage(uint canId, byte[] data, bool isExtended = false, bool isFd = false)
        {
            if (!_isRunning || _canCard == null)
            {
                RaiseError("CAN服务未运行，无法发送消息。");
                return false;
            }

            try
            {
                var message = new CanMessage
                {
                    CanId = canId,
                    Data = data ?? Array.Empty<byte>(),
                    Length = (byte)(data?.Length ?? 0),
                    IsExtended = isExtended,
                    IsFd = isFd,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                bool success = _canCard.SendMessage(message);

                // 更新统计信息
                lock (_statsLock)
                {
                    _statistics.TxCount++;
                    if (!success) _statistics.TxErrorCount++;
                    _statistics.LastReceiveTime = DateTime.Now;
                }

                return success;
            }
            catch (Exception ex)
            {
                lock (_statsLock)
                {
                    _statistics.TxCount++;
                    _statistics.TxErrorCount++;
                }
                RaiseError($"发送CAN消息失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量发送CAN消息
        /// 按指定间隔依次发送消息列表中的所有消息
        /// </summary>
        /// <param name="messages">CAN消息列表</param>
        /// <param name="intervalMs">消息发送间隔（毫秒）</param>
        /// <returns>成功发送的消息数量</returns>
        public int SendBatchMessages(List<CanMessage> messages, double intervalMs = 1.0)
        {
            if (!_isRunning || _canCard == null)
            {
                RaiseError("CAN服务未运行，无法批量发送消息。");
                return 0;
            }

            if (messages == null || messages.Count == 0)
                return 0;

            int successCount = 0;
            int intervalTicks = (int)(intervalMs * TimeSpan.TicksPerMillisecond);

            foreach (var message in messages)
            {
                try
                {
                    if (_canCard.SendMessage(message))
                        successCount++;

                    // 消息间隔控制（避免总线拥塞）
                    if (intervalMs > 0)
                        Thread.Sleep(new TimeSpan(intervalTicks));
                }
                catch (Exception ex)
                {
                    RaiseError($"批量发送CAN消息过程中出错: {ex.Message}");
                }
            }

            lock (_statsLock)
            {
                _statistics.TxCount += messages.Count;
                _statistics.TxErrorCount += (messages.Count - successCount);
            }

            return successCount;
        }

        #endregion

        #region -- 统计信息 --

        /// <summary>
        /// 获取CAN通信统计信息
        /// 返回当前统计数据的深拷贝，避免外部修改
        /// </summary>
        /// <returns>CAN通信统计数据</returns>
        public CanStatistics GetCanStatistics()
        {
            lock (_statsLock)
            {
                return new CanStatistics
                {
                    TxCount = _statistics.TxCount,
                    RxCount = _statistics.RxCount,
                    TxErrorCount = _statistics.TxErrorCount,
                    RxErrorCount = _statistics.RxErrorCount,
                    BusStatus = _statistics.BusStatus,
                    LastReceiveTime = _statistics.LastReceiveTime,
                    BusErrorCount = _statistics.BusErrorCount
                };
            }
        }

        #endregion

        #region -- 事件处理与分发 --

        /// <summary>
        /// 处理底层CAN卡接收到的消息（回调函数）
        /// 更新统计信息并通过事件分发给外部订阅者
        /// 此方法在底层接收线程中执行，需要保证线程安全
        /// </summary>
        /// <param name="message">接收到的CAN消息</param>
        private void HandleCanMessageReceived(CanMessage message)
        {
            // 更新统计信息
            lock (_statsLock)
            {
                _statistics.RxCount++;
                _statistics.LastReceiveTime = DateTime.Now;
            }

            // 线程安全地触发事件
            Action<CanMessage> handler;
            lock (_eventLock)
            {
                handler = OnCanMessageReceived;
            }
            handler?.Invoke(message);
        }

        /// <summary>
        /// 处理底层CAN卡报告的错误（回调函数）
        /// </summary>
        /// <param name="errorMessage">错误描述信息</param>
        private void HandleCanError(string errorMessage)
        {
            lock (_statsLock)
            {
                _statistics.BusErrorCount++;
            }

            Action<string> handler;
            lock (_eventLock)
            {
                handler = OnCanError;
            }
            handler?.Invoke(errorMessage);
        }

        /// <summary>
        /// 内部错误报告方法
        /// 统一通过 OnCanError 事件向外部发布错误信息
        /// </summary>
        /// <param name="message">错误信息</param>
        private void RaiseError(string message)
        {
            Action<string> handler;
            lock (_eventLock)
            {
                handler = OnCanError;
            }
            handler?.Invoke($"[CanCommunicationService] {message}");
        }

        #endregion

        #region -- IDisposable 实现 --

        /// <summary>
        /// 释放CAN通信服务占用的所有资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopCanService();
        }

        #endregion
    }
}
