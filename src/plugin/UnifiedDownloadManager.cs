using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerNS.Models;
using CommonPlugin.Enums;
using System.Linq;
using System.Windows;

namespace UnifiedDownloadManagerNS
{
    public class UnifiedDownloadManager : GenericPlugin, IUnifiedDownloadManager
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private UnifiedDownloadManagerSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = UnifiedDownloadManagerSharedProperties.Id;
        public static UnifiedDownloadManager Instance { get; set; }

        private MainPanel DownloadManagerPanel;
        private SidebarItem downloadManagerSidebarItem;
        public IUnifiedTaskManager Manager { get; set; }
        public UnifiedDownloadManagerData UnifiedDownloadManagerData { get; set; }
        public string pluginName = "Unified Download Manager";
        public CommonHelpers commonHelpers { get; set; }

        public UnifiedDownloadManager(IPlayniteAPI api) : base(api)
        {
            Instance = this;
            settings = new UnifiedDownloadManagerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            Load3pLocalization();
            commonHelpers = new CommonHelpers(Instance);
            commonHelpers.LoadNeededResources(icons: false);
            Manager = new TaskManager();
  
            DownloadManagerPanel = new MainPanel((TaskManager)Manager);
            var savedTasks = LoadSavedManagerData();
            UnifiedDownloadManagerData = savedTasks;
            Manager.Downloads = UnifiedDownloadManagerData.downloads;
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\icon.png");

        public UnifiedDownloadManagerData LoadSavedManagerData()
        {
            UnifiedDownloadManagerData downloadManagerData = new UnifiedDownloadManagerData
            {
                downloads = new ObservableCollection<UnifiedDownload>()
            };
            var dataDir = UnifiedDownloadManager.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "unifiedDownloads.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out downloadManagerData))
                {
                    if (downloadManagerData != null && downloadManagerData != null)
                    {
                        correctJson = true;
                    }
                }
            }
            if (!correctJson)
            {
                downloadManagerData = new UnifiedDownloadManagerData
                {
                    downloads = new ObservableCollection<UnifiedDownload>()
                };
            }
            return downloadManagerData;
        }

        public void SaveManagerData()
        {
            var strConf = Serialization.ToJson(UnifiedDownloadManagerData, true);
            if (!strConf.IsNullOrEmpty())
            {
                var path = Path.Combine(GetPluginUserDataPath());
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                var dataFile = Path.Combine(path, $"unifiedDownloads.json");
                File.WriteAllText(dataFile, strConf);
            }
        }

        public void Load3pLocalization()
        {
            var currentLanguage = PlayniteApi.ApplicationSettings.Language;
            LocalizationManager.Instance.SetLanguage(currentLanguage);
            var commonFluentArgs = new Dictionary<string, IFluentType>
            {
                { "pluginShortName", (FluentString)"Unified Download Manager" },
            };
            LocalizationManager.Instance.SetCommonArgs(commonFluentArgs);
        }

        public static SidebarItem GetPanel()
        {
            if (Instance.downloadManagerSidebarItem == null)
            {
                Instance.downloadManagerSidebarItem = new SidebarItem
                {
                    Title = Instance.pluginName,
                    Icon = GetSidebarIcon(),
                    Type = SiderbarItemType.View,
                    Opened = () => GetDownloadManagerPanel(),
                    ProgressValue = 0,
                    ProgressMaximum = 100,
                };
            }
            return Instance.downloadManagerSidebarItem;
        }

        public static MainPanel GetDownloadManagerPanel()
        {
            return Instance.DownloadManagerPanel;
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return downloadManagerSidebarItem;
        }

        public static TextBlock GetSidebarIcon()
        {
            var textBlock = new TextBlock
            {
                Text = char.ConvertFromUtf32(0xef08),
                FontSize = 18
            };

            var font = ResourceProvider.GetResource("FontIcoFont") as System.Windows.Media.FontFamily;
            textBlock.FontFamily = font ?? new System.Windows.Media.FontFamily("Segoe UI Symbol");
            return textBlock;
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override async void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            if (Manager is TaskManager fullTaskManager)
            {
                await fullTaskManager.PauseAllTasks();
            }
            bool downloadsChanged = false;
            bool settingsChanged = false;
            var settings = GetSettings();
            if (settings != null)
            {
                if (settings.AutoRemoveCompletedDownloads != ClearCacheTime.Never)
                {
                    var nextRemovingCompletedDownloadsTime = settings.NextRemovingCompletedDownloadsTime;
                    if (nextRemovingCompletedDownloadsTime != 0)
                    {
                        DateTimeOffset now = DateTime.UtcNow;
                        if (now.ToUnixTimeSeconds() >= nextRemovingCompletedDownloadsTime)
                        {
                            foreach (var downloadItem in Manager.Downloads.ToList())
                            {
                                if (downloadItem.status == UnifiedDownloadStatus.Completed)
                                {
                                    Manager.Downloads.Remove(downloadItem);
                                    downloadsChanged = true;
                                }
                            }
                            settings.NextRemovingCompletedDownloadsTime = GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                            settingsChanged = true;
                        }
                    }
                    else
                    {
                        settings.NextRemovingCompletedDownloadsTime = GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                        settingsChanged = true;
                    }
                }
            }
            if (settingsChanged)
            {
                SavePluginSettings(settings);
            }
            if (downloadsChanged)
            {
                SaveManagerData();
            }
        }


        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new UnifiedDownloadManagerSettingsView();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            if (PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                yield return new MainMenuItem
                {
                    Description = LocalizationManager.Instance.GetString(LOC.UdmDownloadManager),
                    MenuSection = $"@{Instance.pluginName}",
                    Icon = UnifiedDownloadManager.Icon,
                    Action = (args) =>
                    {
                        Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                        {
                            ShowMaximizeButton = true,
                        });
                        window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonPanel)}";
                        window.Content = GetDownloadManagerPanel();
                        window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                        window.SizeToContent = SizeToContent.WidthAndHeight;
                        window.ShowDialog();
                    }
                };
            }
        }

        public static long GetNextClearingTime(ClearCacheTime frequency)
        {
            DateTimeOffset? clearingTime = null;
            DateTimeOffset now = DateTime.UtcNow;
            switch (frequency)
            {
                case ClearCacheTime.Day:
                    clearingTime = now.AddDays(1);
                    break;
                case ClearCacheTime.Week:
                    clearingTime = now.AddDays(7);
                    break;
                case ClearCacheTime.Month:
                    clearingTime = now.AddMonths(1);
                    break;
                case ClearCacheTime.ThreeMonths:
                    clearingTime = now.AddMonths(3);
                    break;
                case ClearCacheTime.SixMonths:
                    clearingTime = now.AddMonths(6);
                    break;
                default:
                    break;
            }
            return clearingTime?.ToUnixTimeSeconds() ?? 0;
        }

        public static UnifiedDownloadManagerSettings GetSettings()
        {
            return Instance.settings?.Settings ?? null;
        }
    }
}