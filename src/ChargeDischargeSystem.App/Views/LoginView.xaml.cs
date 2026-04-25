using System.Windows.Controls;
using ChargeDischargeSystem.App.ViewModels;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 登录视图代码后置
// 说明: 处理密码框的密码变更事件，同步到 ViewModel
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 登录页面视图
    /// 提供用户名密码输入和登录操作
    /// </summary>
    public partial class LoginView : UserControl
    {
        /// <summary>
        /// 构造函数
        /// DataContext 由 DataTemplate 自动绑定 LoginViewModel
        /// </summary>
        public LoginView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// DataContext变更事件处理
        /// 当DataContext设置为LoginViewModel时，将Password属性同步到PasswordBox
        /// </summary>
        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                PasswordBox.Password = viewModel.Password;
            }
        }

        /// <summary>
        /// 密码框内容变更事件处理
        /// 将 PasswordBox 的密码同步到 ViewModel 的 Password 属性
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel viewModel)
            {
                viewModel.Password = PasswordBox.Password;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void ProgressBar_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {

        }
    }
}
