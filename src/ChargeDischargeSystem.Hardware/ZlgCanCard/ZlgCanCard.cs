using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// ============================================================
// 命名空间: ChargeDischargeSystem.Hardware.ZlgCanCard
// 功能描述: 周立功CAN卡硬件驱动封装
// 说明: 封装周立功USBCAN系列CAN卡的API调用，包括CAN 2.0B和CAN FD协议
// 注意: 此实现依赖周立功官方提供的ControlCAN.dll动态库
//       开发者需要将ControlCAN.dll放置在可执行文件目录下
// ============================================================
namespace ChargeDischargeSystem.Hardware.ZlgCanCard
{
    /// <summary>
    /// 周立功CAN卡硬件驱动封装类
    /// 通过P/Invoke调用周立功原生DLL实现CAN通信
    /// </summary>
    public class ZlgCanCard : ICanCard
    {
        #region -- 字段定义 --

        private CanConfig _config = new CanConfig();
        private bool _isOpen = false;
        private bool _isDisposed = false;
        private Thread _receiveThread = null;
        private CancellationTokenSource _cancelTokenSource;

        // CAN通信统计
        private long _txCount = 0;
        private long _rxCount = 0;
        private long _txErrorCount = 0;
        private long _rxErrorCount = 0;
        private long _busErrorCount = 0;
        private DateTime? _lastReceiveTime;

        #endregion

        #region -- 事件声明 --

        /// <summary>CAN卡打开成功事件</summary>
        public event Action OnOpened;

        /// <summary>CAN卡关闭事件</summary>
        public event Action OnClosed;

        /// <summary>接收到CAN消息事件</summary>
        public event Action<CanMessage> OnMessageReceived;

        /// <summary>通信错误事件</summary>
        public event Action<string> OnError;

        #endregion

        #region -- 属性 --

        /// <summary>CAN卡是否已打开</summary>
        public bool IsOpen => _isOpen;

        #endregion

        #region -- 周立功DLL结构体定义 --

        // 周立功CAN卡API常量
        private const int VCI_USBCAN2 = 4;        // USBCAN-II设备类型
        private const int STATUS_OK = 1;            // 操作成功
        private const int STATUS_ERR = 0;           // 操作失败

        // 注意：以下代码提供周立功CAN卡API的调用框架
        // 实际开发时需要引入ControlCAN.dll并正确配置P/Invoke签名
        // 以下仅为接口调用示意，具体DLL函数声明需要参照周立功官方开发文档

        #endregion

        #region -- 初始化和生命周期 --

        /// <summary>
        /// 初始化CAN卡设备，设置工作参数
        /// </summary>
        /// <param name="config">CAN配置参数</param>
        /// <returns>初始化是否成功</returns>
        public bool Initialize(CanConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 实际实现时需要调用:
            // VCI_OpenDevice(config.DeviceType, config.DeviceIndex, 0);
            // VCI_InitCAN(config.DeviceType, config.DeviceIndex, config.ChannelIndex, ref initConfig);

            Console.WriteLine($"[ZlgCanCard] 初始化CAN卡: 设备索引={_config.DeviceIndex}, " +
                              $"通道={_config.ChannelIndex}, 波特率={_config.Bitrate}bps");
            return true;
        }

        /// <summary>
        /// 打开CAN通道，开始通信
        /// 启动接收线程持续监听CAN总线数据
        /// </summary>
        public bool Open()
        {
            if (_isOpen)
            {
                Console.WriteLine("[ZlgCanCard] CAN卡已经打开");
                return true;
            }

            try
            {
                // 实际实现时需要调用:
                // VCI_StartCAN(config.DeviceType, config.DeviceIndex, config.ChannelIndex);

                _isOpen = true;
                _cancelTokenSource = new CancellationTokenSource();

                // 启动CAN消息接收线程
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "ZLG_CAN_Receive"
                };
                _receiveThread.Start();

                Console.WriteLine($"[ZlgCanCard] CAN卡打开成功");
                OnOpened?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZlgCanCard] CAN卡打开失败: {ex.Message}");
                OnError?.Invoke($"CAN卡打开失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 关闭CAN通道，停止通信
        /// 停止接收线程并释放资源
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _cancelTokenSource?.Cancel();

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                _receiveThread.Join(3000); // 等待接收线程结束，最多3秒
            }

