using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 设备管理视图代码后置
// 说明: 设备管理页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 设备管理页面
    /// 提供设备列表查看、筛选、添加和编辑功能
    /// </summary>
    public partial class DeviceManagementView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 DeviceManagementViewModel
        /// </summary>
        public DeviceManagementView()
        {
            InitializeComponent();
        }
    }
}
