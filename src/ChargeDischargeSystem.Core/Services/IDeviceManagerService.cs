// ============================================================
// 文件名: IDeviceManagerService.cs
// 用途: 设备管理服务接口，提供设备的注册、查询、修改、删除和参数配置功能
// ============================================================

using System;
using System.Collections.Generic;
using ChargeDischargeSystem.Core.Models;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 设备管理服务接口
    /// 管理充放电系统中所有设备的全生命周期：
    ///   注册新设备、查询设备列表与状态、更新设备信息、
    ///   设置设备运行参数、删除设备等操作
    /// </summary>
    public interface IDeviceManagerService
    {
        /// <summary>
        /// 注册新设备到系统，验证设备信息合法性后将设备信息持久化到数据库
        /// </summary>
        /// <param name="device">设备信息实体</param>
        /// <returns>注册是否成功</returns>
        bool RegisterDevice(DeviceInfo device);

        /// <summary>
        /// 获取设备列表（支持类型和状态过滤）
        /// </summary>
        /// <param name="filterType">设备类型过滤（PCS/BMS/Inverter/Meter/Charger），为null则不过滤</param>
        /// <param name="filterStatus">设备状态过滤（Online/Offline/Fault/Maintenance），为null则不过滤</param>
        /// <returns>满足过滤条件的设备信息列表</returns>
        List<DeviceInfo> GetDeviceList(string filterType = null, string filterStatus = null);

        /// <summary>
        /// 根据设备ID获取设备完整信息
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备信息，不存在则返回null</returns>
        DeviceInfo GetDeviceById(string deviceId);

        /// <summary>
        /// 获取设备的当前运行状态，包含在线状态、最后通信时间、当前参数值等
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备状态信息，不存在则返回null</returns>
        DeviceStatusRecord GetDeviceStatus(string deviceId);

        /// <summary>
        /// 更新设备信息，修改设备的基本属性（名称、型号等），设备ID不可变更
        /// </summary>
        /// <param name="device">设备信息实体（DeviceId必须与现有设备匹配）</param>
        /// <returns>更新是否成功</returns>
        bool UpdateDevice(DeviceInfo device);

        /// <summary>
        /// 设置设备运行参数，通过CAN总线向设备发送参数配置命令
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <param name="paramName">参数名称（如 charge_power_limit / discharge_power_limit）</param>
        /// <param name="paramValue">参数值</param>
        /// <returns>设置是否成功</returns>
        bool SetDeviceParameter(string deviceId, string paramName, object paramValue);

        /// <summary>
        /// 删除设备，从系统中移除设备记录（如有活跃会话则拒绝删除）
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>删除是否成功</returns>
        bool DeleteDevice(string deviceId);

        /// <summary>
        /// 获取设备的完整配置信息，包含通信地址、协议配置、校准记录等
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>设备配置信息JSON字符串，不存在则返回null</returns>
        string GetDeviceConfig(string deviceId);
    }
}
