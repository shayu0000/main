using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Hardware.Mock
// 功能描述: 模拟CAN卡实现
// 说明: 在不连接实际CAN硬件时，使用模拟数据测试上位机软件功能
//       测试人员可以在配置中切换使用真实CAN卡或模拟CAN卡
// ============================================================
namespace ChargeDischargeSystem.Hardware.Mock
{
    /// <summary>
    /// 模拟CAN卡实现类
    /// 用于开发和测试阶段，无需连接实际CAN硬件即可运行上位机
    /// 生成模拟的充放电设备数据（电压、电流、温度、SOC等）
    /// </summary>
    public class MockCanCard : ICanCard
    {
        private bool _isOpen = false;
        private bool _isDisposed = false;
        private Thread _simulateThread = null;
        private CancellationTokenSource _cancelTokenSource;
        private readonly Random _random = new Random();

        // 模拟设备参数
        private double _baseVoltage = 800.0;
        private double _baseCurrent = 500.0;
        private double _baseTemperature = 35.0;
        private double _baseSoc = 75.0;

        // 统计
        private long _txCount = 0;
        private long _rxCount = 0;

        public event Action OnOpened;
        public event Action OnClosed;
        public event Action<CanMessage> OnMessageReceived;
        public event Action<string> OnError;
        public bool IsOpen => _isOpen;

        /// <summary>
        /// 初始化模拟CAN卡
        /// </summary>
        public bool Initialize(CanConfig config)
        {
            Console.WriteLine($"[MockCanCard] 初始化模拟CAN卡 - 设备:{config.DeviceIndex}, 波特率:{config.Bitrate}bps");
            return true;
        }

        /// <summary>
        /// 打开模拟CAN卡，开始生成模拟数据
        /// </summary>
        public bool Open()
        {
            if (_isOpen) return true;

            _isOpen = true;
            _cancelTokenSource = new CancellationTokenSource();

            _simulateThread = new Thread(SimulateLoop)
            {
                IsBackground = true,
                Name = "MockCAN_Simulate"
            };
            _simulateThread.Start();

            Console.WriteLine("[MockCanCard] 模拟CAN卡已启动");
            OnOpened?.Invoke();
            return true;
        }

        /// <summary>
        /// 关闭模拟CAN卡
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _cancelTokenSource?.Cancel();

            if (_simulateThread != null && _simulateThread.IsAlive)
                _simulateThread.Join(2000);

            Console.WriteLine($"[MockCanCard] 模拟CAN卡已关闭, 总发送:{_txCount}, 总接收:{_rxCount}");
            OnClosed?.Invoke();
        }

        /// <summary>
        /// 发送消息 - 模拟实现，直接回调触发接收事件
        /// </summary>
        public bool SendMessage(CanMessage message)
        {
            if (!_isOpen) return false;

            _txCount++;
            Console.WriteLine($"[MockCanCard] 模拟发送: ID=0x{message.CanId:X}");

            // 模拟消息发送后的响应
            Thread.Sleep(5);
            return true;
        }

        /// <summary>
        /// 批量发送
        /// </summary>
        public int SendBatchMessages(IEnumerable<CanMessage> messages, double intervalMs = 1.0)
        {
            int count = 0;
            foreach (var msg in messages)
            {
                if (SendMessage(msg))
                    count++;
                if (intervalMs > 0)
                    Thread.Sleep((int)intervalMs);
            }
            return count;
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public CanStatistics GetStatistics()
        {
            return new CanStatistics
            {
                TxCount = _txCount,
                RxCount = _rxCount,
                BusStatus = "OK (模拟)"
            };
        }

        /// <summary>
        /// 模拟数据生成循环
        /// 周期性地生成模拟的充放电设备CAN报文
        /// </summary>
        private void SimulateLoop()
        {
            Console.WriteLine("[MockCanCard] 模拟数据生成线程已启动");

            while (_isOpen && !_cancelTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // 生成模拟电压数据 (CAN ID: 0x181)
                    GenerateAndPublishVoltageData();
                    Thread.Sleep(50);

                    // 生成模拟电流数据 (CAN ID: 0x182)
                    GenerateAndPublishCurrentData();
                    Thread.Sleep(50);

                    // 生成模拟温度数据 (CAN ID: 0x183)
                    GenerateAndPublishTemperatureData();
                    Thread.Sleep(50);

                    // 生成SOC/SOH数据 (CAN ID: 0x184)
                    GenerateAndPublishSocData();
                    Thread.Sleep(50);

                    // 设备状态数据 (CAN ID: 0x185)
                    GenerateAndPublishStatusData();
                    Thread.Sleep(50);

                    // 每100次循环随机生成一次告警
                    if (_random.Next(100) == 0)
                    {
                        GenerateAndPublishAlarmData();
                    }

                    // 模拟数据变化趋势
                    UpdateBaseValues();
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MockCanCard] 模拟数据生成异常: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }

            Console.WriteLine("[MockCanCard] 模拟数据生成线程已退出");
        }

