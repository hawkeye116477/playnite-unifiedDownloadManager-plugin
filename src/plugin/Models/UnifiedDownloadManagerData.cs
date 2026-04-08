using System.Collections.ObjectModel;
using UnifiedDownloadManagerApiNS;

namespace UnifiedDownloadManagerNS.Models
{
    public class UnifiedDownloadManagerData
    {
        public ObservableCollection<UnifiedDownload> downloads { get; set; }
        public ObservableCollection<PluginDownloadSetting> pluginSettings { get; } = new ObservableCollection<PluginDownloadSetting>();
    }

    public class PluginDownloadSetting
    {
        public string pluginId { get; set; }
        public bool dontShowDownloadManagerWhatsUpMsg { get; set; } = false;
    }
}
