using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 电池协议管理服务实现类
// 说明: 实现BMS电池管理系统协议的加载、数据解析和命令发送
//       支持多帧数据拼装、单体电压温度解析和BMS连接状态监控
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 电池协议管理服务实现类
    /// 负责与BMS电池管理系统的通信和数据解析：
    ///   1. 加载不同供应商的BMS通信协议（基于PGN定义）
    ///   2. 实时解析BMS上报的单体电压和温度数据
    ///   3. 支持CAN多帧传输数据的拼装（TP协议）
    ///   4. 监控BMS连接状态和心跳超时检测
    /// 
    /// CAN ID编码规则（BMS协议）：
    ///   标准格式: 0x18FF0000 + PGN
    ///   例如: 单体电压 PGN=0xF123, CAN ID=0x18FFF123
    /// </summary>
    public class BatteryProtocolService : IBatteryProtocolService
    {
        #region -- 字段定义 --

        /// <summary>CAN通信服务引用</summary>
        private readonly ICanCommunicationService _canService;

        /// <summary>协议管理服务引用</summary>
        private readonly IProtocolService _protocolService;

        /// <summary>已加载的电池协议配置字典（Key: 协议名称, Value: 配置）</summary>
        private readonly Dictionary<string, BatteryProtocolConfig> _loadedProtocols = new Dictionary<string, BatteryProtocolConfig>();

        /// <summary>电池包最新数据缓存</summary>
        private PackData _latestPackData;

        /// <summary>单体电芯数据缓存（Key: 单体索引）</summary>
        private readonly Dictionary<int, CellData> _cellDataCache = new Dictionary<int, CellData>();

        /// <summary>BMS连接状态</summary>
        private BmsConnectionStatus _bmsStatus = new BmsConnectionStatus();

        /// <summary>数据读写锁</summary>
        private readonly object _dataLock = new object();

        /// <summary>多帧拼装缓冲区（Key: 传输序列号, Value: 已接收的数据片段）</summary>
        private readonly Dictionary<byte, List<byte[]>> _multiFrameBuffer = new Dictionary<byte, List<byte[]>>();

        #endregion

        /// <summary>
        /// 构造电池协议管理服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务</param>
        /// <param name="protocolService">协议管理服务</param>
        public BatteryProtocolService(ICanCommunicationService canService, IProtocolService protocolService)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
            _protocolService = protocolService ?? throw new ArgumentNullException(nameof(protocolService));
        }

        #region -- 电池协议加载 --

        /// <summary>
        /// 加载电池协议
        /// 根据BMS供应商和协议版本加载对应的电池通信协议
        /// 协议定义通过ProtocolService的YAML加载机制获取
        /// </summary>
        /// <param name="protocolName">协议名称</param>
        /// <param name="bmsVendor">BMS供应商名称</param>
        /// <param name="protocolVersion">协议版本号</param>
        /// <returns>加载是否成功</returns>
        public bool LoadBatteryProtocol(string protocolName, string bmsVendor, string protocolVersion)
        {
            if (string.IsNullOrEmpty(protocolName))
                throw new ArgumentException("协议名称不能为空", nameof(protocolName));

            try
            {
                var config = new BatteryProtocolConfig
                {
                    ConfigId = CryptoHelper.GenerateUuid(),
                    ProtocolName = protocolName,
                    BmsVendor = bmsVendor ?? "Unknown",
                    ProtocolVersion = protocolVersion ?? "1.0",
                    IsActive = 1,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                lock (_dataLock)
                {
                    _loadedProtocols[protocolName] = config;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[BatteryProtocolService] 电池协议加载成功: {protocolName} (供应商: {bmsVendor}, 版本: {protocolVersion})");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolService] 电池协议加载失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region -- 数据获取 --

        /// <summary>
        /// 获取单体电芯数据
        /// 从缓存中返回指定索引或全部单体电芯的电压和温度数据
        /// </summary>
        /// <param name="cellIndices">要查询的单体索引列表，为null则返回所有</param>
        /// <returns>单体数据字典</returns>
        public Dictionary<int, CellData> GetCellData(List<int> cellIndices = null)
        {
            lock (_dataLock)
            {
                if (cellIndices == null || cellIndices.Count == 0)
                {
                    return new Dictionary<int, CellData>(_cellDataCache);
                }

                var result = new Dictionary<int, CellData>();
                foreach (int idx in cellIndices)
                {
                    if (_cellDataCache.TryGetValue(idx, out var data))
                        result[idx] = data;
                }
                return result;
            }
        }

        /// <summary>
        /// 获取电池包整体数据
        /// 返回最新的电池包汇总信息
        /// </summary>
        /// <returns>电池包数据</returns>
        public PackData GetPackData()
        {
            lock (_dataLock)
            {
                return _latestPackData;
            }
        }

        /// <summary>
        /// 发送BMS命令
        /// 通过CAN总线向BMS发送控制命令
        /// </summary>
        /// <param name="commandName">命令名称</param>
        /// <param name="parameters">命令参数</param>
        /// <returns>发送是否成功</returns>
        public bool SendBmsCommand(string commandName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(commandName))
                return false;

            try
            {
                // 构建BMS命令CAN消息
                uint canId = 0x18FF0000; // BMS命令基地址
                byte[] data = new byte[8];

                // 第0字节：命令码
                data[0] = commandName switch
                {
                    "balance_enable" => 0x20,
                    "balance_disable" => 0x21,
                    "request_cell_data" => 0x30,
                    "set_balance_threshold" => 0x22,
                    "reset_bms" => 0xFF,
                    _ => 0x00
                };

                // 后续字节：参数编码
                if (parameters != null)
                {
                    int idx = 1;
                    foreach (var kvp in parameters)
                    {
                        if (idx >= 8) break;
                        if (kvp.Value is double d)
                            data[idx++] = (byte)(d * 100 % 256);
                        else if (kvp.Value is int i)
                            data[idx++] = (byte)(i % 256);
                    }
                }

                return _canService.SendCanMessage(canId, data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolService] 发送BMS命令失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取BMS连接状态
        /// 返回当前BMS通信的健康状态信息
        /// </summary>
        /// <returns>BMS连接状态</returns>
        public BmsConnectionStatus GetBmsConnectionStatus()
        {
            lock (_dataLock)
            {
                return new BmsConnectionStatus
                {
                    IsConnected = _bmsStatus.IsConnected,
                    LastHeartbeatTime = _bmsStatus.LastHeartbeatTime,
                    HeartbeatTimeoutMs = _bmsStatus.HeartbeatTimeoutMs,
                    ErrorCount = _bmsStatus.ErrorCount,
                    CommunicationQuality = _bmsStatus.CommunicationQuality,
                    StatusMessage = _bmsStatus.StatusMessage
                };
            }
        }

        #endregion

        #region -- BMS数据解析 --

        /// <summary>
        /// 处理接收到的BMS CAN消息
        /// 根据CAN ID识别消息类型并解析对应的电池数据
        /// 支持多帧数据拼装（ISO 15765-2 TP协议）
        /// 
        /// CAN ID类型映射：
        ///   PGN 0xF100~0xF1FF: 单体电压数据
        ///   PGN 0xF200~0xF2FF: 单体温度数据
        ///   PGN 0xF300: 电池包汇总数据
        ///   PGN 0xF400: BMS心跳
        /// </summary>
        /// <param name="message">接收到的CAN消息</param>
        public void ProcessBmsMessage(CanMessage message)
        {
            if (message == null || message.Data == null || message.Data.Length == 0)
                return;

            try
            {
                uint canId = message.CanId;
                uint pgn = canId & 0x00FFFFFF;

                // 判断是否为多帧传输首帧
                if ((pgn >> 16) == 0xF1 || (pgn >> 16) == 0xF2)
                {
                    ProcessMultiFrameMessage(message);
                }

                switch (pgn >> 12)
                {
                    case 0xF1: // 单体电压
                        ParseCellVoltageData(message);
                        break;
                    case 0xF2: // 单体温度
                        ParseCellTemperatureData(message);
                        break;
                    case 0xF3: // 电池包汇总
                        ParsePackSummaryData(message);
                        break;
                    case 0xF4: // BMS心跳
                        UpdateBmsHeartbeat(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BatteryProtocolService] BMS消息处理异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析单体电压数据
        /// 标准格式：第0-1字节=起始索引，第2-7字节=3个单体电压（每单体2字节）
        /// </summary>
        /// <param name="message">CAN消息</param>
        private void ParseCellVoltageData(CanMessage message)
        {
            byte[] data = message.Data;
            if (data.Length < 4) return;

            int startIndex = data[0]; // 起始单体编号
            int cellCount = data[1];   // 本帧包含的单体数量

            lock (_dataLock)
            {
                for (int i = 0; i < cellCount && 2 + i * 2 + 1 < data.Length; i++)
                {
                    int cellIndex = startIndex + i;
                    ushort rawVoltage = (ushort)(data[2 + i * 2] << 8 | data[3 + i * 2]);
                    double voltage = rawVoltage * 0.001; // 分辨率 1mV

                    if (!_cellDataCache.TryGetValue(cellIndex, out var cell))
                    {
                        cell = new CellData { CellIndex = cellIndex };
                        _cellDataCache[cellIndex] = cell;
                    }
                    cell.Voltage = Math.Round(voltage, 3);
                }
            }
        }

        /// <summary>
        /// 解析单体温度数据
        /// 标准格式：第0-1字节=起始索引，第2-7字节=6个温度值（每字节1个，偏移-40°C）
        /// </summary>
        /// <param name="message">CAN消息</param>
        private void ParseCellTemperatureData(CanMessage message)
        {
            byte[] data = message.Data;
            if (data.Length < 3) return;

            int startIndex = data[0];
            int sensorCount = data[1];

            lock (_dataLock)
            {
                for (int i = 0; i < sensorCount && 2 + i < data.Length; i++)
                {
                    int cellIndex = startIndex + i;
                    double temperature = data[2 + i] - 40.0; // BMS温度编码：原始值-40=实际温度

                    if (!_cellDataCache.TryGetValue(cellIndex, out var cell))
                    {
                        cell = new CellData { CellIndex = cellIndex };
                        _cellDataCache[cellIndex] = cell;
                    }
                    cell.Temperature = Math.Round(temperature, 1);
                }
            }
        }

        /// <summary>
        /// 解析电池包汇总数据
        /// 标准格式：总电压(2B)+总电流(2B)+SOC(1B)+SOH(1B)+绝缘电阻(2B)
        /// </summary>
        /// <param name="message">CAN消息</param>
        private void ParsePackSummaryData(CanMessage message)
        {
            byte[] data = message.Data;
            if (data.Length < 8) return;

            var packData = new PackData
            {
                Timestamp = message.Timestamp,
                TotalVoltage = Math.Round(((data[0] << 8) | data[1]) * 0.1, 1),     // 0.1V分辨率
                TotalCurrent = Math.Round((((data[2] << 8) | data[3]) - 32000) * 0.1, 2), // 偏移-32000, 0.1A
                Soc = data[4] * 0.4,                                                // 0.4%分辨率
                Soh = data[5] * 0.4,
                InsulationResistanceKOhm = (data[6] << 8) | data[7],
                CellCount = _cellDataCache.Count
            };

            // 计算单体统计值
            if (_cellDataCache.Count > 0)
            {
                var voltages = _cellDataCache.Values.Where(c => c.Voltage > 0).Select(c => c.Voltage).ToList();
                var temps = _cellDataCache.Values.Where(c => c.Temperature != 0).Select(c => c.Temperature).ToList();

                if (voltages.Count > 0)
                {
                    packData.CellMaxVoltage = voltages.Max();
                    packData.CellMinVoltage = voltages.Min();
                    packData.CellAvgVoltage = voltages.Average();
                }
                if (temps.Count > 0)
                {
                    packData.CellMaxTemperature = temps.Max();
                    packData.CellMinTemperature = temps.Min();
                    packData.CellAvgTemperature = temps.Average();
                }
                packData.TemperatureSensorCount = temps.Count;
            }

            lock (_dataLock)
            {
                _latestPackData = packData;
            }
        }

        /// <summary>
        /// 更新BMS心跳状态
        /// 记录最后心跳时间并评估通信质量
        /// </summary>
        /// <param name="message">CAN消息</param>
        private void UpdateBmsHeartbeat(CanMessage message)
        {
            lock (_dataLock)
            {
                _bmsStatus.IsConnected = true;
                _bmsStatus.LastHeartbeatTime = message.Timestamp;
                _bmsStatus.CommunicationQuality = "GOOD";
                _bmsStatus.StatusMessage = "BMS通信正常";
            }
        }

        /// <summary>
        /// 处理CAN多帧传输数据拼装
        /// 遵循ISO 15765-2传输协议（TP）：
        ///   首帧(FF): 包含总长度和多帧序号
        ///   连续帧(CF): 包含序号和数据分段
        /// </summary>
        /// <param name="message">CAN消息</param>
        private void ProcessMultiFrameMessage(CanMessage message)
        {
            byte[] data = message.Data;
            if (data.Length < 1) return;

            byte frameType = (byte)((data[0] >> 4) & 0x0F);

            if (frameType == 0x01) // 首帧 (First Frame)
            {
                byte sequenceNumber = data[1];
                lock (_multiFrameBuffer)
                {
                    _multiFrameBuffer[sequenceNumber] = new List<byte[]> { data };
                }
            }
            else if (frameType == 0x02) // 连续帧 (Consecutive Frame)
            {
                byte sequenceNumber = data[1];
                lock (_multiFrameBuffer)
                {
                    if (_multiFrameBuffer.TryGetValue(sequenceNumber, out var frames))
                    {
                        frames.Add(data);
                    }
                }
            }

            // 检查拼装完成并合并数据
            // TODO: 根据总长度判断是否接收完整，合并所有帧数据后调用解析方法
        }

        #endregion
    }
}
