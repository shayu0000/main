using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 报告生成视图代码后置
// 说明: 报告页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 报告生成页面
    /// 提供充放电测试报告的生成、预览、下载和定时调度
    /// </summary>
    public partial class ReportView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 ReportViewModel
        /// </summary>
        public ReportView()
        {
            InitializeComponent();
        }
    }
}
