using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 设备校准视图代码后置
// 说明: 校准页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 设备校准页面
    /// 提供零点校准、量程校准、线性校准功能和校准历史记录
    /// </summary>
    public partial class CalibrationView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 CalibrationViewModel
        /// </summary>
        public CalibrationView()
        {
            InitializeComponent();
        }
    }
}
