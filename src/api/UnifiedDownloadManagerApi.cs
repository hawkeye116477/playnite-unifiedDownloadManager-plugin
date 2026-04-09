using Playnite.SDK;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnifiedDownloadManagerApiNS
{
    public class UnifiedDownloadManagerApi
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        public Playnite.SDK.Plugins.Plugin udmPlugin => playniteAPI.Addons.Plugins.Find(plugin => plugin.Id.Equals(UnifiedDownloadManagerSharedProperties.Id));
        private readonly IUnifiedTaskManager manager;

        public UnifiedDownloadManagerApi()
        {
            manager = GetTaskManager();
            if (manager == null)
            {
                return;
            }
        }

        private IUnifiedTaskManager GetTaskManager()
        {
            var pluginInterface = udmPlugin as IUnifiedDownloadManager;
            return pluginInterface.Manager;
        }

        public async Task AddTasks(List<UnifiedDownload> downloadManagerDataList, bool silently = false)
        {
            await manager.AddTasks(downloadManagerDataList, silently);
        }

        public UnifiedDownload GetTask(string appId, string pluginId)
        {
            return manager.GetTask(appId, pluginId);
        }

    }
}
