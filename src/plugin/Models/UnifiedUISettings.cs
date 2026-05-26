using System.Collections.Generic;

namespace UnifiedDownloadManagerNS.Models
{
    public class UnifiedUISettings
    {
        public UnifiedMessagesSettings messagesSettings { get; } = new UnifiedMessagesSettings();
        public UnifiedColumnsSettings columnsSettings { get; set; } = new UnifiedColumnsSettings();

        public class UnifiedMessagesSettings
        {
            public bool dontShowDownloadManagerWhatsUpMsg { get; set; } = false;
        }

        public class UnifiedColumnsSettings
        {
            public List<int> hiddenColumns { get; set; }
        }
    }
}
