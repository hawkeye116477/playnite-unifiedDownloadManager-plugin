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
            public Dictionary<string, UnifiedColumn> columns { get; set; } = [];
            public bool horizontalScrolling { get; set; }
        }
    }
    
    public class UnifiedColumn
    {
        public bool hidden { get; set; } = false;
        public int index { get; set; }
    }
    
}
