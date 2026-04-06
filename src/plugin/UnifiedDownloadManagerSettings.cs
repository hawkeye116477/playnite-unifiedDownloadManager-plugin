using CommonPlugin.Enums;
using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using UnifiedDownloadManagerNS.Enums;

namespace UnifiedDownloadManagerNS
{
    public class UnifiedDownloadManagerSettings : ObservableObject
    {
        public bool DisplayDownloadTaskFinishedNotifications { get; set; } = true;
        public bool DisplayDownloadSpeedInBits { get; set; } = false;
        public DownloadCompleteAction DoActionAfterDownloadComplete { get; set; } = DownloadCompleteAction.Nothing;
        public ClearCacheTime AutoRemoveCompletedDownloads { get; set; } = ClearCacheTime.Never;
        public long NextRemovingCompletedDownloadsTime { get; set; } = 0;
    }

    public class UnifiedDownloadManagerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly UnifiedDownloadManager plugin;
        private UnifiedDownloadManagerSettings editingClone { get; set; }

        private UnifiedDownloadManagerSettings settings;
        public UnifiedDownloadManagerSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public UnifiedDownloadManagerSettingsViewModel(UnifiedDownloadManager plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<UnifiedDownloadManagerSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new UnifiedDownloadManagerSettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            if (editingClone.AutoRemoveCompletedDownloads != Settings.AutoRemoveCompletedDownloads)
            {
                if (Settings.AutoRemoveCompletedDownloads != ClearCacheTime.Never)
                {
                    Settings.NextRemovingCompletedDownloadsTime = UnifiedDownloadManager.GetNextClearingTime(Settings.AutoRemoveCompletedDownloads);
                }
                else
                {
                    Settings.NextRemovingCompletedDownloadsTime = 0;
                }
            }

            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}