using CommonPlugin.Enums;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Playnite;
using UnifiedDownloadManagerNS.Enums;

namespace UnifiedDownloadManagerNS
{
    public partial class UnifiedDownloadManagerSettings : ObservableObject
    {
        [ObservableProperty] private bool _displayDownloadTaskFinishedNotifications = true;

        [ObservableProperty] private bool _displayDownloadSpeedInBits;

        [ObservableProperty]
        private DownloadCompleteAction _doActionAfterDownloadComplete = DownloadCompleteAction.Nothing;

        [ObservableProperty] private ClearCacheTime _autoRemoveCompletedDownloads = ClearCacheTime.Never;

        [ObservableProperty] private long _nextRemovingCompletedDownloadsTime;
    }

    [INotifyPropertyChanged]
    public partial class UnifiedDownloadManagerSettingsViewModel(UnifiedDownloadManager plugin) : PluginSettingsHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        [ObservableProperty] private UnifiedDownloadManagerSettings _settings = new();

        public override UserControl GetEditView(GetSettingsViewArgs args)
        {
            return new UnifiedDownloadManagerSettingsView { DataContext = this };
        }

        public static UnifiedDownloadManagerSettings LoadPluginSettings(string dataDir)
        {
            UnifiedDownloadManagerSettings? settings = null;
            var settingsFile = Path.Combine(dataDir, "settings.json");
            if (File.Exists(settingsFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(settingsFile);
                if (!Serialization.TryFromJson(content, out UnifiedDownloadManagerSettings? newSettings))
                {
                    Logger.Error("Failed to load plugin settings.");
                }
                else
                {
                    settings = newSettings;
                }
            }
            return settings ?? new UnifiedDownloadManagerSettings();
        }

        public override async Task BeginEditAsync(BeginEditArgs args)
        {
            Settings = plugin.Settings.GetClone();
            await Task.CompletedTask;
        }

        public override async Task CancelEditAsync(CancelEditArgs args)
        {
            await Task.CompletedTask;
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            if (plugin.Settings.AutoRemoveCompletedDownloads != Settings.AutoRemoveCompletedDownloads)
            {
                if (Settings.AutoRemoveCompletedDownloads != ClearCacheTime.Never)
                {
                    Settings.NextRemovingCompletedDownloadsTime =
                        UnifiedDownloadManager.GetNextClearingTime(Settings.AutoRemoveCompletedDownloads);
                }
                else
                {
                    Settings.NextRemovingCompletedDownloadsTime = 0;
                }
            }
            plugin.Settings = Settings;
            plugin.SavePluginSettings(UnifiedDownloadManager.PlayniteApi.UserDataDir, Settings);
            await Task.CompletedTask;
        }
    }
}