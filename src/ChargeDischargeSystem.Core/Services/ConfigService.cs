using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Common.Constants;

// ============================================================
// 命名空间: ChargeDischargeSystem.Core.Services
// 功能描述: 配置管理服务实现类
// 说明: 实现应用中所有YAML/JSON配置文件的管理功能
//       提供类型安全的配置读写、热重载和导入导出
// ============================================================
namespace ChargeDischargeSystem.Core.Services
{
    /// <summary>
    /// 配置管理服务实现类
    /// 统一管理系统中所有配置文件的操作：
    ///   1. 配置加载：从 config/ 目录加载所有YAML和JSON配置
    ///   2. 类型安全访问：GetConfigValue&lt;T&gt;(section, key) 按路径读写配置
    ///   3. 热重载：ReloadConfig() 无需重启即可刷新配置
    ///   4. 配置导出：ExportConfig() 将配置导出为JSON供备份或迁移
    ///   5. 配置导入：ImportConfig() 从JSON文件批量导入配置
    /// 
    /// 配置存储结构：
    ///   内存中使用嵌套字典存储配置树：
    ///     _configTree["section"]["key"] = value
    ///   例如：
    ///     _configTree["can_config"]["bitrate"] = 500000
    ///     _configTree["data_logging"]["sample_interval_ms"] = 1000
    /// </summary>
    public class ConfigService : IConfigService
    {
        #region -- 字段定义 --

        /// <summary>应用程序配置实例（映射到 appsettings.json）</summary>
        private readonly AppConfig _appConfig;

        /// <summary>配置文件目录</summary>
        private readonly string _configDir;

        /// <summary>配置数据树（Key: section, Value: 配置项字典）</summary>
        private readonly Dictionary<string, Dictionary<string, object>> _configTree =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>配置树读写锁</summary>
        private readonly object _configLock = new object();

        #endregion

        /// <summary>
        /// 构造配置管理服务实例
        /// </summary>
        /// <param name="appConfig">应用程序配置实例</param>
        public ConfigService(AppConfig appConfig)
        {
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _configDir = appConfig.ConfigDirectory ?? SystemConstants.DefaultConfigDir;
        }

        #region -- 配置加载 --

