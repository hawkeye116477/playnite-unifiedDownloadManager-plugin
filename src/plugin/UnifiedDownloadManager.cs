using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Media;
using CommonPlugin;
using CommonPlugin.Enums;
using Linguini.Shared.Types.Bundle;
using Playnite;
using PlayniteMod;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Interfaces;
using UnifiedDownloadManagerApiNS.Models;
using UnifiedDownloadManagerNS.Models;
using Fonts = Playnite.Fonts;

namespace UnifiedDownloadManagerNS
{
    public class UnifiedDownloadManager : Plugin
    {
        public UnifiedDownloadManagerSettings Settings { get; set; } = null!;
        public static readonly string Id = UnifiedDownloadManagerSharedProperties.Id;
        public static UnifiedDownloadManager Instance { get; set; } = null!;

        private MainPanel? downloadManagerPanel;
        private TaskManager Manager { get; set; } = null!;
        private UnifiedDownloadManagerData? UnifiedDownloadManagerData { get; set; }
        public const string PluginName = "Unified Download Manager";
        public CommonHelpers CommonHelpersInstance { get; set; } = null!;
        public static IPlayniteApi PlayniteApi { get; private set; } = null!;
        public UnifiedUISettings UnifiedUISettings { get; set; } = null!;
        public bool LayoutChanged { get; set; }
        private static readonly ILogger Logger = LogManager.GetLogger<UnifiedDownloadManager>();

        public override async Task InitializeAsync(InitializeArgs args)
        {
            Instance = this;
            PlayniteApi = args.Api;
            Settings = UnifiedDownloadManagerSettingsViewModel.LoadPluginSettings(PlayniteApi.UserDataDir);
            CommonHelpersInstance = new CommonHelpers(PlayniteApi);
            CommonHelpersInstance.LoadNeededResources();
            Load3PLocalization();
            Manager = new TaskManager();
            downloadManagerPanel = new MainPanel((TaskManager)Manager);
            UnifiedDownloadManagerData = LoadSavedManagerData();
            if (UnifiedDownloadManagerData?.downloads != null)
            {
                Manager.Downloads = UnifiedDownloadManagerData.downloads;
            }

            UnifiedUISettings = LoadUISettings();
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            @"Resources\icon.png");

