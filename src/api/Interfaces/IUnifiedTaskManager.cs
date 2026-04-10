using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using UnifiedDownloadManagerApiNS.Models;

namespace UnifiedDownloadManagerApiNS.Interfaces
{
    public interface IUnifiedTaskManager : INotifyPropertyChanged
    {
        ObservableCollection<UnifiedDownload> Downloads { get; set; }
        Task AddTasks(List<UnifiedDownload> downloadManagerDataList, bool silently = false);
        UnifiedDownload GetTask(string appId, string pluginId);
        Task PauseAllTasks(string pluginId);
        void RemoveTask(UnifiedDownload downloadItem);
    }
}