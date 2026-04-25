using System;
using System.Collections.Generic;

// ============================================================
// 命名空间: ChargeDischargeSystem.Hardware.ZlgCanCard
// 功能描述: CAN卡通信接口定义
// 说明: 定义CAN卡通信的核心接口，支持真实硬件和模拟测试两种实现
// ============================================================
namespace ChargeDischargeSystem.Hardware.ZlgCanCard
{
    /// <summary>
    /// CAN卡通信接口
    /// 定义与CAN硬件设备通信的标准接口，所有CAN卡实现必须遵循此接口
    /// </summary>
    public interface ICanCard : IDisposable
    {
        /// <summary>CAN卡打开成功事件</summary>
        event Action OnOpened;

        /// <summary>CAN卡关闭事件</summary>
        event Action OnClosed;

        /// <summary>接收到CAN消息时触发的事件</summary>
        event Action<CanMessage> OnMessageReceived;

        /// <summary>CAN通信错误时触发的事件</summary>
        event Action<string> OnError;

        /// <summary>
        /// 初始化CAN卡设备
        /// </summary>
        /// <param name="config">CAN配置参数</param>
        /// <returns>初始化是否成功</returns>
        bool Initialize(CanConfig config);

        /// <summary>
        /// 打开CAN通道，开始通信
        /// </summary>
        /// <returns>打开是否成功</returns>
        bool Open();

        /// <summary>
        /// 关闭CAN通道，停止通信
        /// </summary>
        void Close();

        /// <summary>
        /// 发送单条CAN消息
        /// </summary>
        /// <param name="message">要发送的CAN消息</param>
        /// <returns>发送是否成功</returns>
        bool SendMessage(CanMessage message);

        /// <summary>
        /// 批量发送CAN消息（用于固件升级等大数据量场景）
        /// </summary>
        /// <param name="messages">消息列表</param>
        /// <param name="intervalMs">消息间隔(毫秒)</param>
        /// <returns>成功发送数量</returns>
        int SendBatchMessages(IEnumerable<CanMessage> messages, double intervalMs = 1.0);

        /// <summary>
        /// 获取CAN通信统计信息
        /// </summary>
        CanStatistics GetStatistics();

        /// <summary>
        /// 判断CAN卡是否已打开
        /// </summary>
        bool IsOpen { get; }
    }
}
