// ============================================================
// 文件名: AppConfig.cs
// 用途: 应用程序配置管理类，提供统一的配置加载、保存和热更新机制
// ============================================================

using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ChargeDischargeSystem.Common.Config
{
    /// <summary>
    /// 应用程序全局配置类，映射到 config/appsettings.json 配置文件
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 获取或设置数据库路径，默认为 Data/mw_scada.db
        /// </summary>
        public string DatabasePath { get; set; } = "Data/mw_scada.db";

        /// <summary>
        /// 获取或设置配置文件目录路径，默认为 config
        /// </summary>
        public string ConfigDirectory { get; set; } = "config";

        /// <summary>
        /// 获取或设置日志级别: DEBUG/INFO/WARNING/ERROR/CRITICAL，默认为 INFO
        /// </summary>
        public string LogLevel { get; set; } = "INFO";

        /// <summary>
        /// 获取或设置日志文件路径，默认为 logs/mw_scada.log
        /// </summary>
        public string LogFile { get; set; } = "logs/mw_scada.log";

        /// <summary>
        /// 获取或设置日志文件最大大小(MB)，默认为 100MB
        /// </summary>
        public int MaxLogSizeMb { get; set; } = 100;

        /// <summary>
        /// 获取或设置 CAN 通信配置
        /// </summary>
        public CanConfigSection CanConfig { get; set; } = new CanConfigSection();

        /// <summary>
        /// 获取或设置系统性能配置
        /// </summary>
        public PerformanceConfig Performance { get; set; } = new PerformanceConfig();

        /// <summary>
        /// 获取或设置数据记录配置
        /// </summary>
        public DataLoggingConfig DataLogging { get; set; } = new DataLoggingConfig();

        /// <summary>
        /// 获取或设置报告配置
        /// </summary>
        public ReportConfigSection ReportConfig { get; set; } = new ReportConfigSection();

        /// <summary>
        /// 获取或设置公司信息配置
        /// </summary>
        public CompanyInfo Company { get; set; } = new CompanyInfo();
    }

    /// <summary>
    /// CAN 通信配置段，定义 CAN 总线通信相关的参数
    /// </summary>
    public class CanConfigSection
    {
        /// <summary>
        /// 获取或设置 CAN 设备索引，默认为 0
        /// </summary>
        public int DeviceIndex { get; set; } = 0;

        /// <summary>
        /// 获取或设置 CAN 通道号，默认为 0
        /// </summary>
        public int ChannelIndex { get; set; } = 0;

        /// <summary>
        /// 获取或设置 CAN 波特率(bps)，默认为 500000
        /// </summary>
        public int Bitrate { get; set; } = 500000;

        /// <summary>
        /// 获取或设置 CAN FD 数据段波特率，默认为 2000000
        /// </summary>
        public int DataBitrate { get; set; } = 2000000;

        /// <summary>
        /// 获取或设置是否使用模拟 CAN 卡(测试模式)，默认为 true
        /// </summary>
        public bool UseMock { get; set; } = true;

        /// <summary>
        /// 获取或设置是否启用 CAN FD，默认为 false
        /// </summary>
        public bool EnableCanFd { get; set; } = false;

        /// <summary>
        /// 获取或设置数据采集轮询间隔(毫秒)，默认为 100ms
        /// </summary>
        public int PollIntervalMs { get; set; } = 100;

        /// <summary>
        /// 获取或设置看门狗超时(秒)，0 表示禁用，默认为 30s
        /// </summary>
        public int WatchdogTimeoutS { get; set; } = 30;
    }

    /// <summary>
    /// 系统性能配置，定义系统运行性能参数
    /// </summary>
    public class PerformanceConfig
    {
        /// <summary>
        /// 获取或设置工作线程数，默认为 4
        /// </summary>
        public int WorkerThreads { get; set; } = 4;

        /// <summary>
        /// 获取或设置 UI 数据刷新间隔(毫秒)，默认为 500ms
        /// </summary>
        public int UiRefreshIntervalMs { get; set; } = 500;

        /// <summary>
        /// 获取或设置图表最大显示数据点数，默认为 3600
        /// </summary>
        public int MaxChartDataPoints { get; set; } = 3600;
    }

    /// <summary>
    /// 数据记录配置，定义采样和存储相关参数
    /// </summary>
    public class DataLoggingConfig
    {
        /// <summary>
        /// 获取或设置默认采样间隔(毫秒)，默认为 1000ms
        /// </summary>
        public int DefaultSampleIntervalMs { get; set; } = 1000;

        /// <summary>
        /// 获取或设置快速采样间隔(毫秒) - 故障时使用，默认为 100ms
        /// </summary>
        public int FastSampleIntervalMs { get; set; } = 100;

        /// <summary>
        /// 获取或设置缓冲区最大条目数，默认为 1000
        /// </summary>
        public int BufferMaxSize { get; set; } = 1000;

        /// <summary>
        /// 获取或设置缓冲区刷新间隔(毫秒)，默认为 5000ms
        /// </summary>
        public int FlushIntervalMs { get; set; } = 5000;

        /// <summary>
        /// 获取或设置高分辨率数据保留天数，默认为 30 天
        /// </summary>
        public int HighResRetentionDays { get; set; } = 30;

        /// <summary>
        /// 获取或设置自动备份间隔(小时)，默认为 24 小时
        /// </summary>
        public int AutoBackupIntervalHours { get; set; } = 24;
    }

    /// <summary>
    /// 报告配置，定义报告输出相关参数
    /// </summary>
    public class ReportConfigSection
    {
        /// <summary>
        /// 获取或设置报告输出路径，默认为 Data/reports
        /// </summary>
        public string OutputPath { get; set; } = "Data/reports";

        /// <summary>
        /// 获取或设置默认报告格式: pdf/html/csv，默认为 html
        /// </summary>
        public string DefaultFormat { get; set; } = "html";
    }

    /// <summary>
    /// 公司信息配置，定义报告中显示的公司信息
    /// </summary>
    public class CompanyInfo
    {
        /// <summary>
        /// 获取或设置公司名称，默认为占位符
        /// </summary>
        public string Name { get; set; } = "公司名称";

        /// <summary>
        /// 获取或设置公司 Logo 路径，默认为 Resources/Images/logo.png
        /// </summary>
        public string LogoPath { get; set; } = "Resources/Images/logo.png";
    }

    /// <summary>
    /// 配置管理器静态类，负责加载和管理应用程序的各类配置文件
    /// </summary>
    public static class ConfigManager
    {
        // 应用程序配置实例缓存
        private static AppConfig _appConfig;

        // YAML 反序列化器实例，使用下划线命名约定
        private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        // JSON 反序列化器实例，使用驼峰命名约定
        private static readonly IDeserializer _jsonDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// 获取应用程序配置单例实例
        /// </summary>
        public static AppConfig Instance
        {
            get
            {
                // 检查配置是否已加载
                if (_appConfig == null)
                {
                    // 配置未加载时执行加载
                    LoadConfig();
                }

                // 返回已加载的配置实例
                return _appConfig;
            }
        }

        /// <summary>
        /// 从文件加载配置，首先尝试加载 JSON 配置，找不到则使用默认值
        /// </summary>
        /// <param name="configPath">配置文件路径，为空时使用默认路径</param>
        public static void LoadConfig(string configPath = null)
        {
            // 创建默认配置实例
            _appConfig = new AppConfig();

            // 检查配置文件路径是否为空
            if (string.IsNullOrEmpty(configPath))
            {
                // 使用默认路径: 程序运行目录下的 config/appsettings.json
                configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "appsettings.json");
            }

            // 检查配置文件是否存在
            if (File.Exists(configPath))
            {
                try
                {
                    // 读取配置文件全部内容
                    string jsonContent = File.ReadAllText(configPath);

                    // 反序列化 JSON 为配置对象
                    _appConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(jsonContent);

                    // 输出配置加载成功日志
                    Console.WriteLine($"[ConfigManager] 配置加载成功: {configPath}");
                }
                catch (Exception ex)
                {
                    // 配置加载失败时使用默认配置
                    Console.WriteLine($"[ConfigManager] 配置加载失败，使用默认配置: {ex.Message}");

                    // 重置为默认配置
                    _appConfig = new AppConfig();
                }
            }
            else
            {
                // 配置文件不存在时使用默认配置
                Console.WriteLine("[ConfigManager] 配置文件不存在，使用默认配置");
            }
        }

        /// <summary>
        /// 从 YAML 文件加载指定类型的配置，用于加载设备定义、电池协议等 YAML 格式配置
        /// </summary>
        /// <typeparam name="T">配置对象类型</typeparam>
        /// <param name="configPath">配置文件路径</param>
        /// <returns>配置对象，加载失败时返回默认实例</returns>
        public static T LoadYamlConfig<T>(string configPath) where T : new()
        {
            // 检查 YAML 配置文件是否存在
            if (!File.Exists(configPath))
            {
                // 文件不存在时输出警告并返回默认实例
                Console.WriteLine($"[ConfigManager] YAML配置文件不存在: {configPath}");

                // 返回默认配置实例
                return new T();
            }

            try
            {
                // 读取 YAML 文件全部内容
                string yamlContent = File.ReadAllText(configPath);

                // 反序列化 YAML 为指定类型对象
                return _yamlDeserializer.Deserialize<T>(yamlContent);
            }
            catch (Exception ex)
            {
                // YAML 解析失败时输出错误日志并返回默认实例
                Console.WriteLine($"[ConfigManager] YAML配置加载失败: {ex.Message}");

                // 返回默认配置实例
                return new T();
            }
        }

        /// <summary>
        /// 保存配置到文件，根据扩展名自动选择 JSON 或 YAML 格式
        /// </summary>
        /// <typeparam name="T">配置对象类型</typeparam>
        /// <param name="config">配置对象</param>
        /// <param name="configPath">保存路径</param>
        public static void SaveConfig<T>(T config, string configPath)
        {
            try
            {
                // 获取配置文件所在目录
                string dir = Path.GetDirectoryName(configPath);

                // 检查目录是否存在，不存在则创建
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    // 创建目标目录
                    Directory.CreateDirectory(dir);
                }

                // 根据文件扩展名选择序列化格式
                if (configPath.EndsWith(".yaml") || configPath.EndsWith(".yml"))
                {
                    // 构建 YAML 序列化器
                    ISerializer serializer = new SerializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .Build();

                    // 序列化并写入 YAML 文件
                    File.WriteAllText(configPath, serializer.Serialize(config));
                }
                else
                {
                    // 序列化为带缩进的 JSON 格式
                    string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(config,
                        Newtonsoft.Json.Formatting.Indented);

                    // 写入 JSON 文件
                    File.WriteAllText(configPath, jsonContent);
                }

                // 输出配置保存成功日志
                Console.WriteLine($"[ConfigManager] 配置已保存: {configPath}");
            }
            catch (Exception ex)
            {
                // 配置保存失败时输出错误日志
                Console.WriteLine($"[ConfigManager] 配置保存失败: {ex.Message}");
            }
        }
    }
}
