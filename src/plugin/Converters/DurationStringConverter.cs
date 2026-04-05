using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace UnifiedDownloadManager.Converters
{
    public class DurationStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string duration))
            {
                return value;
            }
            var parts = new List<string>();
            var parsedTimeSpan = TimeSpan.Parse((string)value);
            if (parsedTimeSpan.Days > 0)
            {
                parts.Add(parsedTimeSpan.Days.ToString("D2"));
            }
            parts.Add(parsedTimeSpan.Hours.ToString("D2"));
            parts.Add(parsedTimeSpan.Minutes.ToString("D2"));
            parts.Add(parsedTimeSpan.Seconds.ToString("D2"));
            return string.Join(":", parts);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
