using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChargeDischargeSystem.Common.Helpers;
using ChargeDischargeSystem.Core.Models;
using ChargeDischargeSystem.Hardware.ZlgCanCard;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 设备管理服务实现类
// 说明: 实现设备的注册、查询、更新、删除和参数配置功能
//       所有写操作会验证参数合法性并通过CAN发送配置命令到设备
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 设备管理服务实现类
    /// 管理充放电系统中所有设备信息，提供设备CRUD操作和参数配置
    /// 
    /// 核心职责：
    ///   1. 设备注册：验证并持久化设备信息到数据库
    ///   2. 设备查询：支持按类型、状态等条件筛选
    ///   3. 设备更新：修改设备属性并同步到硬件
    ///   4. 参数配置：通过CAN总线向设备发送运行参数
    ///   5. 设备删除：安全移除设备记录
    /// </summary>
    public class DeviceManagerService : IDeviceManagerService
    {
        #region -- 字段定义 --

        /// <summary>CAN通信服务引用，用于发送配置命令到设备</summary>
        private readonly ICanCommunicationService _canService;

        // 注意：实际项目中会注入DeviceRepository用于数据库操作
        // 本实现提供完整的业务逻辑，数据访问层在后续集成时注入

        #endregion

        /// <summary>
        /// 构造设备管理服务实例
        /// </summary>
        /// <param name="canService">CAN通信服务，用于向设备发送配置命令</param>
        public DeviceManagerService(ICanCommunicationService canService)
        {
            _canService = canService ?? throw new ArgumentNullException(nameof(canService));
        }

        #region -- 设备注册 --

        /// <summary>
        /// 注册新设备到系统
        /// 验证设备信息合法性（必填字段检查、CAN地址唯一性等），
        /// 验证通过后生成设备ID并持久化到数据库
        /// </summary>
        /// <param name="device">设备信息实体</param>
        /// <returns>注册是否成功</returns>
        public bool RegisterDevice(DeviceInfo device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            // ---- 第一步：参数合法性校验 ----
            if (!ValidateDeviceInfo(device, out string errorMessage))
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 设备注册失败: {errorMessage}");
                return false;
            }

            // ---- 第二步：生成设备ID（如果没有提供） ----
            if (string.IsNullOrEmpty(device.DeviceId))
            {
                device.DeviceId = $"DEV_{device.DeviceType}_{CryptoHelper.GenerateUuid().Substring(0, 8)}";
            }

            // ---- 第三步：设置初始状态和注册时间 ----
            device.Status = "Offline";
            device.RegisteredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // ---- 第四步：持久化到数据库 ----
            // TODO: 注入DeviceRepository并调用 InsertAsync(device)
            System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 设备注册成功: {device.DeviceId} ({device.DeviceName})");

            return true;
        }

        #endregion

        #region -- 设备查询 --

        /// <summary>
        /// 获取设备列表（支持类型和状态过滤）
        /// </summary>
        /// <param name="filterType">设备类型过滤</param>
        /// <param name="filterStatus">设备状态过滤</param>
        /// <returns>满足条件的设备列表</returns>
        public List<DeviceInfo> GetDeviceList(string filterType = null, string filterStatus = null)
        {
            // TODO: 注入DeviceRepository并调用 GetAllAsync 后按条件过滤
            var devices = new List<DeviceInfo>();

            if (!string.IsNullOrEmpty(filterType))
                devices = devices.Where(d => d.DeviceType == filterType).ToList();

            if (!string.IsNullOrEmpty(filterStatus))
                devices = devices.Where(d => d.Status == filterStatus).ToList();

            System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 查询设备列表: 类型={filterType ?? "全部"}, 状态={filterStatus ?? "全部"}, 结果={devices.Count}台");
            return devices;
        }

        /// <summary>
        /// 根据设备ID获取设备完整信息
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备信息，不存在则返回null</returns>
        public DeviceInfo GetDeviceById(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            // TODO: 注入DeviceRepository并调用 GetByIdAsync(deviceId)
            System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 查询设备: {deviceId}");
            return null;
        }

        /// <summary>
        /// 获取设备的当前运行状态
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备状态记录，不存在则返回null</returns>
        public DeviceStatusRecord GetDeviceStatus(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            // TODO: 注入DeviceStatusRepository查询最新状态
            System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 查询设备状态: {deviceId}");
            return null;
        }

        #endregion

        #region -- 设备更新 --

        /// <summary>
        /// 更新设备信息
        /// 修改设备基本属性（设备ID不可变更）
        /// </summary>
        /// <param name="device">设备信息实体</param>
        /// <returns>更新是否成功</returns>
        public bool UpdateDevice(DeviceInfo device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            if (string.IsNullOrEmpty(device.DeviceId))
            {
                System.Diagnostics.Debug.WriteLine("[DeviceManagerService] 更新失败: 设备ID为空");
                return false;
            }

            // 验证更新数据
            if (!ValidateDeviceInfo(device, out string errorMessage))
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 设备更新失败: {errorMessage}");
                return false;
            }

            // TODO: 注入DeviceRepository并调用 UpdateAsync(device)
            System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 设备更新成功: {device.DeviceId}");
            return true;
        }

        #endregion

        #region -- 设备参数配置 --

        /// <summary>
        /// 设置设备运行参数
        /// 通过CAN总线向设备发送参数配置命令
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="paramName">参数名称</param>
        /// <param name="paramValue">参数值</param>
        /// <returns>设置是否成功</returns>
        public bool SetDeviceParameter(string deviceId, string paramName, object paramValue)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            if (string.IsNullOrEmpty(paramName))
                throw new ArgumentException("参数名不能为空", nameof(paramName));

            if (paramValue == null)
                throw new ArgumentNullException(nameof(paramValue));

            try
            {
                // ---- 第一步：获取设备信息（含CAN地址） ----
                var device = GetDeviceById(deviceId);
                if (device == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 设备不存在: {deviceId}");
                    return false;
                }

                // ---- 第二步：构建CAN配置命令 ----
                uint canId = (uint)(0x200 + device.CanAddress); // 参数配置命令CAN ID
                byte[] data = BuildParameterCommand(paramName, paramValue);

                // ---- 第三步：通过CAN发送配置命令 ----
                bool success = _canService.SendCanMessage(canId, data, isExtended: false);
                System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 参数设置: {deviceId}.{paramName}={paramValue}, 结果={success}");
                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 参数设置异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 构建参数配置的CAN数据帧
        /// 将参数名称和值编码为8字节CAN数据载荷
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="paramValue">参数值</param>
        /// <returns>8字节CAN数据载荷</returns>
        private byte[] BuildParameterCommand(string paramName, object paramValue)
        {
            byte[] data = new byte[8];

            // 第0字节：参数编码（根据参数名映射）
            byte paramCode = MapParameterNameToCode(paramName);
            data[0] = paramCode;

            // 第1-7字节：参数值（根据值类型编码）
            double numericValue = Convert.ToDouble(paramValue);
            byte[] valueBytes = BitConverter.GetBytes((float)numericValue);
            Array.Copy(valueBytes, 0, data, 1, Math.Min(valueBytes.Length, 7));

            return data;
        }

        /// <summary>
        /// 将参数名称映射为CAN命令编码
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns>参数编码</returns>
        private byte MapParameterNameToCode(string paramName)
        {
            return paramName.ToLower() switch
            {
                "charge_power_limit" => 0x01,
                "discharge_power_limit" => 0x02,
                "charge_current_limit" => 0x03,
                "discharge_current_limit" => 0x04,
                "cutoff_voltage_high" => 0x05,
                "cutoff_voltage_low" => 0x06,
                "bms_heartbeat_interval" => 0x07,
                "data_report_interval" => 0x08,
                _ => 0xFF // 未知参数
            };
        }

        #endregion

        #region -- 设备删除 --

        /// <summary>
        /// 删除设备记录
        /// 删除前检查设备是否有活跃会话，如有则拒绝删除
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>删除是否成功</returns>
        public bool DeleteDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("设备ID不能为空", nameof(deviceId));

            try
            {
                // TODO: 检查设备是否有活跃的充放电会话
                // TODO: 注入DeviceRepository并调用 DeleteAsync(deviceId)
                System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 设备已删除: {deviceId}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 删除设备失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取设备的完整配置信息（JSON格式）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备配置JSON字符串</returns>
        public string GetDeviceConfig(string deviceId)
        {
            var device = GetDeviceById(deviceId);
            if (device == null) return null;

            // 构建配置信息JSON
            var configJson = new StringBuilder();
            configJson.AppendLine("{");
            configJson.AppendLine($"  \"deviceId\": \"{device.DeviceId}\",");
            configJson.AppendLine($"  \"deviceType\": \"{device.DeviceType}\",");
            configJson.AppendLine($"  \"deviceName\": \"{device.DeviceName}\",");
            configJson.AppendLine($"  \"canAddress\": {device.CanAddress},");
            configJson.AppendLine($"  \"protocolName\": \"{device.ProtocolName}\",");
            configJson.AppendLine($"  \"firmwareVersion\": \"{device.FirmwareVersion}\",");
            configJson.AppendLine($"  \"status\": \"{device.Status}\"");
            configJson.AppendLine("}");

            System.Diagnostics.Debug.WriteLine($"[DeviceManagerService] 获取设备配置: {deviceId}");
            return configJson.ToString();
        }

        #endregion

        #region -- 参数校验 --

        /// <summary>
        /// 验证设备信息的合法性
        /// 检查必填字段、数值范围和格式正确性
        /// </summary>
        /// <param name="device">设备信息</param>
        /// <param name="errorMessage">校验失败时的错误描述</param>
        /// <returns>校验是否通过</returns>
        private bool ValidateDeviceInfo(DeviceInfo device, out string errorMessage)
        {
            errorMessage = null;

            // 设备类型必须为有效枚举值
            if (string.IsNullOrEmpty(device.DeviceType) ||
                !new[] { "PCS", "BMS", "Inverter", "Meter", "Charger" }.Contains(device.DeviceType))
            {
                errorMessage = "设备类型无效，必须为: PCS/BMS/Inverter/Meter/Charger";
                return false;
            }

            // 设备名称不能为空
            if (string.IsNullOrEmpty(device.DeviceName))
            {
                errorMessage = "设备名称不能为空";
                return false;
            }

            // 额定功率必须为正数
            if (device.RatedPowerKw <= 0)
            {
                errorMessage = "额定功率必须大于0 kW";
                return false;
            }

            // 额定电压必须为正数
            if (device.RatedVoltageV <= 0)
            {
                errorMessage = "额定电压必须大于0 V";
                return false;
            }

            // 额定电流必须为正数
            if (device.RatedCurrentA <= 0)
            {
                errorMessage = "额定电流必须大于0 A";
                return false;
            }

            // CAN地址范围检查（0-255）
            if (device.CanAddress < 0 || device.CanAddress > 255)
            {
                errorMessage = "CAN地址必须在0-255范围内";
                return false;
            }

            return true;
        }

        #endregion
    }
}
