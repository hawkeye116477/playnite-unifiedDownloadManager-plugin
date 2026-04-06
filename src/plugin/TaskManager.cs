using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnifiedDownloadManagerApiNS;

namespace UnifiedDownloadManagerNS
{
    public class TaskManager : INotifyPropertyChanged, IUnifiedTaskManager
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public ObservableCollection<UnifiedDownload> Downloads { get; set; } = new ObservableCollection<UnifiedDownload>();
        private IPlayniteAPI playniteAPI = API.Instance;
        public string GameTitleTBText { get; set; }
        private string _etaText;
        public string EtaText
        {
            get => _etaText;
            set
            {
                _etaText = value;
                OnPropertyChanged(nameof(EtaText));
            }
        }

        private UnifiedDownload _activeTask { get; set; }
        public UnifiedDownload ActiveTask
        {
            get => _activeTask;
            set
            {
                _activeTask = value;
                OnPropertyChanged(nameof(ActiveTask));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        public UnifiedDownload GetTask(string appId, string pluginId)
        {
            var task = Downloads.FirstOrDefault(t => t.gameID == appId && t.pluginId == pluginId);
            return task;
        }

        private IUnifiedDownloadLogic GetUnifiedDownloadLogic(string pluginId)
        {
            var targetPlugin = playniteAPI.Addons.Plugins.Find(plugin => plugin.Id == Guid.Parse(pluginId)) as IUnifiedDownloadProvider;
            return targetPlugin.UnifiedDownloadLogic;

        }

        public async Task DoNextJobInQueue()
        {
            UnifiedDownloadManager.Instance.SaveManagerData();
            var running = Downloads.Any(item => item.status == UnifiedDownloadStatus.Running);

            var queuedList = Downloads.Where(i => i.status == UnifiedDownloadStatus.Queued).ToList();
            if (!running && queuedList.Count > 0)
            {
                queuedList[0].forcefulCts = new CancellationTokenSource();
                queuedList[0].gracefulCts = new CancellationTokenSource();
                ActiveTask = queuedList[0];
                var unifiedDownloadLogic = GetUnifiedDownloadLogic(queuedList[0].pluginId);
                await unifiedDownloadLogic.StartDownload(queuedList[0]);
                ActiveTask = null;
                await DoNextJobInQueue();
            }
        }

        public async Task EnqueueTasks(List<UnifiedDownload> downloadManagerDataList, bool silently = false)
        {
            DateTimeOffset now = DateTime.UtcNow;
            foreach (var downloadJob in downloadManagerDataList)
            {
                var wantedItem = Downloads.FirstOrDefault(item => item.gameID == downloadJob.gameID);
                if (wantedItem == null)
                {
                    downloadJob.status = UnifiedDownloadStatus.Queued;
                    downloadJob.addedTime = now.ToUnixTimeSeconds();
                    Downloads.Add(downloadJob);
                }
                else
                {
                    wantedItem.status = UnifiedDownloadStatus.Queued;
                }
            }
            await DoNextJobInQueue();
        }

        public async Task PauseTask(UnifiedDownload task)
        {
            task.status = UnifiedDownloadStatus.Paused;
            task.gracefulCts?.Cancel();
            task.gracefulCts?.Dispose();
            await DoNextJobInQueue();
        }

        public async Task CancelTask(UnifiedDownload task)
        {
            task.status = UnifiedDownloadStatus.Canceled;
            task.progress = 0;
            task.downloadedBytes = 0;
            task.gracefulCts?.Cancel();
            task.gracefulCts?.Dispose();
            var unifiedDownloadLogic = GetUnifiedDownloadLogic(task.pluginId);
            await unifiedDownloadLogic.OnCancelDownload(task);
            await DoNextJobInQueue();
        }

        public async Task RemoveDownloadEntry(UnifiedDownload selectedEntry)
        {
            var unifiedDownloadLogic = GetUnifiedDownloadLogic(selectedEntry.pluginId);
            if (selectedEntry.status == UnifiedDownloadStatus.Running)
            {
                selectedEntry.gracefulCts.Cancel();
                selectedEntry.gracefulCts.Dispose();
                ActiveTask = null;
                await unifiedDownloadLogic.OnCancelDownload(selectedEntry);
            }
            Downloads.Remove(selectedEntry);
            await unifiedDownloadLogic.OnRemoveDownloadEntry(selectedEntry);
        }

    }
}
