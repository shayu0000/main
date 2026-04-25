using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ChargeDischargeSystem.Data.Database;
using ChargeDischargeSystem.Hardware.ZlgCanCard;
using ChargeDischargeSystem.Hardware.Mock;
using ChargeDischargeSystem.Core.Services;
using ChargeDischargeSystem.App.ViewModels;
using ChargeDischargeSystem.App.Views;
using ChargeDischargeSystem.Common.Config;

// ============================================================
// 命名空间: ChargeDischargeSystem.App
// 功能描述: 应用程序入口
// 说明: 负责应用程序启动初始化、依赖注入容器构建和窗口管理
// ============================================================
namespace ChargeDischargeSystem.App
{
    /// <summary>
    /// 应用程序入口类
    /// 管理应用程序的完整生命周期：
    ///   1. 启动时初始化数据库和配置
    ///   2. 构建依赖注入容器
    ///   3. 显示登录窗口并处理登录流程
    ///   4. 登录成功后切换到主窗口
    ///   5. 退出时清理资源
    /// </summary>
    public partial class App : Application
    {
        /// <summary>服务提供器（依赖注入容器）</summary>
        private ServiceProvider _serviceProvider;

        /// <summary>
        /// 应用程序启动事件
        /// 按顺序执行：初始化数据库→加载配置→构建DI容器→显示登录窗口
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                Console.WriteLine("[App] 应用程序启动中...");

                string dbPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Data", "mw_scada.db");

                string dbDir = System.IO.Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDir) && !System.IO.Directory.Exists(dbDir))
                {
                    System.IO.Directory.CreateDirectory(dbDir);
                    Console.WriteLine($"[App] 创建数据库目录: {dbDir}");
                }

                Console.WriteLine($"[App] 数据库路径: {dbPath}");

                try
                {
                    DatabaseManager.Instance.Initialize(dbPath);
                    Console.WriteLine("[App] 数据库管理器初始化成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] 数据库管理器初始化失败: {ex.Message}");
                    throw;
                }

                try
                {
                    DatabaseInitializer.InitializeDatabase(dbPath);
                    Console.WriteLine("[App] 数据库初始化成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] 数据库初始化失败: {ex.Message}");
                    throw;
                }

                try
                {
                    ConfigManager.LoadConfig();
                    Console.WriteLine("[App] 配置加载成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] 配置加载失败: {ex.Message}");
                    throw;
                }

                try
                {
                    _serviceProvider = BuildServiceProvider();
                    Console.WriteLine("[App] 依赖注入容器构建成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] 依赖注入容器构建失败: {ex.Message}");
                    throw;
                }

                try
                {
                    var loginVm = _serviceProvider.GetRequiredService<LoginViewModel>();
                    var loginWindow = new LoginWindow(loginVm);
                    Console.WriteLine("[App] 登录窗口创建成功");

                    loginVm.OnLoginSuccess += (user, token) =>
                    {
                        Console.WriteLine($"[App] 登录成功: {user.Username}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                            mainViewModel.SetUserLogin(user, user.RoleId ?? "admin");
                            Console.WriteLine("[App] 主窗口ViewModel设置成功");
                            mainWindow.DataContext = mainViewModel;
                            mainWindow.Show();
                            loginWindow.Close();
                            Console.WriteLine("[App] 主窗口显示成功");
                        });
                    };

                    loginVm.OnOpenRegisterDialog += () =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var registerVm = _serviceProvider.GetRequiredService<RegisterViewModel>();
                            var registerDialog = new RegisterDialog(registerVm);
                            registerDialog.Owner = loginWindow;
                            if (registerDialog.ShowDialog() == true)
                            {
                                Console.WriteLine("[App] 用户注册成功");
                            }
                        });
                    };

                    loginWindow.Show();
                    Console.WriteLine("[App] 登录窗口显示成功");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[App] 登录窗口显示失败: {ex.Message}");
                    throw;
                }

                Console.WriteLine("[App] 应用程序启动成功，进入消息循环");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] 启动失败: {ex.Message}");
                Console.WriteLine($"[App] 堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show(
                    $"应用程序启动失败：{ex.Message}\n\n{ex.StackTrace}",
                    "启动错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// 构建依赖注入容器
        /// 注册所有服务、ViewModel 和视图到 DI 容器中
        /// 根据配置选择使用模拟 CAN 卡或真实 ZLG CAN 卡
        /// </summary>
        /// <returns>配置完成的 ServiceProvider</returns>
        private ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            var config = ConfigManager.Instance;
            if (config.CanConfig.UseMock)
            {
                services.AddSingleton<ICanCard, MockCanCard>();
            }
            else
            {
                services.AddSingleton<ICanCard, ZlgCanCard>();
            }

            services.AddSingleton(config);
            services.AddSingleton<ICanCommunicationService, CanCommunicationService>();
            services.AddSingleton<IDeviceMonitorService, DeviceMonitorService>();
            services.AddSingleton<IDeviceManagerService, DeviceManagerService>();
            services.AddSingleton<ICalibrationService, CalibrationService>();
            services.AddSingleton<IProtocolService, ProtocolService>();
            services.AddSingleton<IBatteryProtocolService, BatteryProtocolService>();
            services.AddSingleton<IDataLogService, DataLogService>();
            services.AddSingleton<IFirmwareService, FirmwareService>();
            services.AddSingleton<IFaultRecordService, FaultRecordService>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IReportService, ReportService>();
            services.AddSingleton<IConfigService, ConfigService>();

            services.AddTransient<LoginViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<DeviceManagementViewModel>();
            services.AddTransient<CalibrationViewModel>();
            services.AddTransient<ProtocolManagementViewModel>();
            services.AddTransient<BatteryProtocolViewModel>();
            services.AddTransient<DataRecordViewModel>();
            services.AddTransient<FirmwareUpgradeViewModel>();
            services.AddTransient<FaultRecordViewModel>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<ReportViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<RegisterViewModel>();

            services.AddTransient<MainWindow>();
            services.AddTransient<LoginView>();
            services.AddTransient<DashboardView>();
            services.AddTransient<DeviceManagementView>();
            services.AddTransient<CalibrationView>();
            services.AddTransient<ProtocolManagementView>();
            services.AddTransient<BatteryProtocolView>();
            services.AddTransient<DataRecordView>();
            services.AddTransient<FirmwareUpgradeView>();
            services.AddTransient<FaultRecordView>();
            services.AddTransient<UserManagementView>();
            services.AddTransient<ReportView>();
            services.AddTransient<SettingsView>();
            services.AddTransient<RegisterDialog>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 应用程序退出事件
        /// 关闭数据库连接并释放 DI 容器资源
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            DatabaseManager.Instance?.Close();
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}