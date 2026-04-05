using CommonPlugin;
using CommonPlugin.Enums;
using System;
using System.Globalization;
using System.Windows.Data;
using UnifiedDownloadManagerApiNS;

namespace UnifiedDownloadManager.Converters
{
    public class DownloadStatusEnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case UnifiedDownloadStatus.Queued:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadQueued);
                    break;
                case UnifiedDownloadStatus.Running:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadRunning);
                    break;
                case UnifiedDownloadStatus.Canceled:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadCanceled);
                    break;
                case UnifiedDownloadStatus.Paused:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadPaused);
                    break;
                case UnifiedDownloadStatus.Completed:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadCompleted);
                    break;
                case UnifiedDownloadStatus.Error:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadPaused);
                    break;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
