using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerNS.Enums;
using UnifiedDownloadManagerNS.Models;

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

        private bool _displayDownloadSpeedInBits { get; set; }
        public bool DisplayDownloadSpeedInBits
        {
            get => _displayDownloadSpeedInBits;
            set
            {
                _displayDownloadSpeedInBits = value;
                OnPropertyChanged(nameof(DisplayDownloadSpeedInBits));
            }
        }

        public ObservableCollection<string> AllSources { get; } = new ObservableCollection<string>();


        public event PropertyChangedEventHandler PropertyChanged;

        public TaskManager()
        {
            DisplayDownloadSpeedInBits = UnifiedDownloadManager.GetSettings().DisplayDownloadSpeedInBits;
        }

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
            var settings = UnifiedDownloadManager.GetSettings();
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
                if (settings.DisplayDownloadTaskFinishedNotifications)
                {
                    var appNameArg = new Dictionary<string, IFluentType> { ["appName"] = (FluentString)ActiveTask.name };
                    if (ActiveTask.status == UnifiedDownloadStatus.Completed)
                    {
                        Playnite.WindowsNotifyIconManager.Notify(new System.Drawing.Icon(UnifiedDownloadManager.Icon), UnifiedDownloadManager.Instance.PluginName, LocalizationManager.Instance.GetString(LOC.UdmDownloadFinished, appNameArg), null);
                    }
                    else if (ActiveTask.status == UnifiedDownloadStatus.Error)
                    {
                        Playnite.WindowsNotifyIconManager.Notify(new System.Drawing.Icon(UnifiedDownloadManager.Icon), UnifiedDownloadManager.Instance.PluginName, LocalizationManager.Instance.GetString(LOC.UdmDownloadFailed, appNameArg), null);
                    }
                }
                ActiveTask = null;
                await DoNextJobInQueue();
            }
            else if (!running)
            {
                var downloadCompleteSettings = UnifiedDownloadManager.GetSettings().DoActionAfterDownloadComplete;
                if (downloadCompleteSettings != DownloadCompleteAction.Nothing)
                {
                    Window window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
                    {
                        ShowMaximizeButton = false,
                    });
                    window.Title = UnifiedDownloadManager.Instance.PluginName;
                    window.Content = new UnifiedDownloadCompleteActionView();
                    window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
                    window.SizeToContent = SizeToContent.WidthAndHeight;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.ShowDialog();
                }
            }
        }

        public async Task AddTasks(List<UnifiedDownload> downloadManagerDataList, bool silently = false)
        {
            var uniqueTasks = downloadManagerDataList.Where(downloadJob => !Downloads.Any(d => d.gameID == downloadJob.gameID)).ToList();
            foreach (var uniqueTask in uniqueTasks)
            {
                Downloads.Add(uniqueTask);
            }
            var libraryPluginSettings = UnifiedDownloadManager.Instance.UnifiedDownloadManagerData.pluginSettings.FirstOrDefault(p => p.pluginId == downloadManagerDataList[0].pluginId);
            if (libraryPluginSettings == null)
            {
                var newPluginSettings = new PluginDownloadSetting
                {
                   pluginId = downloadManagerDataList[0].pluginId
                };
                UnifiedDownloadManager.Instance.UnifiedDownloadManagerData.pluginSettings.Add(newPluginSettings);
                libraryPluginSettings = UnifiedDownloadManager.Instance.UnifiedDownloadManagerData.pluginSettings.FirstOrDefault(p => p.pluginId == downloadManagerDataList[0].pluginId);
            }
            if (libraryPluginSettings != null)
            {
                if (libraryPluginSettings.dontShowDownloadManagerWhatsUpMsg == false)
                {
                    var result = MessageCheckBoxDialog.ShowMessage("", LocalizationManager.Instance.GetString(LOC.UdmDownloadManagerWhatsUp), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDontShowAgainTitle), MessageBoxButton.OK, MessageBoxImage.Information);
                    if (result.CheckboxChecked)
                    {
                        libraryPluginSettings.dontShowDownloadManagerWhatsUpMsg = true;
                        UnifiedDownloadManager.Instance.SaveManagerData();
                    }
                }
            }

            await EnqueueTasks(uniqueTasks, silently);
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

        public Task PauseTask(UnifiedDownload task)
        {
            task.gracefulCts?.Cancel();
            task.gracefulCts?.Dispose();
            task.forcefulCts?.Dispose();
            task.status = UnifiedDownloadStatus.Paused;
            return Task.CompletedTask;
        }

        public async Task PauseAllTasks(string pluginId)
        {
            var runningOrQueuedDownloads = Downloads.Where(i => (i.status == UnifiedDownloadStatus.Running || i.status == UnifiedDownloadStatus.Queued) && i.pluginId == pluginId).ToList();
            foreach (var selectedRow in runningOrQueuedDownloads)
            {
                await PauseTask(selectedRow);
            }
            UnifiedDownloadManager.Instance.SaveManagerData();
        }

        public async Task PauseAllTasks()
        {
            var runningOrQueuedDownloads = Downloads.Where(i => i.status == UnifiedDownloadStatus.Running || i.status == UnifiedDownloadStatus.Queued).ToList();
            foreach (var selectedRow in runningOrQueuedDownloads)
            {
                await PauseTask(selectedRow);
            }
            UnifiedDownloadManager.Instance.SaveManagerData();
        }

        public async Task CancelTask(UnifiedDownload task)
        {
            task.gracefulCts?.Cancel();
            task.gracefulCts?.Dispose();
            task.forcefulCts?.Dispose();
            var unifiedDownloadLogic = GetUnifiedDownloadLogic(task.pluginId);
            await unifiedDownloadLogic.OnCancelDownload(task);
            task.status = UnifiedDownloadStatus.Canceled;
            task.progress = 0;
            task.downloadedBytes = 0;
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

        public void OpenDownloadPropertiesWindows(UnifiedDownload selectedEntry)
        {
            var unifiedDownloadLogic = GetUnifiedDownloadLogic(selectedEntry.pluginId);
            unifiedDownloadLogic.OpenDownloadPropertiesWindow(selectedEntry);
        }

    }
}
