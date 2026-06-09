using System;
using System.Globalization;
using System.Windows.Data;

namespace SnipDock.App.Converters
{
    public class TimeZoneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                // Convert stored UTC to Local time
                return dateTime.ToLocalTime();
            }
            return value;
        }

        public object GridConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
