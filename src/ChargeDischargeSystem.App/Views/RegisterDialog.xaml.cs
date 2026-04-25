using System.Windows;
using ChargeDischargeSystem.App.ViewModels;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Views
// 功能描述: 用户注册对话框代码后置
// 说明: 注册新用户弹窗，DataContext 构造函数注入 RegisterViewModel
// ============================================================
namespace ChargeDischargeSystem.App.Views
{
    /// <summary>
    /// 用户注册对话框
    /// 提供新用户注册的交互界面，接收 RegisterViewModel 驱动
    /// </summary>
    public partial class RegisterDialog : Window
    {
        /// <summary>
        /// 构造函数——注入 RegisterViewModel 并绑定事件
        /// 注册成功时设置 DialogResult=true 并关闭窗口
        /// 取消时设置 DialogResult=false 并关闭窗口
        /// </summary>
        /// <param name="viewModel">注册视图模型</param>
        public RegisterDialog(RegisterViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.OnRegisterSuccess += () =>
            {
                DialogResult = true;
                Close();
            };
            viewModel.OnCancel += () =>
            {
                DialogResult = false;
                Close();
            };
        }

        /// <summary>
        /// 密码框文本变化事件——同步密码到 ViewModel
        /// WPF PasswordBox 不直接支持 Binding，需手动同步
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is RegisterViewModel viewModel)
            {
                viewModel.Password = PasswordBox.Password;
            }
        }

        /// <summary>
        /// 确认密码框文本变化事件——同步确认密码到 ViewModel
        /// </summary>
        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is RegisterViewModel viewModel)
            {
                viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
            }
        }
    }
}