namespace UnifiedDownloadManagerNS.Models
{
    public class UnifiedUISettings
    {
        public UnifiedColumnsSettings columnsSettings { get; set; } = new UnifiedColumnsSettings();

        public class UnifiedColumnsSettings
        {
            public Dictionary<string, UnifiedColumn>? columns { get; set; }
            public bool horizontalScrolling { get; set; }
            public bool columnsLocked { get; set; }
        }
    }

    public class UnifiedColumn
    {
        public bool hidden { get; set; } = false;
        public int index { get; set; }
    }
}