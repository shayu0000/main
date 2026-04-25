using System.Windows;
using System.Windows.Input;
using ChargeDischargeSystem.App.ViewModels;
using ChargeDischargeSystem.App.Views;

// ============================================================
// 命名空间: ChargeDischargeSystem.App
// 功能描述: 登录窗口代码后置
// 说明: 登录窗口是一个简单的Window包装器，
//       内嵌 LoginView 用户控件作为登录表单。
//       支持无边框窗口拖拽移动和关闭操作。
// ============================================================
namespace ChargeDischargeSystem.App
{
    /// <summary>
    /// 登录窗口
    /// 包装 LoginView 用户控件，提供无边框窗口的拖拽移动和关闭功能。
    /// 登录成功后由 App.xaml.cs 关闭此窗口并切换至主窗口。
    /// </summary>
    public partial class LoginWindow : Window
    {
        /// <summary>
        /// 登录视图模型引用
        /// </summary>
        private readonly LoginViewModel _loginViewModel;

        /// <summary>
        /// 构造函数
        /// 初始化登录窗口，将 LoginViewModel 绑定到内嵌的 LoginView
        /// </summary>
        /// <param name="loginViewModel">登录视图模型（通过DI注入）</param>
        public LoginWindow(LoginViewModel loginViewModel)
        {
            InitializeComponent();

            // 保存 ViewModel 引用
            _loginViewModel = loginViewModel ?? throw new System.ArgumentNullException(nameof(loginViewModel));

            // 将 ViewModel 设置为 LoginView 的 DataContext
            LoginViewControl.DataContext = _loginViewModel;

            // 订阅 ViewModel 的关闭请求（如登录成功后通知窗口关闭）
            // 注意：实际登录成功后由 App.xaml.cs 中的 OnLoginSuccess 事件负责窗口切换
            _loginViewModel.OnClose += () => this.Close();
        }

        /// <summary>
        /// 窗口鼠标左键按下事件处理
        /// 实现无边框窗口的拖拽移动功能（点击窗口任意位置可拖动）
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">鼠标按钮事件参数</param>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // 允许拖拽移动无边框窗口
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 处理键盘快捷键
        /// ESC 键关闭登录窗口（退出应用程序）
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">键盘事件参数</param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // ESC键关闭登录窗口 -> 退出应用程序
                Application.Current.Shutdown();
            }
        }
    }
}
