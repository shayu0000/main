// ============================================================
// 文件名: IBatteryProtocolService.cs
// 用途: 电池协议管理服务接口，提供BMS电池管理系统协议的管理和通信功能
// ============================================================

using System;
using System.Collections.Generic;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 电池协议管理服务接口
    /// 负责BMS电池管理系统的协议管理：
    ///   1. 加载和配置不同供应商的BMS通信协议
    ///   2. 解析BMS上报的单体电压、温度、SOC等数据
    ///   3. 支持多帧数据拼装
    ///   4. 提供BMS连接状态监控
    /// </summary>
    public interface IBatteryProtocolService
    {
        /// <summary>
        /// 加载电池协议，根据BMS供应商和协议版本加载对应的电池通信协议
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="bmsVendor">BMS供应商名称</param>
        /// <param name="protocolVersion">协议版本号</param>
        /// <returns>加载是否成功</returns>
        bool LoadBatteryProtocol(string protocolName, string bmsVendor, string protocolVersion);

        /// <summary>
        /// 获取单体电芯数据，解析BMS上报的单体电压和温度数据
        /// </summary>
        /// <param name="cellIndices">要查询的单体索引列表，为null则返回所有单体数据</param>
        /// <returns>单体数据字典（Key: 单体索引, Value: 单体数据）</returns>
        Dictionary<int, CellData> GetCellData(List<int> cellIndices = null);

        /// <summary>
        /// 获取电池包整体数据，包含总电压、总电流、SOC、SOH、最高/最低单体等汇总信息
        /// </summary>
        /// <returns>电池包数据</returns>
        PackData GetPackData();

        /// <summary>
        /// 发送BMS命令，通过CAN总线向BMS发送控制命令（如均衡控制、参数设置等）
        /// </summary>
        /// <param name="commandName">命令名称</param>
        /// <param name="parameters">命令参数</param>
        /// <returns>发送是否成功</returns>
        bool SendBmsCommand(string commandName, Dictionary<string, object> parameters);

        /// <summary>
        /// 获取BMS连接状态，检查BMS通信是否正常、心跳是否超时等
        /// </summary>
        /// <returns>BMS连接状态信息</returns>
        BmsConnectionStatus GetBmsConnectionStatus();
    }

    /// <summary>
    /// 单体电芯数据类，存储单个电芯的测量参数
    /// </summary>
    public class CellData
    {
        /// <summary>
        /// 获取或设置单体索引（从0开始）
        /// </summary>
        public int CellIndex { get; set; }

        /// <summary>
        /// 获取或设置单体电压(V)
        /// </summary>
        public double Voltage { get; set; }

        /// <summary>
        /// 获取或设置单体温度(°C)
        /// </summary>
        public double Temperature { get; set; }

        /// <summary>
        /// 获取或设置均衡状态(1=均衡中, 0=未均衡)
        /// </summary>
        public int BalancingStatus { get; set; }

        /// <summary>
        /// 获取或设置数据质量: GOOD/UNCERTAIN/BAD，默认为 GOOD
        /// </summary>
        public string Quality { get; set; } = "GOOD";
    }

    /// <summary>
    /// 电池包整体数据类，汇总电池包的所有关键参数
    /// </summary>
    public class PackData
    {
        /// <summary>
        /// 获取或设置电池包总电压(V)
        /// </summary>
        public double TotalVoltage { get; set; }

        /// <summary>
        /// 获取或设置电池包总电流(A)，正值充电/负值放电
        /// </summary>
        public double TotalCurrent { get; set; }

        /// <summary>
        /// 获取或设置荷电状态 SOC (%)
        /// </summary>
        public double Soc { get; set; }

        /// <summary>
        /// 获取或设置健康状态 SOH (%)
        /// </summary>
        public double Soh { get; set; }

        /// <summary>
        /// 获取或设置单体最高电压(V)
        /// </summary>
        public double CellMaxVoltage { get; set; }

        /// <summary>
        /// 获取或设置单体最低电压(V)
        /// </summary>
        public double CellMinVoltage { get; set; }

        /// <summary>
        /// 获取或设置单体平均电压(V)
        /// </summary>
        public double CellAvgVoltage { get; set; }

        /// <summary>
        /// 获取或设置单体最高温度(°C)
        /// </summary>
        public double CellMaxTemperature { get; set; }

        /// <summary>
        /// 获取或设置单体最低温度(°C)
        /// </summary>
        public double CellMinTemperature { get; set; }

        /// <summary>
        /// 获取或设置单体平均温度(°C)
        /// </summary>
        public double CellAvgTemperature { get; set; }

        /// <summary>
        /// 获取或设置绝缘电阻(kΩ)
        /// </summary>
        public double InsulationResistanceKOhm { get; set; }

        /// <summary>
        /// 获取或设置电池包总容量(Ah)
        /// </summary>
        public double TotalCapacityAh { get; set; }

        /// <summary>
        /// 获取或设置电池包总能量(kWh)
        /// </summary>
        public double TotalEnergyKwh { get; set; }

        /// <summary>
        /// 获取或设置单体总数
        /// </summary>
        public int CellCount { get; set; }

        /// <summary>
        /// 获取或设置温度传感器总数
        /// </summary>
        public int TemperatureSensorCount { get; set; }

        /// <summary>
        /// 获取或设置数据采集时间戳（Unix毫秒）
        /// </summary>
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// BMS连接状态类，描述BMS通信链路的健康状态
    /// </summary>
    public class BmsConnectionStatus
    {
        /// <summary>
        /// 获取或设置是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 获取或设置最后心跳时间（Unix毫秒时间戳）
        /// </summary>
        public long LastHeartbeatTime { get; set; }

        /// <summary>
        /// 获取或设置心跳超时阈值(毫秒)，默认 5000ms
        /// </summary>
        public int HeartbeatTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// 获取或设置通信错误计数
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// 获取或设置通信质量: GOOD/FAIR/POOR，默认为 GOOD
        /// </summary>
        public string CommunicationQuality { get; set; } = "GOOD";

        /// <summary>
        /// 获取或设置连接状态描述
        /// </summary>
        public string StatusMessage { get; set; }
    }
}
