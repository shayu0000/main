using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChargeDischargeSystem.Common.Config;
using ChargeDischargeSystem.Core.Services;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.ViewModels
// 功能描述: 系统设置视图模型
// 说明: 管理CAN配置、数据记录配置、性能参数和数据库维护
// ============================================================
namespace ChargeDischargeSystem.App.ViewModels
{
    /// <summary>
    /// 系统设置视图模型
    /// 负责系统各类配置项的统一管理：
    ///   1. CAN通信配置（波特率、通道、轮询间隔等）
    ///   2. 数据记录配置（采样间隔、缓冲区大小、保留天数等）
    ///   3. 系统性能配置（工作线程、刷新间隔、图表数据点等）
    ///   4. 公司信息配置
    ///   5. 数据库备份与恢复
    ///   6. 配置文件的导出和导入
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        #region 字段

        private readonly IConfigService _configService;

        #endregion

        #region 可观察属性 - CAN配置

        /// <summary>CAN设备索引</summary>
        [ObservableProperty]
        private int _canDeviceIndex;

        /// <summary>CAN通道号</summary>
        [ObservableProperty]
        private int _canChannelIndex;

        /// <summary>CAN波特率(bps)</summary>
        [ObservableProperty]
        private int _canBitrate = 500000;

        /// <summary>是否使用模拟CAN卡</summary>
        [ObservableProperty]
        private bool _canUseMock = true;

        /// <summary>CAN数据采集轮询间隔(ms)</summary>
        [ObservableProperty]
        private int _canPollIntervalMs = 100;

        // ---- CAN配置对象（复合属性） ----

        /// <summary>CAN配置完整对象</summary>
        [ObservableProperty]
        private CanConfigSection _canConfig = new CanConfigSection();

        #endregion

        #region 可观察属性 - 数据记录配置

        /// <summary>默认采样间隔(ms)</summary>
        [ObservableProperty]
        private int _defaultSampleIntervalMs = 1000;

        /// <summary>缓冲区最大条目数</summary>
        [ObservableProperty]
        private int _bufferMaxSize = 1000;

        /// <summary>缓冲区刷新间隔(ms)</summary>
        [ObservableProperty]
        private int _flushIntervalMs = 5000;

        /// <summary>高分辨率数据保留天数</summary>
        [ObservableProperty]
        private int _highResRetentionDays = 30;

        /// <summary>自动备份间隔(小时)</summary>
        [ObservableProperty]
        private int _autoBackupIntervalHours = 24;

        /// <summary>数据记录配置完整对象</summary>
        [ObservableProperty]
        private DataLoggingConfig _dataLoggingConfig = new DataLoggingConfig();

        #endregion

        #region 可观察属性 - 性能配置

        /// <summary>工作线程数</summary>
        [ObservableProperty]
        private int _workerThreads = 4;

        /// <summary>UI数据刷新间隔(ms)</summary>
        [ObservableProperty]
        private int _uiRefreshIntervalMs = 500;

        /// <summary>图表最大显示数据点数</summary>
        [ObservableProperty]
        private int _maxChartDataPoints = 3600;

        /// <summary>性能配置完整对象</summary>
        [ObservableProperty]
        private PerformanceConfig _performanceConfig = new PerformanceConfig();

        #endregion

        #region 可观察属性 - 公司信息

        /// <summary>公司名称</summary>
        [ObservableProperty]
        private string _companyName = "公司名称";

        /// <summary>公司Logo路径</summary>
        [ObservableProperty]
        private string _companyLogoPath = "Resources/Images/logo.png";

        /// <summary>公司信息完整对象</summary>
        [ObservableProperty]
        private CompanyInfo _companyInfo = new CompanyInfo();

        #endregion

        #region 可观察属性 - 数据库

        /// <summary>数据库文件路径</summary>
        [ObservableProperty]
        private string _databasePath = "Data/mw_scada.db";