        /// <summary>
        /// 生成模拟电压数据报文
        /// 电压值在基础值±5%范围内随机波动，模拟实际测量噪声
        /// </summary>
        private void GenerateAndPublishVoltageData()
        {
            double noise = (_random.NextDouble() - 0.5) * _baseVoltage * 0.02; // ±1%噪声
            double voltage = _baseVoltage + noise;

            // 将电压值转换为CAN报文格式 (0.1V精度, 大端序)
            ushort rawValue = (ushort)(voltage * 10);
            byte[] data = new byte[8];
            data[0] = (byte)(rawValue >> 8);
            data[1] = (byte)(rawValue & 0xFF);
            data[2] = 0x80; // 数据质量: GOOD

            var msg = new CanMessage
            {
                CanId = 0x181,
                Data = data,
                Length = 8,
                IsExtended = false,
                IsFd = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Interlocked.Increment(ref _rxCount);
            OnMessageReceived?.Invoke(msg);
        }

        /// <summary>
        /// 生成模拟电流数据报文
        /// </summary>
        private void GenerateAndPublishCurrentData()
        {
            double noise = (_random.NextDouble() - 0.5) * _baseCurrent * 0.03;
            double current = _baseCurrent + noise;

            // 有符号16位 (0.01A精度)
            short rawValue = (short)(current * 100);
            byte[] data = new byte[8];
            data[0] = (byte)(rawValue >> 8);
            data[1] = (byte)(rawValue & 0xFF);
            data[2] = 0x80; // GOOD

            var msg = new CanMessage
            {
                CanId = 0x182,
                Data = data,
                Length = 8,
                IsExtended = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Interlocked.Increment(ref _rxCount);
            OnMessageReceived?.Invoke(msg);
        }

        /// <summary>
        /// 生成模拟温度数据报文
        /// </summary>
        private void GenerateAndPublishTemperatureData()
        {
            double noise = (_random.NextDouble() - 0.5) * 4.0; // ±2°C波动
            double temp = _baseTemperature + noise;

            byte[] data = new byte[8];
            data[0] = (byte)((int)temp & 0xFF);       // 整数部分
            data[1] = (byte)(((int)(temp * 10) % 10) & 0x0F); // 小数部分
            data[2] = 0x80; // GOOD

            var msg = new CanMessage
            {
                CanId = 0x183,
                Data = data,
                Length = 8,
                IsExtended = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Interlocked.Increment(ref _rxCount);
            OnMessageReceived?.Invoke(msg);
        }

        /// <summary>
        /// 生成模拟SOC数据报文
        /// </summary>
        private void GenerateAndPublishSocData()
        {
            byte[] data = new byte[8];
            data[0] = (byte)(_baseSoc * 2); // 0~200对应0%~100%
            data[1] = 0x4B; // SOH=75%
            data[2] = 0x80; // GOOD

            var msg = new CanMessage
            {
                CanId = 0x184,
                Data = data,
                Length = 8,
                IsExtended = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Interlocked.Increment(ref _rxCount);
            OnMessageReceived?.Invoke(msg);
        }

        /// <summary>
        /// 生成设备状态数据报文
        /// </summary>
        private void GenerateAndPublishStatusData()
        {
            byte[] data = new byte[8];
            data[0] = 0x01; // 运行中
            data[1] = 0x00; // 无故障

            var msg = new CanMessage
            {
                CanId = 0x185,
                Data = data,
                Length = 8,
                IsExtended = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            OnMessageReceived?.Invoke(msg);
        }

        /// <summary>
        /// 随机生成模拟告警报文
        /// </summary>
        private void GenerateAndPublishAlarmData()
        {
            int faultCode = _random.Next(0x0001, 0x0005);
            byte[] data = new byte[8];
            data[0] = (byte)(faultCode >> 8);
            data[1] = (byte)(faultCode & 0xFF);
            data[2] = 0x01; // 告警有效

            var msg = new CanMessage
            {
                CanId = (uint)(0x200 + _random.Next(1, 5)),
                Data = data,
                Length = 8,
                IsExtended = false,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Console.WriteLine($"[MockCanCard] 模拟告警: 故障码=0x{faultCode:X4}");
            OnMessageReceived?.Invoke(msg);
        }

        /// <summary>
        /// 更新模拟数据基础值，模拟充放电过程中的参数变化
        /// </summary>
        private void UpdateBaseValues()
        {
            // 模拟充电过程中电压逐渐上升
            _baseVoltage += (_random.NextDouble() - 0.48) * 0.5;
            _baseVoltage = Math.Clamp(_baseVoltage, 500, 900);

            // 模拟温度缓慢上升
            _baseTemperature += (_random.NextDouble() - 0.45) * 0.1;
            _baseTemperature = Math.Clamp(_baseTemperature, 25, 45);

            // SOC缓慢上升（模拟充电）
            _baseSoc += (_random.NextDouble() - 0.3) * 0.01;
            _baseSoc = Math.Clamp(_baseSoc, 10, 95);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                Close();
                _cancelTokenSource?.Dispose();
            }
        }
    }
}
