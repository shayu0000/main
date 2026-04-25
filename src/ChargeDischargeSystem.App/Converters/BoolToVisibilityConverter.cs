using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

// ============================================================
// 命名空间: ChargeDischargeSystem.App.Converters
// 功能描述: 布尔值到可见性转换器
// 说明: 用于在XAML中将布尔值转换为 Visibility 枚举
// ============================================================
namespace ChargeDischargeSystem.App.Converters
{
    /// <summary>
    /// 布尔值到可见性转换器
    /// true → Visible, false → Collapsed
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible;
            return false;
        }
    }

    /// <summary>
    /// 反转布尔值到可见性转换器
    /// false → Visible, true → Collapsed
    /// </summary>
    public class InvertBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility != Visibility.Visible;
            return true;
        }
    }
}