        /// <summary>数据库文件大小(MB)</summary>
        [ObservableProperty]
        private double _databaseSizeMb;

        #endregion

        #region 可观察属性 - 状态

        /// <summary>操作状态消息</summary>
        [ObservableProperty]
        private string _statusMessage = "就绪";

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 SettingsViewModel 实例
        /// 从 ConfigService 加载当前配置到各属性
        /// </summary>
        /// <param name="configService">配置管理服务接口（通过 DI 注入）</param>
        public SettingsViewModel(IConfigService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            // 初始化时加载当前配置
            LoadCurrentSettings();
        }

        #endregion

        #region 命令

        /// <summary>保存所有设置到配置文件</summary>
        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                // 保存 CAN 配置
                _configService.SetConfigValue("can_config", "device_index", CanDeviceIndex);
                _configService.SetConfigValue("can_config", "channel_index", CanChannelIndex);
                _configService.SetConfigValue("can_config", "bitrate", CanBitrate);
                _configService.SetConfigValue("can_config", "use_mock", CanUseMock);
                _configService.SetConfigValue("can_config", "poll_interval_ms", CanPollIntervalMs);

                // 保存数据记录配置
                _configService.SetConfigValue("data_logging", "default_sample_interval_ms", DefaultSampleIntervalMs);
                _configService.SetConfigValue("data_logging", "buffer_max_size", BufferMaxSize);
                _configService.SetConfigValue("data_logging", "flush_interval_ms", FlushIntervalMs);
                _configService.SetConfigValue("data_logging", "high_res_retention_days", HighResRetentionDays);
                _configService.SetConfigValue("data_logging", "auto_backup_interval_hours", AutoBackupIntervalHours);

                // 保存性能配置
                _configService.SetConfigValue("performance", "worker_threads", WorkerThreads);
                _configService.SetConfigValue("performance", "ui_refresh_interval_ms", UiRefreshIntervalMs);
                _configService.SetConfigValue("performance", "max_chart_data_points", MaxChartDataPoints);

                // 保存公司信息
                _configService.SetConfigValue("company", "name", CompanyName);
                _configService.SetConfigValue("company", "logo_path", CompanyLogoPath);

