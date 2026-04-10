using System.Threading.Tasks;
using UnifiedDownloadManagerApiNS.Models;

namespace UnifiedDownloadManagerApiNS.Interfaces
{
    public interface IUnifiedDownloadLogic
    {
        Task StartDownload(UnifiedDownload downloadTask);
        Task OnCancelDownload(UnifiedDownload downloadTask);
        Task OnRemoveDownloadEntry(UnifiedDownload downloadTask);
        void OpenDownloadPropertiesWindow(UnifiedDownload selectedEntry);
    }
}