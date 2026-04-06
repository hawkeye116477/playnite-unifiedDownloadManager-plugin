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

        public UnifiedDownloadManager(IPlayniteAPI api) : base(api)
        {
            Instance = this;
            settings = new UnifiedDownloadManagerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            Load3pLocalization();
            Manager = new TaskManager();
  
            DownloadManagerPanel = new MainPanel((TaskManager)Manager);
            var savedTasks = LoadSavedManagerData();
            UnifiedDownloadManagerData = savedTasks;
            Manager.Downloads = UnifiedDownloadManagerData.downloads;
        }

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
                { "launcherName", (FluentString)"Legendary" },
                { "pluginShortName", (FluentString)"Legendary" },
                { "originalPluginShortName", (FluentString)"Epic" },
                { "updatesSourceName", (FluentString)"Epic Games" }
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
                    Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png"),
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

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }


        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new UnifiedDownloadManagerSettingsView();
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