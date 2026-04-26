using System;
using System.Collections.Generic;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 服务层辅助数据模型定义
// 说明: 定义各服务所需的传输对象(DTO)、配置类、进度信息等辅助类型
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 设备实时数据点
    /// 表示某一时刻从设备采集到的完整数据快照
    /// </summary>
    public class DeviceDataPoint
    {
        public string DeviceId { get; set; }
        public long Timestamp { get; set; }

        /// <summary>参数名称: voltage/current/power/temperature/soc 或 AC特定参数</summary>
        public string ParameterName { get; set; } = string.Empty;

        /// <summary>参数值</summary>
        public double Value { get; set; }

        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Power { get; set; }
        public double Temperature { get; set; }
        public double Soc { get; set; }
        public double Soh { get; set; }
        public double CellMaxVoltage { get; set; }
        public double CellMinVoltage { get; set; }
        public double CellMaxTemperature { get; set; }
        public double CellMinTemperature { get; set; }
        public string Quality { get; set; } = "GOOD";
        public Dictionary<string, double> ExtraParams { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// 固件升级进度信息
    /// 用于回调通知升级过程中各阶段的进度状态
    /// </summary>
    public class UpgradeProgress
    {
        /// <summary>升级任务ID</summary>
        public string TaskId { get; set; }

        /// <summary>设备ID</summary>
        public string DeviceId { get; set; }

        /// <summary>当前升级状态: IDLE/CHECK_VERSION/ENTER_BOOTLOADER/ERASE_FLASH/TRANSFER_DATA/VERIFY_FW/REBOOT_DEVICE/COMPLETED</summary>
        public string CurrentState { get; set; }

        /// <summary>总进度百分比(0~100)</summary>
        public double ProgressPercent { get; set; }

        /// <summary>已传输字节数</summary>
        public long BytesTransferred { get; set; }

        /// <summary>固件总字节数</summary>
        public long TotalBytes { get; set; }

        /// <summary>当前数据块序号</summary>
        public int CurrentBlockIndex { get; set; }

        /// <summary>总数据块数</summary>
        public int TotalBlocks { get; set; }

        /// <summary>状态描述消息</summary>
        public string Message { get; set; }

        /// <summary>是否有错误发生</summary>
        public bool HasError { get; set; }

        /// <summary>错误信息</summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 故障录波触发配置
    /// 定义故障录波功能的触发条件和采集参数
    /// </summary>
    public class FaultTriggerConfig
    {
        /// <summary>故障录波是否启用</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>触发通道名称（如 dc_voltage / dc_current）</summary>
        public string TriggerChannel { get; set; } = "dc_voltage";

        /// <summary>触发模式: rising(上升沿) / falling(下降沿) / absolute(绝对值)</summary>
        public string TriggerMode { get; set; } = "absolute";

        /// <summary>触发阈值</summary>
        public double TriggerThreshold { get; set; }

        /// <summary>触发滞回值</summary>
        public double Hysteresis { get; set; } = 1.0;

        /// <summary>故障前录波时长(秒)，默认2秒</summary>
        public double PreFaultDurationS { get; set; } = 2.0;

        /// <summary>故障后录波时长(秒)，默认3秒</summary>
        public double PostFaultDurationS { get; set; } = 3.0;

        /// <summary>录波采样率(Hz)，默认1000Hz高速采样</summary>
        public double SampleRateHz { get; set; } = 1000.0;

        /// <summary>录波通道列表</summary>
        public List<string> RecordChannels { get; set; } = new List<string> { "dc_voltage", "dc_current", "temperature" };
    }

    /// <summary>
    /// 数据缓冲区
    /// 用于数据记录服务，提供批量数据缓存和定时刷新功能
    /// </summary>
    /// <typeparam name="T">缓冲数据类型</typeparam>
    public class DataBuffer<T>
    {
        private readonly List<T> _buffer = new List<T>();
        private readonly object _lock = new object();
        private readonly int _maxSize;

        /// <summary>缓冲区中当前数据条数</summary>
        public int Count
        {
            get { lock (_lock) return _buffer.Count; }
        }

        /// <summary>缓冲区是否已满</summary>
        public bool IsFull
        {
            get { lock (_lock) return _buffer.Count >= _maxSize; }
        }

        /// <summary>
        /// 构造数据缓冲区
        /// </summary>
        /// <param name="maxSize">最大缓冲条目数</param>
        public DataBuffer(int maxSize = 1000)
        {
            _maxSize = maxSize;
        }

        /// <summary>
        /// 向缓冲区添加一条数据
        /// </summary>
        /// <param name="item">数据条目</param>
        public void Add(T item)
        {
            lock (_lock)
            {
                _buffer.Add(item);
            }
        }

        /// <summary>
        /// 批量添加数据
        /// </summary>
        /// <param name="items">数据条目集合</param>
        public void AddRange(IEnumerable<T> items)
        {
            lock (_lock)
            {
                _buffer.AddRange(items);
            }
        }

        /// <summary>
        /// 取出并清空缓冲区中的所有数据
        /// </summary>
        /// <returns>缓冲区中所有数据</returns>
        public List<T> Flush()
        {
            lock (_lock)
            {
                var result = new List<T>(_buffer);
                _buffer.Clear();
                return result;
            }
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _buffer.Clear();
            }
        }
    }

    /// <summary>
    /// 循环缓冲区（环形缓冲区）
    /// 用于故障录波服务，存储故障前后固定窗口长度的历史数据
    /// 当缓冲区满时，新数据会覆盖最旧的数据
    /// </summary>
    /// <typeparam name="T">缓冲数据类型</typeparam>
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head = 0; // 写入位置
        private int _count = 0;
        private readonly object _lock = new object();

        /// <summary>缓冲区容量</summary>
        public int Capacity { get; }

        /// <summary>当前数据条数</summary>
        public int Count
        {
            get { lock (_lock) return _count; }
        }

        /// <summary>
        /// 构造循环缓冲区
        /// </summary>
        /// <param name="capacity">缓冲区容量（最大条目数）</param>
        public CircularBuffer(int capacity)
        {
            Capacity = capacity;
            _buffer = new T[capacity];
        }

        /// <summary>
        /// 向缓冲区写入一条数据
        /// 当缓冲区满时，覆盖最旧的数据
        /// </summary>
        /// <param name="item">数据条目</param>
        public void Write(T item)
        {
            lock (_lock)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % Capacity;
                if (_count < Capacity) _count++;
            }
        }

        /// <summary>
        /// 读取缓冲区中所有数据（按时间顺序，从旧到新）
        /// </summary>
        /// <returns>按时间顺序排列的数据集合</returns>
        public List<T> ReadAll()
        {
            lock (_lock)
            {
                var result = new List<T>(_count);
                if (_count == 0) return result;

                int startIndex = _count < Capacity ? 0 : _head;
                for (int i = 0; i < _count; i++)
                {
                    int index = (startIndex + i) % Capacity;
                    result.Add(_buffer[index]);
                }
                return result;
            }
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _head = 0;
                _count = 0;
                Array.Clear(_buffer, 0, _buffer.Length);
            }
        }
    }
}