        private static UnifiedDownloadManagerData? LoadSavedManagerData()
        {
            var downloadManagerData = new UnifiedDownloadManagerData
            {
                downloads = []
            };
            var dataDir = PlayniteApi.UserDataDir;
            var dataFile = Path.Combine(dataDir, "unifiedDownloads.json");
            var correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out downloadManagerData))
                {
                    if (downloadManagerData != null)
                    {
                        correctJson = true;
                    }
                }
            }

            if (!correctJson)
            {
                downloadManagerData = new UnifiedDownloadManagerData
                {
                    downloads = []
                };
            }

            return downloadManagerData;
        }

        public void SaveManagerData()
        {
            if (UnifiedDownloadManagerData != null)
            {
                var strConf = Serialization.ToJson(UnifiedDownloadManagerData, true);
                if (!strConf.IsNullOrEmpty())
                {
                    var path = PlayniteApi.UserDataDir;
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    var dataFile = Path.Combine(path, "unifiedDownloads.json");
                    File.WriteAllText(dataFile, strConf);
                }
            }
        }

        private UnifiedUISettings LoadUISettings()
        {
            UnifiedUISettings unifiedUISettings = new UnifiedUISettings();
            var dataDir = PlayniteApi.UserDataDir;
            var dataFile = Path.Combine(dataDir, "unifiedUISettings.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out UnifiedUISettings? newUnifiedUISettings))
                {
                    if (newUnifiedUISettings != null)
                    {
                        correctJson = true;
                        unifiedUISettings = newUnifiedUISettings;
                    }
                }
            }

            if (!correctJson)
            {
                unifiedUISettings = new UnifiedUISettings();
            }

            return unifiedUISettings;
        }

        private void SaveUISettings()
        {
            var strConf = Serialization.ToJson(UnifiedUISettings, true);
            if (!strConf.IsNullOrEmpty())
            {
                var path = Path.Combine(PlayniteApi.UserDataDir);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var dataFile = Path.Combine(path, "unifiedUISettings.json");
                File.WriteAllText(dataFile, strConf);
            }
        }

        private void Load3PLocalization()
        {
            var currentLanguage = PlayniteApi.Settings.Language;
            LocalizationManager.Instance.SetLanguage(currentLanguage);
            var commonFluentArgs = new Dictionary<string, IFluentType>
            {
                { "pluginShortName", (FluentString)"UDM" },
            };
            LocalizationManager.Instance.SetCommonArgs(commonFluentArgs);
        }

        //public static AppViewItem? GetPanel()
        //{
        //    Instance._downloadManagerSidebarItem = new BasicSidebarItem(UIIcon.FromFontIcon("ef08",
        //                                                                    Playnite.Fonts.IcoFont),
        //                                                                (async) => GetDownloadManagerPanel(),
        //                                                                tooltip: Instance.pluginName);
        //    return Instance._downloadManagerSidebarItem;
        //}

        public static MainPanel? GetDownloadManagerPanel()
        {
            return Instance.downloadManagerPanel;
        }

        public override AppViewItem? GetAppViewItem(GetAppViewItemsArgs args)
        {
            if (args.ViewId == "UDM.panel")
            {
                return new UnifiedDownloadManagerAppView();
            }

            return null;
        }


        public override ICollection<AppViewItemDescriptor> GetAppViewItemDescriptors(
            GetAppViewItemDescriptorsArgs args)
        {
            return
            [
                new AppViewItemDescriptor("UDM.panel",
                    "Unified Download Manager",
                    iconArgs => UIIcon.FromFontIcon("ef08", Fonts.IcoFont),
                    iconArgs => UIIcon.FromFontIcon("ef08", Fonts.IcoFont, new SolidColorBrush(Colors.DeepSkyBlue)))
            ];
        }

        public override async Task OnApplicationShutdownAsync(OnApplicationShutdownArgs args)
        {
            if (Manager is TaskManager fullTaskManager)
            {
                await fullTaskManager.PauseAllTasks();
            }

            bool downloadsChanged = false;
            bool settingsChanged = false;
            var settings = GetSettings();
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
                            if (downloadItem.Status == UnifiedDownloadStatus.Completed)
                            {
                                Manager.Downloads.Remove(downloadItem);
                                downloadsChanged = true;
                            }
                        }

                        settings.NextRemovingCompletedDownloadsTime =
                            GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                        settingsChanged = true;
                    }
                }
                else
                {
                    settings.NextRemovingCompletedDownloadsTime =
                        GetNextClearingTime(settings.AutoRemoveCompletedDownloads);
                    settingsChanged = true;
                }
            }


            if (settingsChanged)
            {
                SavePluginSettings(PlayniteApi.UserDataDir, settings);
            }

            if (downloadsChanged)
            {
                SaveManagerData();
            }

            if (LayoutChanged)
            {
                SaveUISettings();
            }
        }

        public override async Task<PluginSettingsHandler?> GetSettingsHandlerAsync(GetSettingsHandlerArgs args)
        {
            return new UnifiedDownloadManagerSettingsViewModel(this);
        }

        public void SavePluginSettings(string dataDir, UnifiedDownloadManagerSettings settings)
        {
            var settingsFile = Path.Combine(dataDir, "settings.json");
            FileSystem.WriteStringToFile(settingsFile, Serialization.ToJson(settings, true));
        }

        //public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        //{
        //    if (PlayniteApi.AppInfo.Mode == AppMode.Fullscreen)
        //    {
        //        yield return new MainMenuItem
        //        {
        //            Description = LocalizationManager.Instance.GetString(LOC.UdmDownloadManager),
        //            MenuSection = $"@{Instance.pluginName}",
        //            Icon = UnifiedDownloadManager.Icon,
        //            Action = (args) =>
        //                     {
        //                         Window window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
        //                         {
        //                             ShowMaximizeButton = true,
        //                         });
        //                         window.ResizeMode = ResizeMode.CanResize;
        //                         window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        //                         window.Title = UnifiedDownloadManager.Instance.pluginName;
        //                         window.Content = GetDownloadManagerPanel();
        //                         window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
        //                         window.ShowDialog();
        //                     }
        //        };
        //    }
        //}

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
            }

            return clearingTime?.ToUnixTimeSeconds() ?? 0;
        }

        public static UnifiedDownloadManagerSettings GetSettings()
        {
            return Instance.Settings;
        }

        public override async Task<object?> OnPluginCallRequestAsync(PluginCallRequestAsyncArgs args)
        {
            if (args.CallId == UnifiedDownloadManagerSharedProperties.GetApi)
            {
                return Manager;
            }

            return null;
        }

        public override async Task<CollectDiagnosticDataArgsAsyncResult?> CollectDiagnosticDataArgsAsync(CollectDiagnosticDataArgs args)
        {
            var logsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp",
                "Playnite", UnifiedDownloadManagerSharedProperties.Id, "Logs");
            try
            {
                if (Directory.Exists(logsPath))
                {
                    Directory.Delete(logsPath, true);
                }
            }
            catch (Exception)
            {
                // ignored
            }

            Directory.CreateDirectory(logsPath);
            var zipPath = Path.Combine(logsPath, $"{UnifiedDownloadManagerSharedProperties.Id}.zip");
            try
            {
                Directory.CreateDirectory(logsPath);
                var fvi = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
                await File.WriteAllTextAsync(Path.Combine(logsPath, "Readme.txt"),
                    $"To report a bug, please fill form at: \n" +
                    $"<https://github.com/hawkeye116477/playnite-unifiedDownloadManager-plugin/issues/new?assignees=&labels=bug&projects=&template=bugs.yml&pluginV={fvi.ProductVersion}&playniteV={PlayniteApi.AppInfo.ApplicationVersion}> \n" +
                    $"and attach generated zip file.");

                var pluginLogFiles = Directory.GetFiles(PlayniteApi.UserDataDir, "plugin*.log", SearchOption.TopDirectoryOnly);
                var playniteLogFiles = Directory.GetFiles(PlayniteApi.AppInfo.ConfigurationDirectory, "playnite*.log",
                    SearchOption.TopDirectoryOnly);
                var files = new List<string>();
                files.AddRange(pluginLogFiles);
                files.AddRange(playniteLogFiles);

                await using var zipArchive = await ZipFile.OpenAsync(zipPath, ZipArchiveMode.Update);
                foreach (var singleFile in files)
                {
                    await using var source = new FileStream(singleFile, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    await source.CopyToAsync(await zipArchive.CreateEntry(Path.GetFileName(singleFile)).OpenAsync());
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex);
            }

            var newResults = new CollectDiagnosticDataArgsAsyncResult
            {
                ResultFile = zipPath
            };
            return newResults;
        }
    }
}