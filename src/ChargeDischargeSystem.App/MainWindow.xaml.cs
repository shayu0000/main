using System.Windows;
using System.Windows.Input;
using ChargeDischargeSystem.App.ViewModels;

// ============================================================
// 命名空间: ChargeDischargeSystem.App
// 功能描述: 主窗口代码后置
// 说明: 主窗口构造注入 ViewModel，设置 DataContext 并初始化窗口
// ============================================================
namespace ChargeDischargeSystem.App
{
    /// <summary>
    /// 主窗口
    /// 应用程序的主界面，包含左侧导航栏、右侧内容区和底部状态栏
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        /// <summary>
        /// 构造函数
        /// 通过依赖注入获取主窗口 ViewModel 并设置为 DataContext
        /// </summary>
        /// <param name="viewModel">主窗口视图模型</param>
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // 窗口尺寸设置
            Width = 1400;
            Height = 900;
            MinWidth = 1024;
            MinHeight = 768;
            Title = "MW级充放电上位机系统";

            // 窗口居中显示
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        /// <summary>
        /// 标题栏鼠标左键按下事件处理
        /// 实现无边框窗口的拖拽移动功能
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击切换最大化/还原
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                // 拖拽移动窗口
                DragMove();
            }
        }
    }
}
