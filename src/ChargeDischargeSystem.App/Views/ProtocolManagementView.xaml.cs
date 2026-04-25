using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 协议管理视图代码后置
// 说明: 协议管理页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 协议管理页面
    /// 提供通信协议的加载、管理和解析测试功能
    /// </summary>
    public partial class ProtocolManagementView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 ProtocolManagementViewModel
        /// </summary>
        public ProtocolManagementView()
        {
            InitializeComponent();
        }
    }
}
