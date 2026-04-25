using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 系统设置视图代码后置
// 说明: 设置页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 系统设置页面
    /// 提供通用设置、CAN配置、数据记录和性能参数的配置管理
    /// </summary>
    public partial class SettingsView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 SettingsViewModel
        /// </summary>
        public SettingsView()
        {
            InitializeComponent();
        }
    }
}
