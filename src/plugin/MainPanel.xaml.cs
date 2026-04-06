using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnifiedDownloadManagerApiNS;

namespace UnifiedDownloadManagerNS
{
    /// <summary>
    /// Logika interakcji dla klasy MainPanel.xaml
    /// </summary>
    public partial class MainPanel : UserControl
    {
        public SidebarItem downloadPanel = UnifiedDownloadManager.GetPanel();
        private readonly TaskManager _manager;
        private IPlayniteAPI playniteAPI = API.Instance;

        public MainPanel(TaskManager manager)
        {
            InitializeComponent();
            _manager = manager;
            DataContext = manager;
            SelectAllBtn.ToolTip = GetToolTipWithKey(LOC.UdmSelectAllEntries, "Ctrl+A");
            RemoveDownloadBtn.ToolTip = GetToolTipWithKey(LOC.UdmRemoveEntry, "Delete");
            MoveTopBtn.ToolTip = GetToolTipWithKey(LOC.UdmMoveEntryTop, "Alt+Home");
            MoveUpBtn.ToolTip = GetToolTipWithKey(LOC.UdmMoveEntryUp, "Alt+Up");
            MoveDownBtn.ToolTip = GetToolTipWithKey(LOC.UdmMoveEntryDown, "Alt+Down");
            MoveBottomBtn.ToolTip = GetToolTipWithKey(LOC.UdmMoveEntryBottom, "Alt+End");
            DownloadPropertiesBtn.ToolTip = GetToolTipWithKey(LOC.UdmEditSelectedDownloadProperties, "Ctrl+P");
            OpenDownloadDirectoryBtn.ToolTip = GetToolTipWithKey(LOC.UdmOpenDownloadDirectory, "Ctrl+O");
        }

        public string GetToolTipWithKey(string description, string shortcut)
        {
            return $"{LocalizationManager.Instance.GetString(description)} [{shortcut}]";
        }

