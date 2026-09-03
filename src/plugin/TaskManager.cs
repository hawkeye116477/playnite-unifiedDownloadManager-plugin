using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PlayniteMod;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;
using UnifiedDownloadManagerNS.Enums;

namespace UnifiedDownloadManagerNS
{
    public class TaskManager : IUnifiedDownloadManagerApi
    {
        public ILogger Logger = LogManager.GetLogger<TaskManager>();
        public ObservableCollection<UnifiedDownload> Downloads { get; set; } = [];
        private IPlayniteApi PlayniteApi = UnifiedDownloadManager.PlayniteApi;
        private UnifiedDownload? _activeTask { get; set; }

        public UnifiedDownload? ActiveTask
        {
            get => _activeTask;
            set
            {
                if (_activeTask != null)
                {
                    _activeTask.PropertyChanged -= ActiveTask_PropertyChanged!;
                }

                _activeTask = value;

                if (_activeTask != null)
                {
                    _activeTask.PropertyChanged += ActiveTask_PropertyChanged!;
                }

                //UnifiedDownloadManager.GetPanel().ProgressValue = ActiveTask?.progress ?? 0;
                OnPropertyChanged(nameof(ActiveTask));
            }
        }

        private bool _canResume { get; set; }

        public bool CanResume
        {
            get => _canResume;
            set
            {
                _canResume = value;
                OnPropertyChanged(nameof(CanResume));
            }
        }

        private bool _canPause { get; set; }

        public bool CanPause
        {
            get => _canPause;
            set
            {
                _canPause = value;
                OnPropertyChanged(nameof(CanPause));
            }
        }

        private bool _canCancel { get; set; }

        public bool CanCancel
        {
            get => _canCancel;
            set
            {
                _canCancel = value;
                OnPropertyChanged(nameof(CanCancel));
            }
        }

        private ConcurrentDictionary<string, bool> tasksToRemove = new ConcurrentDictionary<string, bool>();
        private List<UnifiedDownload> selectedItems = [];

        private void ActiveTask_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // if (e.PropertyName == nameof(ActiveTask.Progress))
            // {
            //     UnifiedDownloadManager.GetPanel().ProgressValue = ActiveTask?.Progress ?? 0;
            // }
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

        public ObservableCollection<string> AllSources { get; } = [];


        public event PropertyChangedEventHandler? PropertyChanged;

