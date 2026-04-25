// ============================================================
// 文件名: ICanCommunicationService.cs
// 用途: CAN通信服务接口，封装周立功CAN卡操作，支持真实与模拟CAN卡切换
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// CAN通信服务接口
    /// 提供统一的CAN总线通信能力，封装底层CAN卡硬件操作
    /// 支持真实CAN卡（周立功USBCAN系列）与模拟CAN卡两种模式
    /// </summary>
    public interface ICanCommunicationService
    {
        /// <summary>
        /// CAN消息接收事件，当收到新CAN消息时触发
        /// </summary>
        event Action<CanMessage> OnCanMessageReceived;

        /// <summary>
        /// CAN通信错误事件，当发生通信错误时触发
        /// </summary>
        event Action<string> OnCanError;

        /// <summary>
        /// 连接状态变化事件，当CAN连接状态发生变化时触发
        /// </summary>
        event Action<bool> OnConnectionStatusChanged;

        /// <summary>
        /// 启动CAN服务
        /// 根据系统配置自动选择真实CAN卡或模拟CAN卡
        /// 初始化CAN通道、开启接收线程并触发连接状态变更事件
        /// </summary>
        /// <returns>启动是否成功</returns>
        bool StartCanService();

        /// <summary>
        /// 停止CAN服务，关闭CAN通道、停止接收线程并释放硬件资源
        /// </summary>
        void StopCanService();

        /// <summary>
        /// 发送单条CAN消息
        /// </summary>
        /// <param name="canId">CAN消息ID（11位标准帧或29位扩展帧）</param>
        /// <param name="data">数据载荷（0-8字节 CAN 2.0, 0-64字节 CAN FD）</param>
        /// <param name="isExtended">是否为扩展帧（29位ID），默认false</param>
        /// <param name="isFd">是否为CAN FD帧，默认false</param>
        /// <returns>发送是否成功</returns>
        bool SendCanMessage(uint canId, byte[] data, bool isExtended = false, bool isFd = false);

        /// <summary>
        /// 批量发送CAN消息，用于固件升级等大数据量传输场景，按指定间隔依次发送
        /// </summary>
        /// <param name="messages">CAN消息列表</param>
        /// <param name="intervalMs">消息发送间隔（毫秒），默认1.0ms</param>
        /// <returns>成功发送的消息数量</returns>
        int SendBatchMessages(List<CanMessage> messages, double intervalMs = 1.0);

        /// <summary>
        /// 获取CAN通信统计信息，包含收发消息总数、错误计数、总线状态等
        /// </summary>
        /// <returns>CAN通信统计数据</returns>
        CanStatistics GetCanStatistics();

        /// <summary>
        /// 获取CAN服务是否正在运行
        /// </summary>
        bool IsRunning { get; }
    }
}
