using CommonPlugin;
using Playnite.SDK;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UnifiedDownloadManagerNS.Converters
{
    public class SpeedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var downloadSpeedBytes = value;
            var displayDownloadSpeedInBits = UnifiedDownloadManager.GetSettings().DisplayDownloadSpeedInBits;

            if (downloadSpeedBytes != null && downloadSpeedBytes != DependencyProperty.UnsetValue)
            {
                return CommonHelpers.FormatSize((double)downloadSpeedBytes, "B", displayDownloadSpeedInBits) + "/s";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
