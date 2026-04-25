using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 电池协议视图代码后置
// 说明: 电池协议管理页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 电池协议管理页面
    /// 提供BMS电池协议的加载、电池包数据查看和单体数据展示
    /// </summary>
    public partial class BatteryProtocolView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 BatteryProtocolViewModel
        /// </summary>
        public BatteryProtocolView()
        {
            InitializeComponent();
        }
    }
}
