using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using UnifiedDownloadManagerApiNS.Models;
using UnifiedDownloadManagerNS.Converters;
using UnifiedDownloadManagerNS.Models;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace UnifiedDownloadManagerNS
{
    /// <summary>
    /// Logika interakcji dla klasy MainPanel.xaml
    /// </summary>
    public partial class MainPanel : UserControl
    {
        private readonly TaskManager _manager;
        public ObservableCollection<UnifiedDownloadStatus>? SelectedStatuses { get; set; } = [];
        public ObservableCollection<string>? SelectedSources { get; set; } = [];
        public IPlayniteApi PlayniteApi { get; set; }

        public MainPanel(TaskManager manager)
        {
            InitializeComponent();
            _manager = manager;
            DataContext = manager;
            PlayniteApi = UnifiedDownloadManager.PlayniteApi;
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
                                                                   .Where(i => i.Status != UnifiedDownloadStatus.Completed && i.Status != UnifiedDownloadStatus.Canceled)
                                                                   .ToList();
                if (cancelableDownloads.Count > 0)
                {
                    string messageText = LocalizationManager.Instance.GetString(LOC.UdmCancelDownloadConfirm, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)cancelableDownloads[0].Name, ["count"] = (FluentNumber)cancelableDownloads.Count });
                    var result = await PlayniteApi.Dialogs.ShowMessageAsync(messageText, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCancelLabel), MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        foreach (var cancelableDownload in cancelableDownloads)
                        {
                            var unifiedDownloadLogic = await _manager.GetUnifiedDownloadLogic(cancelableDownload.PluginId);
                            if (cancelableDownload.Status != UnifiedDownloadStatus.Running && unifiedDownloadLogic != null)
                            {
                                await unifiedDownloadLogic.OnCancelDownload(cancelableDownload);
                            }
                            _manager.CancelTask(cancelableDownload);
                        }
                    }
                }
            }
        }

        private void DownloadsDG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var allSelected = DownloadsDG.SelectedItems.Cast<UnifiedDownload>().ToList();
            _manager.UpdateSelectedItems(allSelected);
            if (DownloadsDG.SelectedIndex != -1)
            {
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
                _manager.CanResume = false;
                _manager.CanPause = false;
                _manager.CanCancel = false;
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
                var runningOrQueuedDownloads = DownloadsDG.SelectedItems.Cast<UnifiedDownload>().Where(i => i.Status == UnifiedDownloadStatus.Running || i.Status == UnifiedDownloadStatus.Queued).ToList();
                if (runningOrQueuedDownloads.Count > 0)
                {
                    foreach (var selectedRow in runningOrQueuedDownloads)
                    {
                        await _manager.PauseTask(selectedRow);
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
                UnifiedDownloadManager.Instance.SaveManagerData();
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
                var removableDownloads = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                                  .ToList();
                if (removableDownloads.Count > 0)
                {
                    string messageText = LocalizationManager.Instance.GetString(LOC.UdmRemoveEntryConfirm, new Dictionary<string, IFluentType> { ["entryName"] = (FluentString)removableDownloads[0].Name, ["count"] = (FluentNumber)removableDownloads.Count });
                    var result = await PlayniteApi.Dialogs.ShowMessageAsync(messageText, LocalizationManager.Instance.GetString(LOC.UdmRemoveEntry), MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        foreach (var selectedRow in removableDownloads)
                        {
                            await _manager.RemoveDownloadEntry(selectedRow);
                        }
                    }
                    UnifiedDownloadManager.Instance.SaveManagerData();
                }
            }
        }

        private async void RemoveCompletedDownloadsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.Items.Count > 0)
            {
                var result = await PlayniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.UdmRemoveCompletedDownloadsConfirm), LocalizationManager.Instance.GetString(LOC.UdmRemoveCompletedDownloads), MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var row in DownloadsDG.Items.Cast<UnifiedDownload>().ToList())
                    {
                        if (row.Status == UnifiedDownloadStatus.Completed)
                        {
                            await _manager.RemoveDownloadEntry(row);
                        }
                    }
                }
                UnifiedDownloadManager.Instance.SaveManagerData();
            }
        }

        private async void OpenDownloadDirectoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = DownloadsDG.SelectedItems[0] as UnifiedDownload;
            var fullInstallPath = selectedItem?.FullInstallPath;
            if (fullInstallPath != "" && Directory.Exists(fullInstallPath))
            {
                ProcessStarter.StartProcess(fullInstallPath);
            }
            else
            {
                await PlayniteApi.Dialogs.ShowErrorMessageAsync($"{fullInstallPath}\n{LocalizationManager.Instance.GetString(LOC.CommonPathNotExistsError)}");
            }
        }

        private async void ResumeDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var downloadsToResume = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                                 .Where(i => i.Status != UnifiedDownloadStatus.Completed
                                                                             && i.Status != UnifiedDownloadStatus.Running
                                                                             && i.Status != UnifiedDownloadStatus.Queued)
                                                                 .ToList();
                await _manager.ResumeTasks(downloadsToResume);
            }
        }

        private async void OpenPluginSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            await PlayniteApi.MainView.OpenPluginSettingsAsync(UnifiedDownloadManager.Id);
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                RemoveDownloadBtn_Click(sender, e);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.Home))
            {
                MoveEntries(EntryPosition.Top, true);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.Up))
            {
                MoveEntries(EntryPosition.Up, true);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.Down))
            {
                MoveEntries(EntryPosition.Down, true);
            }
            else if (Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.End))
            {
                MoveEntries(EntryPosition.Bottom, true);
            }
            else if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.P)
            {
                DownloadPropertiesBtn_Click(sender, e);
            }
            else if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && e.Key == Key.O)
            {
                OpenDownloadDirectoryBtn_Click(sender, e);
            }
        }

        private async void DownloadPropertiesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                if (DownloadsDG.SelectedItems[0] is UnifiedDownload selectedItem)
                {
                    await _manager.OpenDownloadPropertiesWindows(selectedItem);
                }
            }
        }

        private async void BackHl_Click(object sender, RoutedEventArgs e)
        {
            if (PlayniteApi.AppInfo.Mode == AppMode.Fullscreen)
            {
                Window.GetWindow(this)?.Close();
            }
            else
            {
                await PlayniteApi.MainView.SwitchToViewAsync("LibraryView");
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UnifiedDownloadManager.Instance.CommonHelpersInstance.SetControlBackground(this);
            FilterSP.Visibility = Visibility.Collapsed;
            FiltersSepSP.Visibility = FilterSP.Visibility;
            RightCol.Width = new GridLength(0, GridUnitType.Auto);
            StatusCBo.ItemsSource = Enum.GetValues<UnifiedDownloadStatus>();

            var columnSettings = UnifiedDownloadManager.Instance.UnifiedUISettings.columnsSettings;
            var savedColumns = columnSettings.columns;
            if (savedColumns != null)
            {
                foreach (var column in DownloadsDG.Columns)
                {
                    var thisColumnId = DataGridColumnExtensions.GetColumnId(column);
                    if (savedColumns.TryGetValue(thisColumnId, out var savedColumn))
                    {
                        if (savedColumn.hidden)
                        {
                            column.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            column.DisplayIndex = savedColumn.index;
                        }
                    }
                }
                if (columnSettings.horizontalScrolling)
                {
                    var targetColumnWidth = DataGridLength.Auto;
                    DownloadsDG.ColumnWidth = targetColumnWidth;
                    foreach (var column in DownloadsDG.Columns)
                    {
                        column.Width = targetColumnWidth;
                    }
                }
                if (columnSettings.columnsLocked)
                {
                    foreach (var column in DownloadsDG.Columns)
                    {
                        column.CanUserReorder = false;
                        column.CanUserResize = false;
                    }
                    DataGridColumnExtensions.SetIsLocked(DownloadsDG, true);
                }
            }
        }

        private void StatusChk_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: UnifiedDownloadStatus status } checkBox)
            {
                if (checkBox.IsChecked == true)
                {
                    if (SelectedStatuses != null && !SelectedStatuses.Contains(status))
                    {
                        SelectedStatuses.Add(status);
                    }
                }
                else
                {
                    SelectedStatuses?.Remove(status);
                }

                var converter = new DownloadStatusEnumToStringConverter();

                if (SelectedStatuses != null)
                {
                    var text = SelectedStatuses.Select(s => converter.Convert(s, null, null, null)?.ToString());
                    StatusTb.Text = string.Join(", ", text);
                }
            }

            ICollectionView downloadsView = CollectionViewSource.GetDefaultView(DownloadsDG.ItemsSource);
            downloadsView.Filter = DownloadsFilter;
        }

        private bool DownloadsFilter(object obj)
        {
            if (obj is not UnifiedDownload download)
            {
                return false;
            }
            if (SelectedSources == null && SelectedStatuses == null)
            {
                return false;
            }
            bool sourceContains = true;
            if (SelectedSources is { Count: > 0 })
            {
                sourceContains = SelectedSources.Contains(download.SourceName);
            }
            bool statusContains = true;
            if (SelectedStatuses is { Count: > 0 })
            {
                statusContains = SelectedStatuses.Contains(download.Status);
            }
            if (SelectedSources != null && SelectedStatuses != null && (SelectedSources.Count > 0 || SelectedStatuses.Count > 0))
            {
                FilterDownloadBtn.Content = "\uef29 " + LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteFilterActiveLabel);
                ClearFiltersBtn.IsEnabled = true;
            }
            else
            {
                FilterDownloadBtn.Content = "\uef29";
                ClearFiltersBtn.IsEnabled = false;
            }
            return sourceContains && statusContains;
        }

        private void StatusCBo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            comboBox.SelectedItem = null;
        }

        private void FilterDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (FilterSP.Visibility == Visibility.Visible)
            {
                FilterSP.Visibility = Visibility.Collapsed;
                RightCol.Width = new GridLength(0, GridUnitType.Auto);
            }
            else
            {
                FilterSP.Visibility = Visibility.Visible;
                RightCol.Width = new GridLength(1, GridUnitType.Star);
            }
            FiltersSepSP.Visibility = FilterSP.Visibility;
        }

        private void SourceChk_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: string source } checkBox)
            {
                if (checkBox.IsChecked == true)
                {
                    if (SelectedSources != null && !SelectedSources.Contains(source))
                    {
                        SelectedSources.Add(source);
                    }
                }
                else
                {
                    SelectedSources?.Remove(source);
                }

                if (SelectedSources != null)
                {
                    SourceTb.Text = string.Join(", ", SelectedSources);
                }
            }
            ICollectionView downloadsView = CollectionViewSource.GetDefaultView(DownloadsDG.ItemsSource);
            downloadsView.Filter = DownloadsFilter;
        }

        private void SourceCBo_DropDownOpened(object sender, EventArgs e)
        {
            _manager.UpdateSources();
        }

        private void ClearFiltersBtn_Click(object sender, RoutedEventArgs e)
        {
            SelectedSources?.Clear();
            SelectedStatuses?.Clear();
            StatusTb.Text = "";
            SourceTb.Text = "";
            ICollectionView downloadsView = CollectionViewSource.GetDefaultView(DownloadsDG.ItemsSource);
            downloadsView.Filter = DownloadsFilter;
            SourceCBo.Items.Refresh();
            StatusCBo.Items.Refresh();
        }

        private string GetColumnHeaderText(DataGridColumn column)
        {
            if (column.Header is string s)
            {
                return s;
            }

            if (column.Header is DockPanel g && g.Children[1] is TextBlock tb)
            {
                return tb.Text;
            }

            return string.Empty;
        }

        private void ColumnHeader_ContextMenuOpening(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? obj =
                  e.OriginalSource as DependencyObject;

            while (obj != null &&
                   !(obj is DataGridColumnHeader))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }

            var header = obj as DataGridColumnHeader;

            if (header == null)
            {
                return;
            }

            var menu = new ContextMenu();

            foreach (var column in DownloadsDG.Columns)
            {
                var item = new MenuItem
                {
                    Header = GetColumnHeaderText(column),
                    IsCheckable = true,
                    IsChecked = column.Visibility == Visibility.Visible,
                    StaysOpenOnClick = true,
                    Tag = column
                };
                item.Click += ColumnVisibility_Click;
                menu.Items.Add(item);
            }

            menu.Items.Add(new Separator());

            var restoreDefaultOption = new MenuItem
            {
                Header = LocalizationManager.Instance.GetString(LOC.UdmRestoreDefaultColumnSettings),
                IsCheckable = false,
                StaysOpenOnClick = false,
                Icon = new TextBlock
                {
                    Text = "\uefd1",
                    FontFamily = (FontFamily)Application.Current.FindResource("IcoFont")!
                }
            };
            restoreDefaultOption.Click += (sender, e) =>
            {
                var columnSettings = UnifiedDownloadManager.Instance.UnifiedUISettings.columnsSettings;
                if (columnSettings.columns != null)
                {
                    columnSettings.columns = null;
                }
                UnifiedDownloadManager.Instance.LayoutChanged = true;
                foreach (var column in DownloadsDG.Columns)
                {
                    column.DisplayIndex = DownloadsDG.Columns.IndexOf(column);
                    column.Visibility = Visibility.Visible;
                }
            };
            menu.Items.Add(restoreDefaultOption);

            var lockColumnsOption = new MenuItem
            {
                Header = LocalizationManager.Instance.GetString(LOC.UdmLockAllColumns),
                IsCheckable = false,
                StaysOpenOnClick = false,
                Icon = new TextBlock
                {
                    Text = "\uec61",
                    FontFamily = (FontFamily)Application.Current.FindResource("IcoFont")!
                },
            };
            if (!DownloadsDG.Columns[0].CanUserResize)
            {
                lockColumnsOption.Header = LocalizationManager.Instance.GetString(LOC.UdmUnlockAllColumns);
                lockColumnsOption.Icon = new TextBlock
                {
                    Text = "\uec8c",
                    FontFamily = (FontFamily)Application.Current.FindResource("IcoFont")!
                };
            }
            lockColumnsOption.Click += (sender, e) =>
            {
                var columnSettings = UnifiedDownloadManager.Instance.UnifiedUISettings.columnsSettings;
                bool targetCan = false || !DownloadsDG.Columns[0].CanUserResize;
                foreach (var column in DownloadsDG.Columns)
                {
                    column.CanUserReorder = targetCan;
                    column.CanUserResize = targetCan;
                }
                columnSettings.columnsLocked = !targetCan;
                DataGridColumnExtensions.SetIsLocked(DownloadsDG, columnSettings.columnsLocked);
                UnifiedDownloadManager.Instance.LayoutChanged = true;
            };
            menu.Items.Add(lockColumnsOption);

            var horizontalScrollingOption = new MenuItem
            {
                Header = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsFullscreenHorizontalScrolling),
                IsCheckable = true,
                StaysOpenOnClick = false,
                IsChecked = DownloadsDG.ColumnWidth == DataGridLength.Auto
            };
            horizontalScrollingOption.Click += (sender, e) =>
            {
                var columnSettings = UnifiedDownloadManager.Instance.UnifiedUISettings.columnsSettings;
                var targetColumnWidth = new DataGridLength(1, DataGridLengthUnitType.Star);
                if (horizontalScrollingOption.IsChecked)
                {
                    targetColumnWidth = DataGridLength.Auto;
                }
                DownloadsDG.ColumnWidth = targetColumnWidth;
                foreach (var column in DownloadsDG.Columns)
                {
                    column.Width = targetColumnWidth;
                }
                columnSettings.horizontalScrolling = horizontalScrollingOption.IsChecked;
                UnifiedDownloadManager.Instance.LayoutChanged = true;
            };
            menu.Items.Add(new Separator());
            menu.Items.Add(horizontalScrollingOption);
            header.ContextMenu = menu;
        }

        private void ColumnVisibility_Click(object sender, RoutedEventArgs e)
        {
            var columnSettings = UnifiedDownloadManager.Instance.UnifiedUISettings.columnsSettings;
            if (columnSettings.columns == null)
            {
                columnSettings.columns = new Dictionary<string, UnifiedColumn>();
            }
            var item = sender as MenuItem;

            if (item?.Tag is DataGridColumn column)
            {
                var thisColumnId = DataGridColumnExtensions.GetColumnId(column);
                if (!columnSettings.columns.TryGetValue(thisColumnId, out var savedColumn))
                {
                    savedColumn = new UnifiedColumn();
                    columnSettings.columns.Add(thisColumnId, savedColumn);
                }
                if (item.IsChecked)
                {
                    column.Visibility = Visibility.Visible;
                    if (savedColumn.hidden)
                    {
                        savedColumn.hidden = false;
                    }
                }
                else
                {
                    int visibleColumnsCount = DownloadsDG.Columns.Count(c => c.Visibility == Visibility.Visible);
                    if (visibleColumnsCount > 1)
                    {
                        column.Visibility = Visibility.Collapsed;
                        savedColumn.index = column.DisplayIndex;
                        savedColumn.hidden = true;
                    }
                    else
                    {
                        item.IsChecked = true;
                    }
                }
            }
            UnifiedDownloadManager.Instance.LayoutChanged = true;
        }

        private void DownloadsDG_ColumnDisplayIndexChanged(object sender, DataGridColumnEventArgs e)
        {
            var thisColumn = e.Column;
            var columnSettings = UnifiedDownloadManager.Instance.UnifiedUISettings.columnsSettings;
            if (columnSettings.columns == null)
            {
                columnSettings.columns = new Dictionary<string, UnifiedColumn>();
            }
            var thisColumnId = DataGridColumnExtensions.GetColumnId(thisColumn);
            if (!columnSettings.columns.TryGetValue(thisColumnId, out var savedColumn))
            {
                savedColumn = new UnifiedColumn();
                columnSettings.columns.Add(thisColumnId, savedColumn);
            }
            savedColumn.index = thisColumn.DisplayIndex;
            UnifiedDownloadManager.Instance.LayoutChanged = true;
        }
    }
}
