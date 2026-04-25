// ============================================================
// 文件名: IConfigService.cs
// 用途: 配置管理服务接口，定义YAML/JSON配置文件加载、更新和导出功能
// ============================================================

using System;

namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 配置管理服务接口
    /// 负责系统中所有配置文件的统一管理：
    ///   1. 加载和重载所有YAML/JSON配置文件
    ///   2. 按路径（section.key）读写配置值
    ///   3. 配置文件导出和导入
    ///   4. 支持泛型类型安全的配置读取
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// 加载所有配置文件，从 config 目录加载所有YAML和JSON配置到内存
        /// </summary>
        /// <returns>加载是否成功</returns>
        bool LoadAllConfigurations();

        /// <summary>
        /// 重新加载配置，从配置文件重新读取，实现配置热更新
        /// </summary>
        /// <returns>重载是否成功</returns>
        bool ReloadConfig();

        /// <summary>
        /// 获取指定路径的配置值（类型安全）
        /// </summary>
        /// <typeparam name="T">配置值类型</typeparam>
        /// <param name="section">配置段名称（如 "can_config" / "data_logging"）</param>
        /// <param name="key">配置键名称（如 "bitrate" / "sample_interval_ms"）</param>
        /// <returns>配置值，不存在则返回default(T)</returns>
        T GetConfigValue<T>(string section, string key);

        /// <summary>
        /// 设置指定路径的配置值（仅修改内存，需调用SaveConfig持久化）
        /// </summary>
        /// <param name="section">配置段名称</param>
        /// <param name="key">配置键名称</param>
        /// <param name="value">新配置值</param>
        void SetConfigValue(string section, string key, object value);

        /// <summary>
        /// 保存所有配置到文件，将内存中的配置变更持久化到配置文件
        /// </summary>
        /// <returns>保存是否成功</returns>
        bool SaveConfig();

        /// <summary>
        /// 导出配置到指定文件，将当前所有配置导出为单个JSON文件
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <returns>导出是否成功</returns>
        bool ExportConfig(string filePath);

        /// <summary>
        /// 从文件导入配置，从JSON文件批量导入配置并覆盖当前配置
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <returns>导入是否成功</returns>
        bool ImportConfig(string filePath);
    }
}
