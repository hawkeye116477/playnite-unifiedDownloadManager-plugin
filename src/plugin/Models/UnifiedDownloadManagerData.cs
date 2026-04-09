using System.Collections.ObjectModel;
using UnifiedDownloadManagerApiNS;

namespace UnifiedDownloadManagerNS.Models
{
    public class UnifiedDownloadManagerData
    {
        public ObservableCollection<UnifiedDownload> downloads { get; set; }
        public UnifiedMessagesSettings messagesSettings { get; } = new UnifiedMessagesSettings();
    }

    public class UnifiedMessagesSettings
    {
        public bool dontShowDownloadManagerWhatsUpMsg { get; set; } = false;
    }
}
