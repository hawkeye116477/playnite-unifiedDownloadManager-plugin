using System.Collections.ObjectModel;
using UnifiedDownloadManagerApiNS;

namespace UnifiedDownloadManager.Models
{
    public class UnifiedDownloadManagerData
    {
        public ObservableCollection<UnifiedDownload> downloads { get; set; }
    }
}
