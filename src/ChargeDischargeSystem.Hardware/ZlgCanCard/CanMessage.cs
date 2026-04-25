using System;

// ============================================================
// 命名空间: ChargeDischargeSystem.Hardware.ZlgCanCard
// 功能描述: CAN消息数据结构定义
// 说明: 封装CAN总线消息的数据结构，兼容周立功CAN卡数据格式
// ============================================================
namespace ChargeDischargeSystem.Hardware.ZlgCanCard
{
    /// <summary>
    /// CAN总线消息数据结构
    /// 表示CAN总线上的一条报文，包含ID、数据、帧类型和时间戳
    /// </summary>
    public class CanMessage
    {
        /// <summary>CAN消息ID（11位标准帧或29位扩展帧）</summary>
        public uint CanId { get; set; }

        /// <summary>数据载荷（0-8字节 CAN 2.0, 0-64字节 CAN FD）</summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>数据长度</summary>
        public byte Length { get; set; }

        /// <summary>是否为扩展帧（29位ID）</summary>
        public bool IsExtended { get; set; }

        /// <summary>是否为远程帧</summary>
        public bool IsRemote { get; set; }

        /// <summary>是否为CAN FD帧</summary>
        public bool IsFd { get; set; }

        /// <summary>接收时间戳（Unix毫秒）</summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 转换为日志友好的十六进制字符串
        /// </summary>
        public override string ToString()
        {
            string idStr = IsExtended ? $"0x{CanId:X8}" : $"0x{CanId:X3}";
            string dataStr = Data.Length > 0 ? BitConverter.ToString(Data, 0, Data.Length).Replace("-", " ") : "空";
            return $"[{Timestamp}] ID:{idStr} 数据:[{dataStr}] 长度:{Length}";
        }

        /// <summary>
        /// 创建CAN消息的深拷贝
        /// </summary>
        public CanMessage Clone()
        {
            var clone = new CanMessage
            {
                CanId = CanId,
                Length = Length,
                IsExtended = IsExtended,
                IsRemote = IsRemote,
                IsFd = IsFd,
                Timestamp = Timestamp,
                Data = new byte[Data.Length]
            };
            Array.Copy(Data, clone.Data, Data.Length);
            return clone;
        }
    }

    /// <summary>
    /// CAN接口配置参数
    /// 用于初始化周立功CAN卡的工作参数
    /// </summary>
    public class CanConfig
    {
        /// <summary>CAN接口索引（0-based）</summary>
        public int DeviceIndex { get; set; } = 0;

        /// <summary>CAN通道号（0或1）</summary>
        public int ChannelIndex { get; set; } = 0;

        /// <summary>仲裁段波特率（bps），默认500kbps</summary>
        public int Bitrate { get; set; } = 500000;

        /// <summary>CAN FD数据段波特率（bps），默认2Mbps</summary>
        public int DataBitrate { get; set; } = 2000000;

        /// <summary>CAN工作模式：0=正常, 1=只听, 2=自收发(环回)</summary>
        public int WorkMode { get; set; } = 0;

        /// <summary>是否启用CAN FD模式</summary>
        public bool EnableCanFd { get; set; } = false;

        /// <summary>接收缓冲区大小</summary>
        public int RecvBufferSize { get; set; } = 1024;

        /// <summary>发送缓冲区大小</summary>
        public int SendBufferSize { get; set; } = 256;
    }

    /// <summary>
    /// CAN通信统计信息
    /// </summary>
    public class CanStatistics
    {
        /// <summary>发送消息总数</summary>
        public long TxCount { get; set; }

        /// <summary>接收消息总数</summary>
        public long RxCount { get; set; }

        /// <summary>发送错误计数</summary>
        public long TxErrorCount { get; set; }

        /// <summary>接收错误计数</summary>
        public long RxErrorCount { get; set; }

        /// <summary>总线状态</summary>
        public string BusStatus { get; set; } = "Unknown";

        /// <summary>最后消息接收时间</summary>
        public DateTime? LastReceiveTime { get; set; }

        /// <summary>总线错误次数累计</summary>
        public long BusErrorCount { get; set; }
    }
}
