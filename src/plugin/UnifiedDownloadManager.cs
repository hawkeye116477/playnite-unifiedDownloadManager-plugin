using CommonPlugin;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManager.Models;

namespace UnifiedDownloadManager
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

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            // Add code to be executed when game is started running.
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            // Add code to be executed when game is preparing to be started.
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new UnifiedDownloadManagerSettingsView();
        }
    }
}