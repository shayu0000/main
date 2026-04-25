using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChargeDischargeSystem.Common.Constants;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 固件升级服务实现类
// 说明: 实现设备固件的远程升级全流程管理
//       使用CAN总线传输固件数据，支持CRC16校验和升级状态机
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 固件升级服务实现类
    /// 通过CAN总线对设备进行远程固件升级（FOTA）：
    /// 
    /// 升级状态机流程：
    ///   IDLE → CHECK_VERSION → ENTER_BOOTLOADER → ERASE_FLASH
    ///      → TRANSFER_DATA → VERIFY_FW → REBOOT_DEVICE → COMPLETED
    /// 
    /// CAN固件升级协议命令码：
    ///   ENTER_BOOTLOADER(0x10): 进入Bootloader模式
    ///   ERASE_FLASH(0x11):      擦除Flash存储区
    ///   TRANSFER_START(0x12):   开始数据传输
    ///   TRANSFER_DATA(0x13):    传输数据块
    ///   TRANSFER_END(0x14):     数据传输结束
    ///   VERIFY_FIRMWARE(0x15):  校验固件完整性
    ///   REBOOT_DEVICE(0x16):    重启设备
    ///   GET_VERSION(0x17):      获取固件版本
    /// </summary>
    public class FirmwareService : IFirmwareService, IDisposable
    {
        #region -- 常量定义：CAN固件升级协议命令码 --

        private const byte CMD_ENTER_BOOTLOADER = 0x10;
        private const byte CMD_ERASE_FLASH = 0x11;
        private const byte CMD_TRANSFER_START = 0x12;
        private const byte CMD_TRANSFER_DATA = 0x13;
        private const byte CMD_TRANSFER_END = 0x14;
        private const byte CMD_VERIFY_FIRMWARE = 0x15;
        private const byte CMD_REBOOT_DEVICE = 0x16;
        private const byte CMD_GET_VERSION = 0x17;

        /// <summary>固件升级命令基地址 (CAN ID)</summary>
        private const uint FIRMWARE_CMD_CAN_ID_BASE = 0x100;

        /// <summary>CAN 2.0B 每帧数据块大小（6字节有效载荷）</summary>
        private const int CAN20_BLOCK_SIZE = CanConstants.Can20BlockSize;

        /// <summary>CAN FD 每帧数据块大小（62字节有效载荷）</summary>
        private const int CANFD_BLOCK_SIZE = CanConstants.CanFdBlockSize;

        #endregion

        #region -- 字段定义 --

        /// <summary>CAN通信服务引用</summary>
        private readonly ICanCommunicationService _canService;

        /// <summary>升级任务字典（Key: TaskId, Value: 进度信息）</summary>
        private readonly Dictionary<string, UpgradeProgress> _tasks = new Dictionary<string, UpgradeProgress>();

        /// <summary>任务锁</summary>
        private readonly object _taskLock = new object();

        /// <summary>取消令牌字典（Key: TaskId）</summary>
        private readonly Dictionary<string, CancellationTokenSource> _cancelTokens = new Dictionary<string, CancellationTokenSource>();

        /// <summary>对象销毁标志</summary>
        private bool _isDisposed;

        #endregion

        #region -- 事件声明 --

        /// <summary>升级进度更新事件</summary>
        public event Action<string, UpgradeProgress> OnUpgradeProgressUpdated;

        #endregion

        /// <summary>
        /// 构造固件升级服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务</param>
        public FirmwareService(ICanCommunicationService canService)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
        }

        #region -- 固件升级流程 --

        /// <summary>
        /// 启动固件升级
        /// 执行异步升级流程，通过事件通知进度
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="firmwareFile">固件文件路径</param>
        /// <param name="firmwareVersion">固件版本号</param>
        /// <param name="forceUpgrade">强制升级</param>
        /// <returns>升级任务ID</returns>
        public string StartUpgrade(string deviceId, string firmwareFile, string firmwareVersion, bool forceUpgrade = false)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (!File.Exists(firmwareFile))
                throw new FileNotFoundException($"固件文件不存在: {firmwareFile}");

            string taskId = CryptoHelper.GenerateUuid();
            var progress = new UpgradeProgress
            {
                TaskId = taskId,
                DeviceId = deviceId,
                CurrentState = "IDLE",
                ProgressPercent = 0,
                Message = "升级任务已创建"
            };

            lock (_taskLock) { _tasks[taskId] = progress; }

            var cts = new CancellationTokenSource();
            lock (_cancelTokens) { _cancelTokens[taskId] = cts; }

            // 异步执行升级流程
            Task.Run(() => ExecuteUpgradeFlow(taskId, deviceId, firmwareFile, firmwareVersion, forceUpgrade, cts.Token));

            return taskId;
        }

        /// <summary>
        /// 执行完整的固件升级流程（状态机驱动）
        /// </summary>
        private async Task ExecuteUpgradeFlow(string taskId, string deviceId, string firmwareFile,
            string firmwareVersion, bool forceUpgrade, CancellationToken token)
        {
            try
            {
                // ---- 状态1: CHECK_VERSION ----
                UpdateProgress(taskId, "CHECK_VERSION", 5, "正在检查固件版本...");
                if (!forceUpgrade)
                {
                    string currentVersion = GetFirmwareVersion(deviceId);
                    if (currentVersion == firmwareVersion)
                    {
                        UpdateProgress(taskId, "COMPLETED", 100, $"固件已是最新版本: {firmwareVersion}");
                        return;
                    }
                }
                if (token.IsCancellationRequested) return;

                // ---- 状态2: ENTER_BOOTLOADER ----
                UpdateProgress(taskId, "ENTER_BOOTLOADER", 10, "正在进入Bootloader模式...");
                if (!SendFirmwareCommand(deviceId, CMD_ENTER_BOOTLOADER))
                    throw new Exception("进入Bootloader模式失败");
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                // ---- 状态3: ERASE_FLASH ----
                UpdateProgress(taskId, "ERASE_FLASH", 15, "正在擦除Flash存储区...");
                if (!SendFirmwareCommand(deviceId, CMD_ERASE_FLASH))
                    throw new Exception("Flash擦除失败");
                await Task.Delay(3000, token); // Flash擦除需要较长时间
                if (token.IsCancellationRequested) return;

                // ---- 状态4: TRANSFER_DATA ----
                UpdateProgress(taskId, "TRANSFER_DATA", 20, "正在准备固件数据传输...");

                // 读取固件文件
                byte[] firmwareData = File.ReadAllBytes(firmwareFile);
                long totalBytes = firmwareData.Length;
                int blockSize = CAN20_BLOCK_SIZE;
                int totalBlocks = (int)Math.Ceiling((double)totalBytes / blockSize);

                // 发送传输开始命令
                if (!SendFirmwareCommand(deviceId, CMD_TRANSFER_START, BitConverter.GetBytes(totalBytes)))
                    throw new Exception("数据传输启动失败");

                // 分块传输固件数据
                for (int i = 0; i < totalBlocks; i++)
                {
                    if (token.IsCancellationRequested) return;

                    int offset = i * blockSize;
                    int currentBlockSize = Math.Min(blockSize, (int)(totalBytes - offset));

                    byte[] block = new byte[currentBlockSize + 2];
                    block[0] = (byte)(i & 0xFF);       // 块序号低字节
                    block[1] = (byte)((i >> 8) & 0xFF); // 块序号高字节
                    Array.Copy(firmwareData, offset, block, 2, currentBlockSize);

                    // 计算并附加CRC16校验
                    ushort crc16 = CalculateCrc16(firmwareData, offset, currentBlockSize);
                    byte[] crcBytes = BitConverter.GetBytes(crc16);

                    byte[] data = new byte[currentBlockSize + 4];
                    Array.Copy(block, 0, data, 0, block.Length);
                    Array.Copy(crcBytes, 0, data, currentBlockSize + 2, 2);

                    // 发送数据块
                    if (!SendFirmwareCommand(deviceId, CMD_TRANSFER_DATA, data))
                        throw new Exception($"数据块 {i + 1}/{totalBlocks} 传输失败");

                    double progressPercent = 20.0 + (70.0 * (i + 1) / totalBlocks);
                    UpdateProgress(taskId, "TRANSFER_DATA", progressPercent,
                        $"正在传输固件数据: {i + 1}/{totalBlocks} ({progressPercent:F1}%)");

                    await Task.Delay(5, token); // 5ms间隔
                }

                // 发送传输结束命令
                if (!SendFirmwareCommand(deviceId, CMD_TRANSFER_END))
                    throw new Exception("数据传输结束命令发送失败");
                if (token.IsCancellationRequested) return;

                // ---- 状态5: VERIFY_FW ----
                UpdateProgress(taskId, "VERIFY_FW", 90, "正在校验固件完整性...");
                if (!SendFirmwareCommand(deviceId, CMD_VERIFY_FIRMWARE))
                    throw new Exception("固件校验失败");
                await Task.Delay(1000, token);
                if (token.IsCancellationRequested) return;

                // ---- 状态6: REBOOT_DEVICE ----
                UpdateProgress(taskId, "REBOOT_DEVICE", 95, "正在重启设备...");
                if (!SendFirmwareCommand(deviceId, CMD_REBOOT_DEVICE))
                    throw new Exception("设备重启命令发送失败");
                await Task.Delay(2000, token);

                // ---- 状态7: COMPLETED ----
                UpdateProgress(taskId, "COMPLETED", 100, $"固件升级完成: {firmwareVersion}");
            }
            catch (Exception ex) when (!(ex is TaskCanceledException))
            {
                UpdateProgress(taskId, "FAILED", 0, $"升级失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取固件升级任务的当前状态
        /// </summary>
        /// <param name="taskId">升级任务ID</param>
        /// <returns>升级进度信息</returns>
        public UpgradeProgress GetUpgradeStatus(string taskId)
        {
            lock (_taskLock)
            {
                return _tasks.TryGetValue(taskId, out var progress) ? progress : null;
            }
        }

        /// <summary>
        /// 取消正在进行的固件升级
        /// </summary>
        /// <param name="taskId">升级任务ID</param>
        /// <returns>取消是否成功</returns>
        public bool CancelUpgrade(string taskId)
        {
            lock (_cancelTokens)
            {
                if (_cancelTokens.TryGetValue(taskId, out var cts))
                {
                    cts.Cancel();
                    _cancelTokens.Remove(taskId);
                    UpdateProgress(taskId, "CANCELLED", 0, "升级已取消");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取设备的当前固件版本
        /// 通过CAN发送GET_VERSION命令并解析响应
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>固件版本号</returns>
        public string GetFirmwareVersion(string deviceId)
        {
            try
            {
                // 发送获取版本命令
                SendFirmwareCommand(deviceId, CMD_GET_VERSION);
                // TODO: 等待设备响应并解析版本号
                return "1.0.0";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 回滚设备固件到上一个版本
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>回滚是否成功</returns>
        public bool RollbackFirmware(string deviceId)
        {
            // TODO: 从数据库查询上一个固件版本并执行回滚
            System.Diagnostics.Debug.WriteLine($"[FirmwareService] 固件回滚: {deviceId}");
            return false;
        }

        #endregion

        #region -- 辅助方法 --

        /// <summary>
        /// 发送固件升级协议命令
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="commandCode">命令码</param>
        /// <param name="data">附加数据（可选）</param>
        /// <returns>发送是否成功</returns>
        private bool SendFirmwareCommand(string deviceId, byte commandCode, byte[] data = null)
        {
            uint canId = FIRMWARE_CMD_CAN_ID_BASE + commandCode;

            byte[] payload = new byte[8];
            payload[0] = commandCode;

            if (data != null)
            {
                int copyLen = Math.Min(data.Length, 7);
                Array.Copy(data, 0, payload, 1, copyLen);
            }

            return _canService.SendCanMessage(canId, payload);
        }

        /// <summary>
        /// 更新升级进度并触发事件通知
        /// </summary>
        private void UpdateProgress(string taskId, string state, double percent, string message)
        {
            lock (_taskLock)
            {
                if (_tasks.TryGetValue(taskId, out var progress))
                {
                    progress.CurrentState = state;
                    progress.ProgressPercent = percent;
                    progress.Message = message;
                    progress.HasError = state == "FAILED";

                    if (state == "FAILED")
                        progress.ErrorMessage = message;

                    OnUpgradeProgressUpdated?.Invoke(taskId, progress);
                }
            }
        }

        /// <summary>
        /// 计算CRC16校验值（CCITT标准）
        /// 用于固件数据块传输后的完整性校验
        /// </summary>
        /// <param name="data">数据字节数组</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="length">数据长度</param>
        /// <returns>CRC16校验值</returns>
        private ushort CalculateCrc16(byte[] data, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= (ushort)(data[offset + i] << 8);
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
            return crc;
        }

        #endregion

        #region -- IDisposable 实现 --

        /// <summary>
        /// 释放固件升级服务占用的所有资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_cancelTokens)
            {
                foreach (var cts in _cancelTokens.Values)
                    cts.Cancel();
                _cancelTokens.Clear();
            }
        }

        #endregion
    }
}
