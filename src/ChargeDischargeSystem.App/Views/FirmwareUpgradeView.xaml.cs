using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 固件升级视图代码后置
// 说明: 固件升级页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 固件升级页面
    /// 提供设备固件的远程升级、进度监控和版本管理
    /// </summary>
    public partial class FirmwareUpgradeView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 FirmwareUpgradeViewModel
        /// </summary>
        public FirmwareUpgradeView()
        {
            InitializeComponent();
        }
    }
}
