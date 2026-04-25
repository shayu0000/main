using System.Windows.Controls;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 用户管理视图代码后置
// 说明: 用户管理页面，DataContext 由 DataTemplate 自动绑定
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 用户管理页面
    /// 提供系统用户的增删改查和角色管理功能
    /// </summary>
    public partial class UserManagementView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 UserManagementViewModel
        /// </summary>
        public UserManagementView()
        {
            InitializeComponent();
        }
    }
}
