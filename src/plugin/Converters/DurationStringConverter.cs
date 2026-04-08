using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace UnifiedDownloadManagerNS.Converters
{
    public class DurationStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is TimeSpan duration))
            {
                return 0;
            }
            var parts = new List<string>();
            if (duration.Days > 0)
            {
                parts.Add(duration.Days.ToString("D2"));
            }
            parts.Add(duration.Hours.ToString("D2"));
            parts.Add(duration.Minutes.ToString("D2"));
            parts.Add(duration.Seconds.ToString("D2"));
            return string.Join(":", parts);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
