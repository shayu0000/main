using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 故障录波视图代码后置
// 说明: 故障录波页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 故障录波页面
    /// 提供故障事件的列表查询、波形数据查看和导出功能
    /// </summary>
    public partial class FaultRecordView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 FaultRecordViewModel
        /// </summary>
        public FaultRecordView()
        {
            InitializeComponent();
        }
    }
}