                // 持久化到文件
                var success = await Task.Run(() => _configService.SaveConfig());
                StatusMessage = success ? "设置已保存" : "设置保存失败";
            }
            catch (Exception ex) { StatusMessage = $"保存失败：{ex.Message}"; }
        }

        /// <summary>重新加载配置文件（热更新）</summary>
        [RelayCommand]
        private async Task ReloadConfigAsync()
        {
            try
            {
                var ok = await Task.Run(() => _configService.ReloadConfig());
                if (ok)
                {
                    LoadCurrentSettings();
                    StatusMessage = "配置已重新加载";
                }
                else
                    StatusMessage = "配置重载失败";
            }
            catch (Exception ex) { StatusMessage = $"重载异常：{ex.Message}"; }
        }

        /// <summary>备份数据库</summary>
        [RelayCommand]
        private async Task BackupDatabaseAsync()
        {
            try
            {
                // 实际项目中调用数据库备份逻辑
                var backupPath = $"Data/backup/mw_scada_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                if (System.IO.File.Exists(DatabasePath))
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backupPath)!);
                    await Task.Run(() => System.IO.File.Copy(DatabasePath, backupPath, true));
                    StatusMessage = $"数据库已备份到：{backupPath}";
                }
                else
                    StatusMessage = "数据库文件不存在，无法备份";
            }
            catch (Exception ex) { StatusMessage = $"备份失败：{ex.Message}"; }
        }

        /// <summary>从备份恢复数据库</summary>
        [RelayCommand]
        private void RestoreDatabase()
        {
            // 实际项目中使用文件对话框选择备份文件
            // 然后关闭数据库连接，替换文件，重新打开
            StatusMessage = "请选择要恢复的备份文件（功能待UI绑定文件对话框）";
        }

        /// <summary>导出配置文件到JSON</summary>
        [RelayCommand]
        private async Task ExportConfigAsync()
        {
            try
            {
                var exportPath = $"config/app_config_export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var ok = await Task.Run(() => _configService.ExportConfig(exportPath));
                StatusMessage = ok ? $"配置已导出到：{exportPath}" : "导出失败";
            }
            catch (Exception ex) { StatusMessage = $"导出异常：{ex.Message}"; }
        }

        /// <summary>从JSON文件导入配置</summary>
        [RelayCommand]
        private async Task ImportConfigAsync()
        {
            // 实际项目中使用文件对话框选择导入文件
            StatusMessage = "请选择要导入的配置文件（功能待UI绑定文件对话框）";
            await Task.CompletedTask;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从 ConfigService 加载当前配置到ViewModel属性
        /// </summary>
        private void LoadCurrentSettings()
        {
            try
            {
                // 加载 CAN 配置
                CanDeviceIndex = _configService.GetConfigValue<int>("can_config", "device_index");
                CanChannelIndex = _configService.GetConfigValue<int>("can_config", "channel_index");
                CanBitrate = _configService.GetConfigValue<int>("can_config", "bitrate");
                CanUseMock = _configService.GetConfigValue<bool>("can_config", "use_mock");
                CanPollIntervalMs = _configService.GetConfigValue<int>("can_config", "poll_interval_ms");

                // 加载数据记录配置
                DefaultSampleIntervalMs = _configService.GetConfigValue<int>("data_logging", "default_sample_interval_ms");
                BufferMaxSize = _configService.GetConfigValue<int>("data_logging", "buffer_max_size");
                FlushIntervalMs = _configService.GetConfigValue<int>("data_logging", "flush_interval_ms");
                HighResRetentionDays = _configService.GetConfigValue<int>("data_logging", "high_res_retention_days");
                AutoBackupIntervalHours = _configService.GetConfigValue<int>("data_logging", "auto_backup_interval_hours");

                // 加载性能配置
                WorkerThreads = _configService.GetConfigValue<int>("performance", "worker_threads");
                UiRefreshIntervalMs = _configService.GetConfigValue<int>("performance", "ui_refresh_interval_ms");
                MaxChartDataPoints = _configService.GetConfigValue<int>("performance", "max_chart_data_points");

                // 加载公司信息
                CompanyName = _configService.GetConfigValue<string>("company", "name");
                CompanyLogoPath = _configService.GetConfigValue<string>("company", "logo_path");

                // 加载数据库路径
                DatabasePath = _configService.GetConfigValue<string>("general", "database_path");

                // 更新复合配置对象
                CanConfig = new CanConfigSection
                {
                    DeviceIndex = CanDeviceIndex,
                    ChannelIndex = CanChannelIndex,
                    Bitrate = CanBitrate,
                    UseMock = CanUseMock,
                    PollIntervalMs = CanPollIntervalMs
                };
                DataLoggingConfig = new DataLoggingConfig
                {
                    DefaultSampleIntervalMs = DefaultSampleIntervalMs,
                    BufferMaxSize = BufferMaxSize,
                    FlushIntervalMs = FlushIntervalMs,
                    HighResRetentionDays = HighResRetentionDays,
                    AutoBackupIntervalHours = AutoBackupIntervalHours
                };
                PerformanceConfig = new PerformanceConfig
                {
                    WorkerThreads = WorkerThreads,
                    UiRefreshIntervalMs = UiRefreshIntervalMs,
                    MaxChartDataPoints = MaxChartDataPoints
                };
                CompanyInfo = new CompanyInfo
                {
                    Name = CompanyName,
                    LogoPath = CompanyLogoPath
                };

                StatusMessage = "配置加载成功";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载配置失败：{ex.Message}";
            }
        }

        #endregion
    }
}
