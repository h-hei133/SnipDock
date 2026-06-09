using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SnipDock.App.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = false;
            if (value is bool b)
            {
                flag = b;
            }
            else if (value is bool?)
            {
                flag = ((bool?)value).GetValueOrDefault();
            }
            else
            {
                // Fallback for object references (e.g. SelectedPrompt != null)
                flag = value != null;
            }

            if (parameter is string param && param.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