        /// <summary>
        /// 加载所有配置文件
        /// 从配置目录加载所有YAML/JSON文件到内存配置树
        /// </summary>
        /// <returns>加载是否成功</returns>
        public bool LoadAllConfigurations()
        {
            try
            {
                lock (_configLock)
                {
                    _configTree.Clear();

                    // ---- 加载主配置文件 (appsettings.json) ----
                    LoadJsonConfig(Path.Combine(_configDir, "appsettings.json"));

                    // ---- 加载所有YAML配置文件 ----
                    if (Directory.Exists(_configDir))
                    {
                        foreach (var file in Directory.GetFiles(_configDir, "*.yaml"))
                        {
                            LoadYamlConfig(file);
                        }
                        foreach (var file in Directory.GetFiles(_configDir, "*.yml"))
                        {
                            LoadYamlConfig(file);
                        }
                    }

                    // ---- 从AppConfig实例同步配置 ----
                    SyncFromAppConfig();
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置加载完成，共 {_configTree.Count} 个配置段");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置加载失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重新加载配置（热更新）
        /// </summary>
        /// <returns>重载是否成功</returns>
        public bool ReloadConfig()
        {
            System.Diagnostics.Debug.WriteLine("[ConfigService] 配置重载中...");
            return LoadAllConfigurations();
        }

        #endregion

        #region -- 配置读写 --

        /// <summary>
        /// 获取指定路径的配置值（类型安全）
        /// 从内存配置树中按 section.key 路径查找值
        /// </summary>
        /// <typeparam name="T">期望的配置值类型</typeparam>
        /// <param name="section">配置段名称</param>
        /// <param name="key">配置键名称</param>
        /// <returns>配置值</returns>
        public T GetConfigValue<T>(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
                return default;

            lock (_configLock)
            {
                if (_configTree.TryGetValue(section, out var keys)
                    && keys.TryGetValue(key, out var value))
                {
                    try
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch
                    {
                        return default;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置未找到: {section}.{key}");
            return default;
        }

        /// <summary>
        /// 设置指定路径的配置值（仅修改内存）
        /// </summary>
        /// <param name="section">配置段名称</param>
        /// <param name="key">配置键名称</param>
        /// <param name="value">新配置值</param>
        public void SetConfigValue(string section, string key, object value)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key))
                return;

            lock (_configLock)
            {
                if (!_configTree.ContainsKey(section))
                    _configTree[section] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                _configTree[section][key] = value;
            }

            System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置已更新: {section}.{key} = {value}");
        }

        /// <summary>
        /// 保存所有配置到文件
        /// 将内存中的配置变更持久化到配置文件
        /// </summary>
        /// <returns>保存是否成功</returns>
        public bool SaveConfig()
        {
            try
            {
                lock (_configLock)
                {
                    // 保存主配置为JSON
                    string jsonPath = Path.Combine(_configDir, "appsettings.json");
                    string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(_configTree, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(jsonPath, jsonContent, Encoding.UTF8);

                    // 同时更新AppConfig实例
                    SyncToAppConfig();
                }

                System.Diagnostics.Debug.WriteLine("[ConfigService] 配置已保存");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置保存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出配置到指定文件
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <returns>导出是否成功</returns>
        public bool ExportConfig(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                lock (_configLock)
                {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(_configTree, Newtonsoft.Json.Formatting.Indented);
                    string dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(filePath, json, Encoding.UTF8);
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置已导出: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置导出失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从文件导入配置
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <returns>导入是否成功</returns>
        public bool ImportConfig(string filePath)
        {
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] 导入文件不存在: {filePath}");
                return false;
            }

            try
            {
                string jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
                var imported = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(jsonContent);

                if (imported == null) return false;

                lock (_configLock)
                {
                    _configTree.Clear();
                    foreach (var section in imported)
                    {
                        _configTree[section.Key] = section.Value;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置已导入: {filePath}, 配置段数={imported.Count}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] 配置导入失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region -- 内部方法 --

        /// <summary>
        /// 从JSON配置文件加载配置到内存树
        /// </summary>
        private void LoadJsonConfig(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                string jsonContent = File.ReadAllText(filePath, Encoding.UTF8);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonContent);

                if (config == null) return;

                string sectionName = Path.GetFileNameWithoutExtension(filePath);
                if (!_configTree.ContainsKey(sectionName))
                    _configTree[sectionName] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in config)
                {
                    _configTree[sectionName][kvp.Key] = kvp.Value;
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigService] JSON配置加载: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] JSON配置加载失败: {filePath}, {ex.Message}");
            }
        }

        /// <summary>
        /// 从YAML配置文件加载配置到内存树
        /// </summary>
        private void LoadYamlConfig(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                var yamlConfig = ConfigManager.LoadYamlConfig<Dictionary<string, object>>(filePath);
                if (yamlConfig == null) return;

                string sectionName = Path.GetFileNameWithoutExtension(filePath);
                if (!_configTree.ContainsKey(sectionName))
                    _configTree[sectionName] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in yamlConfig)
                {
                    _configTree[sectionName][kvp.Key] = kvp.Value;
                }

                System.Diagnostics.Debug.WriteLine($"[ConfigService] YAML配置加载: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] YAML配置加载失败: {filePath}, {ex.Message}");
            }
        }

        /// <summary>
        /// 从AppConfig实例同步配置到内存树
        /// </summary>
        private void SyncFromAppConfig()
        {
            // 同步CAN配置
            SetConfigValue("can_config", "device_index", _appConfig.CanConfig?.DeviceIndex ?? 0);
            SetConfigValue("can_config", "channel_index", _appConfig.CanConfig?.ChannelIndex ?? 0);
            SetConfigValue("can_config", "bitrate", _appConfig.CanConfig?.Bitrate ?? 500000);
            SetConfigValue("can_config", "use_mock", _appConfig.CanConfig?.UseMock ?? true);
            SetConfigValue("can_config", "poll_interval_ms", _appConfig.CanConfig?.PollIntervalMs ?? 100);

            // 同步数据记录配置
            SetConfigValue("data_logging", "sample_interval_ms", _appConfig.DataLogging?.DefaultSampleIntervalMs ?? 1000);
            SetConfigValue("data_logging", "buffer_max_size", _appConfig.DataLogging?.BufferMaxSize ?? 1000);
            SetConfigValue("data_logging", "flush_interval_ms", _appConfig.DataLogging?.FlushIntervalMs ?? 5000);
            SetConfigValue("data_logging", "auto_backup_interval_hours", _appConfig.DataLogging?.AutoBackupIntervalHours ?? 24);

            // 同步性能配置
            SetConfigValue("performance", "worker_threads", _appConfig.Performance?.WorkerThreads ?? 4);
            SetConfigValue("performance", "ui_refresh_interval_ms", _appConfig.Performance?.UiRefreshIntervalMs ?? 500);

            // 同步报告配置
            SetConfigValue("report", "output_path", _appConfig.ReportConfig?.OutputPath ?? "Data/reports");
            SetConfigValue("report", "default_format", _appConfig.ReportConfig?.DefaultFormat ?? "html");

            // 同步公司信息
            SetConfigValue("company", "name", _appConfig.Company?.Name ?? "公司名称");
            SetConfigValue("company", "logo_path", _appConfig.Company?.LogoPath ?? "Resources/Images/logo.png");
        }

        /// <summary>
        /// 将内存配置树同步回AppConfig实例
        /// </summary>
        private void SyncToAppConfig()
        {
            if (_appConfig.CanConfig != null)
            {
                _appConfig.CanConfig.DeviceIndex = GetConfigValue<int>("can_config", "device_index");
                _appConfig.CanConfig.ChannelIndex = GetConfigValue<int>("can_config", "channel_index");
                _appConfig.CanConfig.Bitrate = GetConfigValue<int>("can_config", "bitrate");
                _appConfig.CanConfig.UseMock = GetConfigValue<bool>("can_config", "use_mock");
                _appConfig.CanConfig.PollIntervalMs = GetConfigValue<int>("can_config", "poll_interval_ms");
            }

            if (_appConfig.DataLogging != null)
            {
                _appConfig.DataLogging.DefaultSampleIntervalMs = GetConfigValue<int>("data_logging", "sample_interval_ms");
                _appConfig.DataLogging.BufferMaxSize = GetConfigValue<int>("data_logging", "buffer_max_size");
                _appConfig.DataLogging.FlushIntervalMs = GetConfigValue<int>("data_logging", "flush_interval_ms");
                _appConfig.DataLogging.AutoBackupIntervalHours = GetConfigValue<int>("data_logging", "auto_backup_interval_hours");
            }
        }

        #endregion
    }
}
