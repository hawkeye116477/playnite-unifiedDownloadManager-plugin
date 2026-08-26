using System.Collections.ObjectModel;
using UnifiedDownloadManagerApiNS.Models;

namespace UnifiedDownloadManagerNS.Models
{
    public class UnifiedDownloadManagerData
    {
        public ObservableCollection<UnifiedDownload>? downloads { get; set; }
    }
}