using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using UnifiedDownloadManagerApiNS.Models;
using UnifiedDownloadManagerNS.Converters;
using UnifiedDownloadManagerNS.Models;

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
        private bool selectionMode;
        public ObservableCollection<UnifiedDownloadStatus> SelectedStatuses { get; set; } = new ObservableCollection<UnifiedDownloadStatus>();
        public ObservableCollection<string> SelectedSources = new ObservableCollection<string>();
        private int lastSelectedIndex = 0;
        private bool menuEnabled;

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

        private async Task CancelSelectedDownloads()
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var cancelableDownloads = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                                   .Where(i => i.status != UnifiedDownloadStatus.Completed && i.status != UnifiedDownloadStatus.Canceled)
                                                                   .ToList();
                if (cancelableDownloads.Count > 0)
                {
                    string messageText = LocalizationManager.Instance.GetString(LOC.UdmCancelDownloadConfirm, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)cancelableDownloads[0].name, ["count"] = (FluentNumber)cancelableDownloads.Count });
                    var result = MessageCheckBoxDialog.ShowMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteCancelLabel), messageText, null, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result.Result)
                    {
                        foreach (var cancelableDownload in cancelableDownloads)
                        {
                            var unifiedDownloadLogic = _manager.GetUnifiedDownloadLogic(cancelableDownload.pluginId);
                            if (cancelableDownload.status != UnifiedDownloadStatus.Running)
                            {
                                await unifiedDownloadLogic.OnCancelDownload(cancelableDownload);
                            }
                            _manager.CancelTask(cancelableDownload);
                        }
                    }
                }
            }
        }

        private async void CancelDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            await CancelSelectedDownloads();
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
                var runningOrQueuedDownloads = DownloadsDG.SelectedItems.Cast<UnifiedDownload>().Where(i => i.status == UnifiedDownloadStatus.Running || i.status == UnifiedDownloadStatus.Queued).ToList();
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

        private void SelectAllEntries()
        {
            if (DownloadsDG.Items.Count > 0)
            {
                if (DownloadsDG.SelectedItems.Count == DownloadsDG.Items.Count)
                {
                    DownloadsDG.UnselectAll();
                }
                else
                {
                    DownloadsDG.SelectAll();
                }
            }
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            SelectAllEntries();
        }

        private async void RemoveDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var removableDownloads = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                                  .ToList();
                if (removableDownloads.Count > 0)
                {
                    string messageText = LocalizationManager.Instance.GetString(LOC.UdmRemoveEntryConfirm, new Dictionary<string, IFluentType> { ["entryName"] = (FluentString)removableDownloads[0].name, ["count"] = (FluentNumber)removableDownloads.Count });
                    var result = MessageCheckBoxDialog.ShowMessage(LocalizationManager.Instance.GetString(LOC.UdmRemoveEntry), messageText, null, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result.Result)
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
                var result = MessageCheckBoxDialog.ShowMessage(LocalizationManager.Instance.GetString(LOC.UdmRemoveCompletedDownloads), LocalizationManager.Instance.GetString(LOC.UdmRemoveCompletedDownloadsConfirm), null, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result.Result)
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
                ProcessStarter.StartProcess(selectedItem.fullInstallPath);
            }
            else
            {
                playniteAPI.Dialogs.ShowErrorMessage($"{selectedItem.fullInstallPath}\n{LocalizationManager.Instance.GetString(LOC.CommonPathNotExistsError)}");
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
                await _manager.ResumeTasks(downloadsToResume);
            }
        }

        private void OpenPluginSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            playniteAPI.MainView.OpenPluginSettings(UnifiedDownloadManager.Instance.Id);
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

        private void EditSelectedEntry()
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var selectedItem = DownloadsDG.SelectedItems[0] as UnifiedDownload;
                _manager.OpenDownloadPropertiesWindows(selectedItem);
            }
        }
        private void DownloadPropertiesBtn_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedEntry();
        }

        private void BackHl_Click(object sender, RoutedEventArgs e)
        {
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                Window.GetWindow(this).Close();
            }
            else
            {
                playniteAPI.MainView.SwitchToLibraryView();
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CommonHelpers.SetControlBackground(this);
            FilterSP.Visibility = Visibility.Collapsed;
            FiltersSepSP.Visibility = FilterSP.Visibility;
            RightCol.Width = new GridLength(0, GridUnitType.Auto);
            StatusCBo.ItemsSource = Enum.GetValues(typeof(UnifiedDownloadStatus)).Cast<UnifiedDownloadStatus>();

            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                OpenPluginSettingsBtn.Visibility = Visibility.Collapsed;
                DownloadsDG.Focus();

                if (DownloadsDG.Items.Count > 0 && DownloadsDG.Columns.Count > 0)
                {
                    DownloadsDG.SelectedIndex = 0;
                    var item = DownloadsDG.Items[DownloadsDG.SelectedIndex];
                    DownloadsDG.CurrentCell = new DataGridCellInfo(item, DownloadsDG.Columns[0]);
                }
            }
            else
            {
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
                        DataGridColumnExtensions.SetIsLocked(DownloadsDG, trued=);
                    }
                }
            }
        }

        private void StatusChk_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is UnifiedDownloadStatus status)
            {
                if (checkBox.IsChecked == true)
                {
                    if (!SelectedStatuses.Contains(status))
                    {
                        SelectedStatuses.Add(status);
                    }
                }
                else
                {
                    SelectedStatuses.Remove(status);
                }

                var converter = new DownloadStatusEnumToStringConverter();

                var text = SelectedStatuses.Select(s => converter.Convert(s, null, null, null).ToString());
                StatusTb.Text = string.Join(", ", text);
            }

            ICollectionView downloadsView = CollectionViewSource.GetDefaultView(DownloadsDG.ItemsSource);
            downloadsView.Filter = DownloadsFilter;
        }

        private bool DownloadsFilter(object obj)
        {
            if (!(obj is UnifiedDownload download))
            {
                return false;
            }
            if (SelectedSources == null && SelectedStatuses == null)
            {
                return false;
            }
            bool sourceContains = true;
            if (SelectedSources.Count > 0)
            {
                sourceContains = SelectedSources.Contains(download.sourceName);
            }
            bool statusContains = true;
            if (SelectedStatuses.Count > 0)
            {
                statusContains = SelectedStatuses.Contains(download.status);
            }
            if (SelectedSources.Count > 0 || SelectedStatuses.Count > 0)
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

        private void OpenFiltersPanel()
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

        private void FilterDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFiltersPanel();
        }

        private void SourceChk_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is string source)
            {
                if (checkBox.IsChecked == true)
                {
                    if (!SelectedSources.Contains(source))
                    {
                        SelectedSources.Add(source);
                    }
                }
                else
                {
                    SelectedSources.Remove(source);
                }
                SourceTb.Text = string.Join(", ", SelectedSources);
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
            SelectedSources.Clear();
            SelectedStatuses.Clear();
            StatusTb.Text = "";
            SourceTb.Text = "";
            ICollectionView downloadsView = CollectionViewSource.GetDefaultView(DownloadsDG.ItemsSource);
            downloadsView.Filter = DownloadsFilter;
            SourceCBo.Items.Refresh();
            StatusCBo.Items.Refresh();
        }

        private void FocusFirstEnabledButton()
        {
            var firstEnabledBtn = LogicalTreeHelper.GetChildren(ButtonsSP).OfType<Button>().FirstOrDefault(b => b.IsEnabled);
            if (firstEnabledBtn != null)
            {
                firstEnabledBtn.Focus();
            }
        }

        private void FocusLastEnabledButton()
        {
            var lastEnabledBtn = ButtonsSP.Children.OfType<Button>().LastOrDefault(b => b.IsEnabled);
            if (lastEnabledBtn != null)
            {
                var last = LogicalTreeHelper.GetChildren(ButtonsSP).OfType<Button>()
                           .LastOrDefault(b => b.IsEnabled && b.IsVisible);

                Keyboard.Focus(last);
            }
        }

        private async Task PauseOrResumeSelectedEntries()
        {
            if (DownloadsDG.SelectedIndex != -1)
            {
                var downloadsToResume = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                                 .Where(i => i.status != UnifiedDownloadStatus.Completed
                                                                             && i.status != UnifiedDownloadStatus.Running
                                                                             && i.status != UnifiedDownloadStatus.Queued)
                                                                 .ToList();
                if (downloadsToResume.Count > 0)
                {
                    await _manager.ResumeTasks(downloadsToResume);
                }
                else
                {
                    var downloadsToPause = DownloadsDG.SelectedItems.Cast<UnifiedDownload>()
                                                 .Where(i => i.status == UnifiedDownloadStatus.Running
                                                             || i.status == UnifiedDownloadStatus.Queued)
                                                 .ToList();
                    if (downloadsToPause.Count > 0)
                    {
                        foreach (var downloadToPause in downloadsToPause)
                        {
                            await _manager.PauseTask(downloadToPause);
                        }
                    }
                }
            }
        }

        // Source: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-find-datatemplate-generated-elements
        private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem item)
                {
                    return item;
                }
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        private void HandleDataGridSelection(string direction)
        {
            var grid = DownloadsDG;
            if (grid == null || grid.Items.Count == 0)
            {
                return;
            }

            var currentItem = grid.CurrentItem ?? grid.SelectedItem;
            if (currentItem == null)
            {
                return;
            }

            int currentIndex = grid.Items.IndexOf(currentItem);
            int targetIndex = (direction == "Down") ? currentIndex + 1 : currentIndex - 1;

            if (targetIndex >= 0 && targetIndex < grid.Items.Count)
            {
                var targetItem = grid.Items[targetIndex];

                grid.CurrentItem = targetItem;

                if (grid.SelectedItems.Contains(targetItem))
                {
                    grid.SelectedItems.Remove(currentItem);
                }
                else
                {
                    grid.SelectedItems.Add(targetItem);
                }

                grid.ScrollIntoView(targetItem);
            }
        }

        public async Task HandleControllerInput(ControllerInput button, bool isHold)
        {
            var comboBoxFocused = Keyboard.FocusedElement as ComboBox;
            var comboBoxItemFocused = Keyboard.FocusedElement as ComboBoxItem;
            switch (button)
            {
                case ControllerInput.LeftShoulder:
                    FocusFirstEnabledButton();
                    lastSelectedIndex = DownloadsDG.SelectedIndex;
                    menuEnabled = true;
                    break;
                case ControllerInput.RightShoulder:
                    FocusLastEnabledButton();
                    lastSelectedIndex = DownloadsDG.SelectedIndex;
                    menuEnabled = true;
                    break;
                case ControllerInput.A:
                    if (Keyboard.FocusedElement is Button btn)
                    {
                        btn.RaiseEvent(
                            new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    else if (comboBoxItemFocused != null)
                    {
                        var chk = FindVisualChild<CheckBox>(comboBoxItemFocused);
                        if (chk != null)
                        {
                            chk.IsChecked = !chk.IsChecked;
                        }
                    }
                    else if (comboBoxFocused != null)
                    {
                        comboBoxFocused.IsDropDownOpen = true;
                    }
                    break;
                case ControllerInput.X:
                    await CancelSelectedDownloads();
                    break;
                case ControllerInput.Y:
                    await PauseOrResumeSelectedEntries();
                    break;
                case ControllerInput.B:
                    if (comboBoxFocused != null)
                    {
                        if (comboBoxFocused.IsDropDownOpen)
                        {
                            comboBoxFocused.IsDropDownOpen = false;
                            return;
                        }
                    }
                    if (comboBoxItemFocused != null)
                    {
                        var parentComboBox = ItemsControl.ItemsControlFromItemContainer(comboBoxItemFocused) as ComboBox;
                        parentComboBox.IsDropDownOpen = false;
                        return;
                    }
                    Window.GetWindow(this).Close();
                    break;
                case ControllerInput.DPadUp:
                case ControllerInput.LeftStickUp:
                    if (selectionMode)
                    {
                        HandleDataGridSelection("Up");
                        return;
                    }
                    if (comboBoxFocused != null && comboBoxFocused.IsDropDownOpen == false)
                    {
                        comboBoxFocused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
                    }
                    break;
                case ControllerInput.DPadDown:
                case ControllerInput.LeftStickDown:
                    if (selectionMode)
                    {
                        HandleDataGridSelection("Down");
                        return;
                    }
                    if (comboBoxFocused != null && comboBoxFocused.IsDropDownOpen == false)
                    {
                        comboBoxFocused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    };
                    if (menuEnabled && ((Keyboard.FocusedElement as DataGridCell) != null))
                    {
                        DataGridRow row = (DataGridRow)DownloadsDG.ItemContainerGenerator.ContainerFromIndex(lastSelectedIndex);
                        row.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                        menuEnabled = false;
                    }
                    break;
                case ControllerInput.RightStick:
                    if (FilterSP.Visibility == Visibility.Collapsed)
                    {
                        lastSelectedIndex = DownloadsDG.SelectedIndex;
                    }
                    OpenFiltersPanel();
                    if (FilterSP.Visibility == Visibility.Visible)
                    {
                        StatusCBo.Focus();
                    }
                    else
                    {
                        DataGridRow row = (DataGridRow)DownloadsDG.ItemContainerGenerator.ContainerFromIndex(lastSelectedIndex);
                        row.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                    }
                    break;
                case ControllerInput.Start:
                    EditSelectedEntry();
                    break;
                case ControllerInput.Back:
                    if (isHold)
                    {
                        selectionMode = false;
                        SelectAllEntries();
                    }
                    else
                    {
                        selectionMode = !selectionMode;
                    }
                    break;
                default:
                    break;
            }
        }

        private void DownloadsDG_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (selectionMode && (e.Key == Key.Up || e.Key == Key.Down))
            {
                e.Handled = true;
            }
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
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                return;
            }
            DependencyObject obj =
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
                    FontFamily = (FontFamily)Application.Current.FindResource("FontIcoFont")
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
                    FontFamily = (FontFamily)Application.Current.FindResource("FontIcoFont")
                },
            };
            if (!DownloadsDG.Columns[0].CanUserResize)
            {
                lockColumnsOption.Header = LocalizationManager.Instance.GetString(LOC.UdmUnlockAllColumns);
                lockColumnsOption.Icon = new TextBlock
                {
                    Text = "\uec8c",
                    FontFamily = (FontFamily)Application.Current.FindResource("FontIcoFont")
                };
            }
            lockColumnsOption.Click += (sender, e) =>
            {
                var columnSettings = UnifiedDownloadManager.Instance.UnifiedUISettings.columnsSettings;
                bool targetCan = false;
                if (!DownloadsDG.Columns[0].CanUserResize)
                {
                    targetCan = true;
                }
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
                    if (savedColumn != null && savedColumn.hidden)
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
                        if (savedColumn == null)
                        {
                            savedColumn = new UnifiedColumn();
                            columnSettings.columns.Add(thisColumnId, savedColumn);
                        }
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