        public TaskManager()
        {
            DisplayDownloadSpeedInBits = UnifiedDownloadManager.GetSettings().DisplayDownloadSpeedInBits;
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void UpdateSelectedItems(List<UnifiedDownload> selectedItemsList)
        {
            selectedItems = selectedItemsList;
            RefreshButtonStates();
        }

        public void RefreshButtonStates()
        {
            CanResume = false;
            CanPause = false;
            CanCancel = false;
            foreach (var selectedItem in selectedItems)
            {
                if (selectedItem.Status != UnifiedDownloadStatus.Completed)
                {
                    if (!CanResume && selectedItem.Status != UnifiedDownloadStatus.Running)
                    {
                        CanResume = true;
                    }

                    if (!CanPause && selectedItem.Status == UnifiedDownloadStatus.Running)
                    {
                        CanPause = true;
                    }

                    if (!CanCancel && selectedItem.Status != UnifiedDownloadStatus.Canceled)
                    {
                        CanCancel = true;
                    }
                }
            }
        }

        public UnifiedDownload? GetTask(string appId, string pluginId)
        {
            return Downloads.FirstOrDefault(t => t.GameId == appId && t.PluginId == pluginId);
        }

        public async Task<IUnifiedDownloadLogic?> GetUnifiedDownloadLogic(string pluginId)
        {
            var result = await PlayniteApi.CallPluginAsync(new(pluginId, UnifiedDownloadManagerSharedProperties.GetDownloadLogic));
            IUnifiedDownloadLogic? pluginDownloadLogic = null;
            if (result is { Success: true, Value: IUnifiedDownloadLogic newPluginDownloadLogic })
            {
                pluginDownloadLogic = newPluginDownloadLogic;
            }

            return pluginDownloadLogic;
        }

        public async Task DoNextJobInQueue()
        {
            var settings = UnifiedDownloadManager.GetSettings();
            UnifiedDownloadManager.Instance.SaveManagerData();
            var running = Downloads.Any(item => item.Status == UnifiedDownloadStatus.Running);

            var queuedList = Downloads.Where(i => i.Status == UnifiedDownloadStatus.Queued).ToList();
            if (!running && queuedList.Count > 0)
            {
                queuedList[0].ForcefulCts = new CancellationTokenSource();
                queuedList[0].GracefulCts = new CancellationTokenSource();
                ActiveTask = queuedList[0];
                if (ActiveTask != null)
                {
                    var unifiedDownloadLogic = await GetUnifiedDownloadLogic(queuedList[0].PluginId);
                    try
                    {
                        if (unifiedDownloadLogic != null)
                        {
                            await unifiedDownloadLogic.StartDownload(queuedList[0]);
                        }
                    }
                    catch (Exception ex)
                    {
                        bool isExpectedCancel = ex is OperationCanceledException &&
                                                (queuedList[0].Status == UnifiedDownloadStatus.Canceled
                                                 || queuedList[0].Status == UnifiedDownloadStatus.Paused);

                        if (!isExpectedCancel)
                        {
                            Logger.Error($"An error occurred while downloading {queuedList[0].Name}: {ex}.");
                            queuedList[0].Status = UnifiedDownloadStatus.Error;
                        }
                    }
                    finally
                    {
                        if (queuedList[0].Status == UnifiedDownloadStatus.Canceled)
                        {
                            if (unifiedDownloadLogic != null)
                            {
                                await unifiedDownloadLogic.OnCancelDownload(queuedList[0]);
                            }
                        }

                        queuedList[0].GracefulCts?.Dispose();
                        queuedList[0].ForcefulCts?.Dispose();
                        queuedList[0].GracefulCts = null;
                        queuedList[0].ForcefulCts = null;
                        if (tasksToRemove.TryRemove($"{queuedList[0].PluginId}_{queuedList[0].GameId}", out bool shouldRemove) &&
                            shouldRemove)
                        {
                            if (unifiedDownloadLogic != null)
                            {
                                await unifiedDownloadLogic.OnRemoveDownloadEntry(queuedList[0]);
                            }

                            queuedList[0].PropertyChanged -= DownloadTask_PropertyChanged;
                            Downloads.Remove(queuedList[0]);
                        }

                        if (settings.DisplayDownloadTaskFinishedNotifications)
                        {
                            var appNameArg = new Dictionary<string, IFluentType> { ["appName"] = (FluentString)ActiveTask.Name };
                            var bitmap = new Bitmap(UnifiedDownloadManager.Icon);
                            var iconHandle = bitmap.GetHicon();
                            var icon = Icon.FromHandle(iconHandle);
                            if (ActiveTask.Status == UnifiedDownloadStatus.Completed)
                            {
                                WindowsNotifyIconManager.Notify(icon, UnifiedDownloadManager.PluginName,
                                    LocalizationManager.Instance.GetString(LOC.UdmDownloadFinished, appNameArg));
                            }
                            else if (ActiveTask.Status == UnifiedDownloadStatus.Error)
                            {
                                WindowsNotifyIconManager.Notify(icon, UnifiedDownloadManager.PluginName,
                                    LocalizationManager.Instance.GetString(LOC.UdmDownloadFailed, appNameArg));
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
                    Window window = PlayniteApi.CreateWindow(new WindowCreationOptions
                    {
                        ShowMaximizeButton = false,
                    });
                    window.Title = UnifiedDownloadManager.PluginName;
                    window.Content = new UnifiedDownloadCompleteActionView();
                    window.Owner = PlayniteApi.GetLastActiveWindow();
                    window.SizeToContent = SizeToContent.WidthAndHeight;
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.ShowDialog();
                }
            }
        }

        public async Task AddTasks(List<UnifiedDownload> downloadManagerDataList, bool silently = false)
        {
            var existingKeys = new HashSet<(string gameID, string pluginId)>(Downloads.Where(d => true)
                                                                                      .Select(d => (d.GameId, d.PluginId)));
            var uniqueTasks = downloadManagerDataList
                             .Where(downloadJob => !existingKeys.Contains((downloadJob.GameId, downloadJob.PluginId)))
                             .ToList();
            if (uniqueTasks.Count > 0)
            {
                DateTimeOffset now = DateTime.UtcNow;
                foreach (var uniqueTask in uniqueTasks)
                {
                    if (uniqueTask.AddedTime == 0)
                    {
                        uniqueTask.AddedTime = now.ToUnixTimeSeconds();
                    }

                    bool canAdd = true;
                    if (uniqueTask.SourceName.IsNullOrEmpty())
                    {
                        Logger.Warn("Empty source for download item.");
                    }

                    if (uniqueTask.GameId.IsNullOrEmpty())
                    {
                        Logger.Error("Empty game id for download item isn't allowed.");
                        canAdd = false;
                    }

                    if (uniqueTask.PluginId.IsNullOrEmpty())
                    {
                        Logger.Error("Empty plugin id for download item isn't allowed.");
                        canAdd = false;
                    }

                    if (uniqueTask.Name.IsNullOrEmpty())
                    {
                        Logger.Warn("Empty name for download item.");
                    }

                    if (canAdd)
                    {
                        Downloads?.Add(uniqueTask);
                        uniqueTask.PropertyChanged += DownloadTask_PropertyChanged;
                    }
                }

                if (!silently)
                {
                    await PlayniteApi.MainView.SwitchToViewAsync("UDM.panel");
                }

                await DoNextJobInQueue();
            }
        }

        private void DownloadTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UnifiedDownload.Status))
            {
                RefreshButtonStates();
            }
        }

        public async Task ResumeTasks(List<UnifiedDownload> downloadManagerDataList)
        {
            foreach (var downloadJob in downloadManagerDataList)
            {
                var wantedItem = Downloads.FirstOrDefault(item => item.GameId == downloadJob.GameId);
                if (wantedItem != null)
                {
                    wantedItem.PropertyChanged += DownloadTask_PropertyChanged;
                    wantedItem.Status = UnifiedDownloadStatus.Queued;
                }
            }

            await DoNextJobInQueue();
        }

        public Task PauseTask(UnifiedDownload task)
        {
            task.GracefulCts?.Cancel();
            task.Status = UnifiedDownloadStatus.Paused;
            return Task.CompletedTask;
        }

        public async Task PauseAllTasks(string pluginId)
        {
            var runningOrQueuedDownloads = Downloads.Where(i =>
                                                         i.Status is UnifiedDownloadStatus.Running or UnifiedDownloadStatus.Queued &&
                                                         i.PluginId == pluginId)
                                                    .ToList();
            foreach (var selectedRow in runningOrQueuedDownloads)
            {
                await PauseTask(selectedRow);
            }

            UnifiedDownloadManager.Instance.SaveManagerData();
        }

        public async Task PauseAllTasks()
        {
            var runningOrQueuedDownloads =
                Downloads.Where(i => i.Status is UnifiedDownloadStatus.Running or UnifiedDownloadStatus.Queued).ToList();
            foreach (var selectedRow in runningOrQueuedDownloads)
            {
                await PauseTask(selectedRow);
            }

            UnifiedDownloadManager.Instance.SaveManagerData();
        }

        public void CancelTask(UnifiedDownload task)
        {
            task.GracefulCts?.Cancel();
            task.Status = UnifiedDownloadStatus.Canceled;
            task.Progress = 0;
            task.DownloadedBytes = 0;
        }

        public async Task RemoveDownloadEntry(UnifiedDownload selectedEntry)
        {
            var unifiedDownloadLogic = await GetUnifiedDownloadLogic(selectedEntry.PluginId);
            if (selectedEntry.Status == UnifiedDownloadStatus.Running)
            {
                tasksToRemove[$"{selectedEntry.PluginId}_{selectedEntry.GameId}"] = true;
                CancelTask(selectedEntry);
            }
            else
            {
                if (unifiedDownloadLogic != null)
                {
                    await unifiedDownloadLogic.OnRemoveDownloadEntry(selectedEntry);
                }

                selectedEntry.PropertyChanged -= DownloadTask_PropertyChanged;
                Downloads.Remove(selectedEntry);
            }
        }

        public async Task OpenDownloadPropertiesWindows(UnifiedDownload selectedEntry)
        {
            var unifiedDownloadLogic = await GetUnifiedDownloadLogic(selectedEntry.PluginId);
            unifiedDownloadLogic?.OpenDownloadPropertiesWindow(selectedEntry);
        }

        public void RemoveTask(UnifiedDownload downloadItem)
        {
            Downloads.Remove(downloadItem);
        }

        public void UpdateSources()
        {
            var sources = Downloads.Select(d => d.SourceName).Distinct().OrderBy(s => s);
            foreach (var src in sources)
            {
                if (!AllSources.Contains(src) && !src.IsNullOrEmpty())
                {
                    AllSources.Add(src);
                }
            }
        }
    }
}