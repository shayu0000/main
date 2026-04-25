using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 仪表盘视图代码后置
// 说明: 设备监控仪表盘页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 设备监控仪表盘
    /// 显示设备状态卡片、实时参数仪表盘和告警列表
    /// </summary>
    public partial class DashboardView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 DashboardViewModel
        /// </summary>
        public DashboardView()
        {
            InitializeComponent();
        }
    }
}
