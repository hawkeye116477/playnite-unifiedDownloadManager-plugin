using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;
using UnifiedDownloadManagerNS.Enums;
using UnifiedDownloadManagerNS.Models;

namespace UnifiedDownloadManagerNS
{
    public class TaskManager : INotifyPropertyChanged, IUnifiedTaskManager
    {
        public ILogger logger = LogManager.GetLogger();
        public ObservableCollection<UnifiedDownload> Downloads { get; set; }
        private IPlayniteAPI playniteAPI = API.Instance;
        private UnifiedDownload _activeTask { get; set; }
        public UnifiedDownload ActiveTask
        {
            get => _activeTask;
            set
            {
                if (_activeTask != null)
                {
                    _activeTask.PropertyChanged -= ActiveTask_PropertyChanged;
                }

                _activeTask = value;

                if (_activeTask != null)
                {
                    _activeTask.PropertyChanged += ActiveTask_PropertyChanged;
                }

                UnifiedDownloadManager.GetPanel().ProgressValue = ActiveTask?.progress ?? 0;
                OnPropertyChanged(nameof(ActiveTask));
            }
        }

        private void ActiveTask_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActiveTask.progress))
            {
                UnifiedDownloadManager.GetPanel().ProgressValue = ActiveTask?.progress ?? 0;
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
                if (ActiveTask != null)
                {
                    var unifiedDownloadLogic = GetUnifiedDownloadLogic(queuedList[0].pluginId);
                    try
                    {
                        await unifiedDownloadLogic.StartDownload(queuedList[0]);
                    }
                    catch (Exception ex)
                    {
                        if (ex is OperationCanceledException && (queuedList[0].status == UnifiedDownloadStatus.Canceled || queuedList[0].status == UnifiedDownloadStatus.Paused))
                        {
                            if (queuedList[0].status == UnifiedDownloadStatus.Canceled)
                            {
                                await unifiedDownloadLogic.OnCancelDownload(queuedList[0]);
                            }
                        }
                        else
                        {
                            logger.Error($"An error occurred while downloading {queuedList[0].name}: {ex}.");
                            queuedList[0].status = UnifiedDownloadStatus.Error;
                        }
                    }
                    finally
                    {
                        if (queuedList[0].status == UnifiedDownloadStatus.Canceled)
                        {
                            await unifiedDownloadLogic.OnCancelDownload(queuedList[0]);
                        }
                        queuedList[0].gracefulCts?.Dispose();
                        queuedList[0].forcefulCts?.Dispose();
                        queuedList[0].gracefulCts = null;
                        queuedList[0].forcefulCts = null;
                        if (settings.DisplayDownloadTaskFinishedNotifications)
                        {
                            var appNameArg = new Dictionary<string, IFluentType> { ["appName"] = (FluentString)ActiveTask.name };
                            var bitmap = new Bitmap(UnifiedDownloadManager.Icon);
                            var iconHandle = bitmap.GetHicon();
                            var icon = Icon.FromHandle(iconHandle);
                            if (ActiveTask.status == UnifiedDownloadStatus.Completed)
                            {
                                Playnite.WindowsNotifyIconManager.Notify(icon, UnifiedDownloadManager.Instance.PluginName, LocalizationManager.Instance.GetString(LOC.UdmDownloadFinished, appNameArg), null);
                            }
                            else if (ActiveTask.status == UnifiedDownloadStatus.Error)
                            {
                                Playnite.WindowsNotifyIconManager.Notify(icon, UnifiedDownloadManager.Instance.PluginName, LocalizationManager.Instance.GetString(LOC.UdmDownloadFailed, appNameArg), null);
                            }
                            bitmap.Dispose();
                            icon.Dispose();
                        }
                        ActiveTask = null;
                        await DoNextJobInQueue();
                    }
                }
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
            var existingKeys = new HashSet<(string gameID, string pluginId)>(Downloads?
                .Where(d => d != null)
                .Select(d => (d.gameID, d.pluginId))
                ?? Enumerable.Empty<(string, string)>());
            var uniqueTasks = downloadManagerDataList
                .Where(downloadJob => !existingKeys.Contains((downloadJob.gameID, downloadJob.pluginId)))
                .ToList();
            if (uniqueTasks.Count > 0)
            {
                DateTimeOffset now = DateTime.UtcNow;
                foreach (var uniqueTask in uniqueTasks)
                {
                    if (uniqueTask.addedTime == 0)
                    {
                        uniqueTask.addedTime = now.ToUnixTimeSeconds();
                    }
                    bool canAdd = true;
                    if (uniqueTask.sourceName.IsNullOrEmpty())
                    {
                        logger.Warn("Empty source for download item.");
                    }
                    if (uniqueTask.gameID.IsNullOrEmpty())
                    {
                        logger.Error("Empty game id for download item isn't allowed.");
                        canAdd = false;
                    }
                    if (uniqueTask.pluginId.IsNullOrEmpty())
                    {
                        logger.Error("Empty plugin id for download item isn't allowed.");
                        canAdd = false;
                    }
                    if (uniqueTask.name.IsNullOrEmpty())
                    {
                        logger.Warn("Empty name for download item.");
                    }
                    if (canAdd)
                    {
                        Downloads.Add(uniqueTask);
                    }
                }
                if (!silently)
                {
                    var messagesSettings = UnifiedDownloadManager.Instance.UnifiedDownloadManagerData.messagesSettings;
                    if (messagesSettings.dontShowDownloadManagerWhatsUpMsg == false)
                    {
                        var result = MessageCheckBoxDialog.ShowMessage("", LocalizationManager.Instance.GetString(LOC.UdmDownloadManagerWhatsUp), LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDontShowAgainTitle), MessageBoxButton.OK, MessageBoxImage.Information);
                        if (result.CheckboxChecked)
                        {
                            messagesSettings.dontShowDownloadManagerWhatsUpMsg = true;
                            UnifiedDownloadManager.Instance.SaveManagerData();
                        }
                    }
                }
                await DoNextJobInQueue();
            }
        }

        public async Task ResumeTasks(List<UnifiedDownload> downloadManagerDataList)
        {
            foreach (var downloadJob in downloadManagerDataList)
            {
                var wantedItem = Downloads.FirstOrDefault(item => item.gameID == downloadJob.gameID);
                if (wantedItem != null)
                {
                    wantedItem.status = UnifiedDownloadStatus.Queued;
                }
            }
            await DoNextJobInQueue();
        }

        public Task PauseTask(UnifiedDownload task)
        {
            task.gracefulCts?.Cancel();
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

        public void CancelTask(UnifiedDownload task)
        {
            task.gracefulCts?.Cancel();
            task.status = UnifiedDownloadStatus.Canceled;
            task.progress = 0;
            task.downloadedBytes = 0;
        }

        public async Task RemoveDownloadEntry(UnifiedDownload selectedEntry)
        {
            var unifiedDownloadLogic = GetUnifiedDownloadLogic(selectedEntry.pluginId);
            if (selectedEntry.status == UnifiedDownloadStatus.Running)
            {
                CancelTask(selectedEntry);
            }
            await unifiedDownloadLogic.OnRemoveDownloadEntry(selectedEntry);
            Downloads.Remove(selectedEntry);
        }

        public void OpenDownloadPropertiesWindows(UnifiedDownload selectedEntry)
        {
            var unifiedDownloadLogic = GetUnifiedDownloadLogic(selectedEntry.pluginId);
            unifiedDownloadLogic.OpenDownloadPropertiesWindow(selectedEntry);
        }

        public void RemoveTask(UnifiedDownload downloadItem)
        {
            Downloads.Remove(downloadItem);
        }

    }
}