        private async void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var cancelableDownloads = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                                   .Where(i => i.status != UnifiedDownloadStatus.Completed && i.status != UnifiedDownloadStatus.Canceled)
                                                                   .ToList();
                foreach (var cancelableDownload in cancelableDownloads)
                {
                    await _manager.CancelTask(cancelableDownload);
                }
                UnifiedDownloadManager.Instance.SaveManagerData();
            }
        }

        private void DownloadsDG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                ResumeDownloadBtn.IsEnabled = true;
                PauseBtn.IsEnabled = true;
                CancelDownloadBtn.IsEnabled = true;
                RemoveDownloadBtn.IsEnabled = true;
                MoveBottomBtn.IsEnabled = true;
                MoveDownBtn.IsEnabled = true;
                MoveTopBtn.IsEnabled = true;
                MoveUpBtn.IsEnabled = true;
                if (DownloadsDG.SelectedItems.Count == 1)
                {
                    DownloadPropertiesBtn.IsEnabled = true;
                    OpenDownloadDirectoryBtn.IsEnabled = true;
                }
                else
                {
                    DownloadPropertiesBtn.IsEnabled = false;
                    OpenDownloadDirectoryBtn.IsEnabled = false;
                }
            }
            else
            {
                ResumeDownloadBtn.IsEnabled = false;
                PauseBtn.IsEnabled = false;
                CancelDownloadBtn.IsEnabled = false;
                RemoveDownloadBtn.IsEnabled = false;
                DownloadPropertiesBtn.IsEnabled = false;
                OpenDownloadDirectoryBtn.IsEnabled = false;
                MoveBottomBtn.IsEnabled = false;
                MoveDownBtn.IsEnabled = false;
                MoveTopBtn.IsEnabled = false;
                MoveUpBtn.IsEnabled = false;
            }
        }

        private async void PauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var runningOrQueuedDownloads = DownloadsDG.SelectedItems.Cast<UnifiedDownload>().Where(i => i.status == UnifiedDownloadStatus.Running || i.status == UnifiedDownloadStatus.Queued).ToList();
                if (runningOrQueuedDownloads.Count > 0)
                {
                    foreach (var selectedRow in runningOrQueuedDownloads)
                    {
                        if (selectedRow.status == UnifiedDownloadStatus.Running)
                        {
                            await _manager.PauseTask(selectedRow);
                        }
                    }
                }
            }
        }

        private enum EntryPosition
        {
            Up,
            Down,
            Top,
            Bottom
        }

        private void MoveEntries(EntryPosition entryPosition, bool moveFocus = false)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var selectedIndexes = new List<int>();
                var allItems = DownloadsDG.Items;
                foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<UnifiedDownload>().ToList())
                {
                    var selectedIndex = allItems.IndexOf(selectedRow);
                    selectedIndexes.Add(selectedIndex);
                }
                selectedIndexes.Sort();
                if (entryPosition == EntryPosition.Down || entryPosition == EntryPosition.Top)
                {
                    selectedIndexes.Reverse();
                }
                var lastIndex = _manager.Downloads.Count - 1;
                int loopIndex = 0;
                foreach (int selectedIndex in selectedIndexes)
                {
                    int newIndex = selectedIndex;
                    int newSelectedIndex = selectedIndex;
                    switch (entryPosition)
                    {
                        case EntryPosition.Up:
                            if (selectedIndex != 0)
                            {
                                newIndex = selectedIndex - 1;
                            }
                            else
                            {
                                return;
                            }
                            break;
                        case EntryPosition.Down:
                            if (selectedIndex != lastIndex)
                            {
                                newIndex = selectedIndex + 1;
                            }
                            else
                            {
                                return;
                            }
                            break;
                        case EntryPosition.Top:
                            newSelectedIndex += loopIndex;
                            newIndex = 0;
                            break;
                        case EntryPosition.Bottom:
                            newIndex = lastIndex;
                            newSelectedIndex -= loopIndex;
                            break;
                    }
                    _manager.Downloads.Move(newSelectedIndex, newIndex);
                    loopIndex++;
                }
                if (moveFocus)
                {
                    DownloadsDG.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                }
            }
        }

        private void MoveTopBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Top);
        }

        private void MoveUpBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Up);
        }

        private void MoveDownBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Down);
        }

        private void MoveBottomBtn_Click(object sender, RoutedEventArgs e)
        {
            MoveEntries(EntryPosition.Bottom);
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.Items.Count > 0)
            {
                DownloadsDG.SelectAll();
            }
        }

        private async void RemoveDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                string messageText;
                if (DownloadsDG.SelectedItems.Count == 1)
                {
                    var selectedRow = (UnifiedDownload)DownloadsDG.SelectedItem;
                    messageText = LocalizationManager.Instance.GetString(LOC.UdmRemoveEntryConfirm, new Dictionary<string, IFluentType> { ["entryName"] = (FluentString)selectedRow.name });
                }
                else
                {
                    messageText = LocalizationManager.Instance.GetString(LOC.UdmRemoveSelectedEntriesConfirm);
                }
                var result = playniteAPI.Dialogs.ShowMessage(messageText, LocalizationManager.Instance.GetString(LOC.UdmRemoveEntry), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var selectedRow in DownloadsDG.SelectedItems.Cast<UnifiedDownload>().ToList())
                    {
                        await _manager.RemoveDownloadEntry(selectedRow);
                    }
                }
                UnifiedDownloadManager.Instance.SaveManagerData();
            }
        }

        private async void RemoveCompletedDownloadsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.Items.Count > 0)
            {
                var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.UdmRemoveCompletedDownloadsConfirm), LocalizationManager.Instance.GetString(LOC.UdmRemoveCompletedDownloads), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var row in DownloadsDG.Items.Cast<UnifiedDownload>().ToList())
                    {
                        if (row.status == UnifiedDownloadStatus.Completed)
                        {
                            await _manager.RemoveDownloadEntry(row);
                        }
                    }
                }
                UnifiedDownloadManager.Instance.SaveManagerData();
            }
        }

        private void OpenDownloadDirectoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = DownloadsDG.SelectedItems[0] as UnifiedDownload;
            var fullInstallPath = selectedItem.fullInstallPath;
            if (fullInstallPath != "" && Directory.Exists(fullInstallPath))
            {
                ProcessStarter.StartProcess("explorer.exe", selectedItem.fullInstallPath);
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage($"{selectedItem.fullInstallPath}\n{LocalizationManager.Instance.GetString(LOC.UdmPathNotExistsError)}");
            }
        }

        private async void ResumeDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var downloadsToResume = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                                 .Where(i => i.status != UnifiedDownloadStatus.Completed
                                                                             && i.status != UnifiedDownloadStatus.Running
                                                                             && i.status != UnifiedDownloadStatus.Queued)
                                                                 .ToList();
                await _manager.EnqueueTasks(downloadsToResume, true);
            }
        }
    }
}
