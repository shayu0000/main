using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 数据记录视图代码后置
// 说明: 数据记录页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 数据记录页面
    /// 提供数据记录会话管理、历史数据查询和CSV导出功能
    /// </summary>
    public partial class DataRecordView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 DataRecordViewModel
        /// </summary>
        public DataRecordView()
        {
            InitializeComponent();
        }
    }
}
