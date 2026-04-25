// ============================================================
// 文件名: FaultEvent.cs
// 用途: 故障事件和故障录波实体类，对应 fault_event 和 fault_waveform 表
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Core.Models
{
    /// <summary>
    /// 故障事件实体类，存储设备故障事件的详细信息和录波数据引用
    /// </summary>
    public class FaultEvent
    {
        /// <summary>
        /// 获取或设置故障事件 ID，主键，UUID 格式
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// 获取或设置设备 ID，外键关联 device_info
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 获取或设置关联的充放电会话 ID，可为空
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// 获取或设置故障码
        /// </summary>
        public string FaultCode { get; set; }

        /// <summary>
        /// 获取或设置故障等级: CRITICAL(严重)/MAJOR(主要)/MINOR(次要)
        /// </summary>
        public string FaultLevel { get; set; }

        /// <summary>
        /// 获取或设置故障描述信息
        /// </summary>
        public string FaultDescription { get; set; }

        /// <summary>
        /// 获取或设置故障触发时间，Unix 毫秒时间戳
        /// </summary>
        public long TriggeredAt { get; set; }

        /// <summary>
        /// 获取或设置触发录波的通道名称
        /// </summary>
        public string TriggerChannel { get; set; }

        /// <summary>
        /// 获取或设置触发值，触发时刻的瞬时测量值
        /// </summary>
        public double? TriggerValue { get; set; }

        /// <summary>
        /// 获取或设置故障前录波样点数
        /// </summary>
        public int PreFaultSamples { get; set; }

        /// <summary>
        /// 获取或设置故障后录波样点数
        /// </summary>
        public int PostFaultSamples { get; set; }

        /// <summary>
        /// 获取或设置录波采样率，单位 Hz
        /// </summary>
        public double SampleRateHz { get; set; }

        /// <summary>
        /// 获取或设置波形数据文件路径，大数据 Blob 存为文件
        /// </summary>
        public string WaveformDataPath { get; set; }

        /// <summary>
        /// 获取或设置是否已导出: 1=已导出, 0=未导出
        /// </summary>
        public int IsExported { get; set; }

        /// <summary>
        /// 获取或设置分析人用户 ID
        /// </summary>
        public string AnalyzedBy { get; set; }

        /// <summary>
        /// 获取或设置分析备注或结论
        /// </summary>
        public string AnalysisNotes { get; set; }

        /// <summary>
        /// 获取或设置录波波形数据集合，导航属性
        /// </summary>
        public List<FaultWaveform> Waveforms { get; set; } = new List<FaultWaveform>();
    }

    /// <summary>
    /// 故障录波波形数据实体类，存储单通道的录波波形数据
    /// </summary>
    public class FaultWaveform
    {
        /// <summary>
        /// 获取或设置波形数据 ID，主键
        /// </summary>
        public string WaveformId { get; set; }

        /// <summary>
        /// 获取或设置故障事件 ID，外键关联 fault_event
        /// </summary>
        public string EventId { get; set; }

        /// <summary>
        /// 获取或设置通道索引序号
        /// </summary>
        public int ChannelIndex { get; set; }

        /// <summary>
        /// 获取或设置通道名称，如 dc_voltage、dc_current 等
        /// </summary>
        public string ChannelName { get; set; }

        /// <summary>
        /// 获取或设置波形数据 Blob，压缩存储的采样数据
        /// </summary>
        public byte[] DataBlob { get; set; }

        /// <summary>
        /// 获取或设置波形数据点数量
        /// </summary>
        public int DataSize { get; set; }

        /// <summary>
        /// 获取或设置测量单位
        /// </summary>
        public string Unit { get; set; }
    }
}