            // 实际实现时需要调用:
            // VCI_CloseDevice(config.DeviceType, config.DeviceIndex);

            Console.WriteLine($"[ZlgCanCard] CAN卡已关闭, 总发送:{_txCount}, 总接收:{_rxCount}");
            OnClosed?.Invoke();
        }

        #endregion

        #region -- 消息收发 --

        /// <summary>
        /// 发送单条CAN消息到总线
        /// </summary>
        /// <param name="message">要发送的CAN消息</param>
        /// <returns>发送是否成功</returns>
        public bool SendMessage(CanMessage message)
        {
            if (!_isOpen)
            {
                OnError?.Invoke("CAN卡未打开，无法发送消息");
                return false;
            }

            if (message == null) return false;

            try
            {
                // 实际实现时需要填充VCI_CAN_OBJ结构体并调用VCI_Transmit
                Console.WriteLine($"[ZlgCanCard] 发送CAN消息: ID=0x{message.CanId:X}, " +
                                  $"数据={BitConverter.ToString(message.Data)}");

                Interlocked.Increment(ref _txCount);
                return true;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _txErrorCount);
                OnError?.Invoke($"CAN消息发送失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量发送CAN消息
        /// 用于固件升级等需要快速连续发送大量数据的场景
        /// </summary>
        /// <param name="messages">消息列表</param>
        /// <param name="intervalMs">消息间隔(毫秒)</param>
        /// <returns>成功发送数量</returns>
        public int SendBatchMessages(IEnumerable<CanMessage> messages, double intervalMs = 1.0)
        {
            if (!_isOpen)
            {
                OnError?.Invoke("CAN卡未打开，无法批量发送");
                return 0;
            }

            int successCount = 0;
            int intervalTicks = (int)(intervalMs * 10000); // 转换为100纳秒单位

            foreach (var msg in messages)
            {
                if (SendMessage(msg))
                    successCount++;
                else
                    break; // 发送失败则终止批量发送

                // 消息间隔控制
                if (intervalTicks > 0)
                    Thread.Sleep((int)intervalMs);
            }

            Console.WriteLine($"[ZlgCanCard] 批量发送完成: {successCount} 条");
            return successCount;
        }

        #endregion

        #region -- 接收线程 --

        /// <summary>
        /// CAN消息接收主循环
        /// 在独立线程中持续轮询CAN总线数据
        /// </summary>
        private void ReceiveLoop()
        {
            Console.WriteLine("[ZlgCanCard] 接收线程已启动");

            while (_isOpen && !_cancelTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // 实际实现时需要调用VCI_Receive获取CAN帧
                    // VCI_CAN_OBJ[] frames = new VCI_CAN_OBJ[100];
                    // int count = VCI_Receive(config.DeviceType, config.DeviceIndex, config.ChannelIndex, frames, 100, 100);

                    // 模拟数据接收 - 实际项目中删除此段代码
                    // 每隔一段时间模拟接收一条消息用于调试

                    Thread.Sleep(100); // 轮询间隔100ms，实际可调整
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _rxErrorCount);
                    OnError?.Invoke($"CAN接收错误: {ex.Message}");
                    Thread.Sleep(1000); // 出错后等待1秒再重试
                }
            }

            Console.WriteLine("[ZlgCanCard] 接收线程已退出");
        }

        #endregion

        #region -- 统计信息 --

        /// <summary>
        /// 获取CAN通信统计信息
        /// 包含发送/接收计数、错误计数和总线状态
        /// </summary>
        public CanStatistics GetStatistics()
        {
            return new CanStatistics
            {
                TxCount = _txCount,
                RxCount = _rxCount,
                TxErrorCount = _txErrorCount,
                RxErrorCount = _rxErrorCount,
                BusErrorCount = _busErrorCount,
                BusStatus = _isOpen ? "OK" : "Offline",
                LastReceiveTime = _lastReceiveTime
            };
        }

        #endregion

        #region -- 资源释放 --

        /// <summary>
        /// 释放CAN卡资源
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                Close();
                _cancelTokenSource?.Dispose();
                Console.WriteLine("[ZlgCanCard] 资源已释放");
            }
        }

        #endregion
    }
}
