namespace UnifiedDownloadManagerApiNS.Interfaces
{
    public interface IUnifiedDownloadManager
    {
        IUnifiedTaskManager Manager { get; set; }
    }
}
