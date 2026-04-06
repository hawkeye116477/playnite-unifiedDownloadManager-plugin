using CommonPlugin;
using System;
using System.Globalization;
using System.Windows.Data;
using UnifiedDownloadManagerApiNS;

namespace UnifiedDownloadManagerNS.Converters
{
    public class DownloadStatusEnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case UnifiedDownloadStatus.Queued:
                    value = LocalizationManager.Instance.GetString(LOC.UdmDownloadQueued);
                    break;
                case UnifiedDownloadStatus.Running:
                    value = LocalizationManager.Instance.GetString(LOC.UdmDownloadRunning);
                    break;
                case UnifiedDownloadStatus.Canceled:
                    value = LocalizationManager.Instance.GetString(LOC.UdmDownloadCanceled);
                    break;
                case UnifiedDownloadStatus.Paused:
                    value = LocalizationManager.Instance.GetString(LOC.UdmDownloadPaused);
                    break;
                case UnifiedDownloadStatus.Completed:
                    value = LocalizationManager.Instance.GetString(LOC.UdmDownloadCompleted);
                    break;
                case UnifiedDownloadStatus.Error:
                    value = LocalizationManager.Instance.GetString(LOC.UdmDownloadPaused);
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
