using CommonPlugin.Enums;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommonPlugin;
using CommunityToolkit.Mvvm.ComponentModel;
using Playnite;
using UnifiedDownloadManagerNS.Enums;

namespace UnifiedDownloadManagerNS
{
    public partial class UnifiedDownloadManagerSettings : ObservableObject
    {
        [ObservableProperty]
        public partial bool DisplayDownloadTaskFinishedNotifications { get; set; } = true;

        [ObservableProperty]
        public partial bool DisplayDownloadSpeedInBits { get; set; }

        [ObservableProperty]
        public partial DownloadCompleteAction DoActionAfterDownloadComplete { get; set; } = DownloadCompleteAction.Nothing;

        [ObservableProperty]
        public partial ClearCacheTime AutoRemoveCompletedDownloads { get; set; } = ClearCacheTime.Never;

        [ObservableProperty]
        public partial long NextRemovingCompletedDownloadsTime { get; set; }
    }

    [INotifyPropertyChanged]
    public partial class UnifiedDownloadManagerSettingsViewModel(UnifiedDownloadManager plugin) : PluginSettingsHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        [ObservableProperty]
        public partial UnifiedDownloadManagerSettings Settings { get; set; } = new();

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
        }
    }
}