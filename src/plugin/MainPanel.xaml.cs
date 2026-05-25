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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Models;
using UnifiedDownloadManagerNS.Converters;

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
        public ObservableCollection<UnifiedDownloadStatus> SelectedStatuses { get; set; } = new ObservableCollection<UnifiedDownloadStatus>();
        public ObservableCollection<string> SelectedSources = new ObservableCollection<string>();

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

        public async Task HandleControllerInput(ControllerInput button)
        {
            var comboBoxFocused = Keyboard.FocusedElement as ComboBox;
            var comboBoxItemFocused = Keyboard.FocusedElement as ComboBoxItem;
            switch (button)
            {
                case ControllerInput.LeftShoulder:
                    FocusFirstEnabledButton();
                    break;
                case ControllerInput.RightShoulder:
                    FocusLastEnabledButton();
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
                    if (comboBoxFocused != null && comboBoxFocused.IsDropDownOpen == false)
                    {
                        comboBoxFocused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
                    }
                    break;
                case ControllerInput.DPadDown:
                case ControllerInput.LeftStickDown:
                    if (comboBoxFocused != null && comboBoxFocused.IsDropDownOpen == false)
                    {
                        comboBoxFocused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    }
                    break;
                case ControllerInput.RightStick:
                    OpenFiltersPanel();
                    break;
                case ControllerInput.Start:
                    EditSelectedEntry();
                    break;
                case ControllerInput.Back:
                    SelectAllEntries();
                    break;
                default:
                    break;
            }
        }
    }
}
