using System.Threading.Tasks;

namespace UnifiedDownloadManagerApiNS
{
    public interface IUnifiedDownloadLogic
    {
        Task StartDownload(UnifiedDownload downloadTask);
        Task OnCancelDownload(UnifiedDownload downloadTask);
        Task OnRemoveDownloadEntry(UnifiedDownload downloadTask);
        void OpenDownloadPropertiesWindow(UnifiedDownload selectedEntry);
    }
}