using Playnite;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;

namespace UnifiedDownloadManagerApiNS
{
    public class UnifiedDownloadManagerApi
    {
        private Plugin UdmPlugin { get; set; }
        private readonly IUnifiedTaskManager? manager;

        public UnifiedDownloadManagerApi(IPlayniteApi playniteApi)
        {
            manager = GetTaskManager();
            UdmPlugin = playniteApi.Addons.GetPlugin(UnifiedDownloadManagerSharedProperties.Id)!;
            if (manager == null)
            {
            }
        }

        private IUnifiedTaskManager? GetTaskManager()
        {
            var pluginInterface = UdmPlugin as IUnifiedDownloadManager;
            return pluginInterface?.Manager;
        }

        public async Task AddTasks(List<UnifiedDownload> downloadManagerDataList, bool silently = false)
        {
            await manager?.AddTasks(downloadManagerDataList, silently)!;
        }

        public UnifiedDownload? GetTask(string appId, string pluginId)
        {
            return manager?.GetTask(appId, pluginId);
        }
        
        public ObservableCollection<UnifiedDownload>? GetAllDownloads()
        {
            return manager?.Downloads;
        }

        public async Task PauseAllTasks(string pluginId)
        {
            await manager?.PauseAllTasks(pluginId)!;
        }

        public void RemoveTask(UnifiedDownload downloadItem)
        {
            manager?.RemoveTask(downloadItem);
        }

    }
}
