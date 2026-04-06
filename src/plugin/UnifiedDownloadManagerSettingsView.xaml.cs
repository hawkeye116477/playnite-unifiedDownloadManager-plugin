using CommonPlugin;
using CommonPlugin.Enums;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using UnifiedDownloadManagerNS.Enums;

namespace UnifiedDownloadManagerNS
{
    /// <summary>
    /// Logika interakcji dla klasy UnifiedDownloadManagerSettingsView.xaml
    /// </summary>
    public partial class UnifiedDownloadManagerSettingsView : UserControl
    {
        public UnifiedDownloadManagerSettingsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var downloadCompleteActions = new Dictionary<DownloadCompleteAction, string>
            {
                { DownloadCompleteAction.Nothing, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteDoNothing) },
                { DownloadCompleteAction.ShutDown, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuShutdownSystem) },
                { DownloadCompleteAction.Reboot, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuRestartSystem) },
                { DownloadCompleteAction.Hibernate, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuHibernateSystem) },
                { DownloadCompleteAction.Sleep, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteMenuSuspendSystem) },
            };
            AfterDownloadCompleteCBo.ItemsSource = downloadCompleteActions;

            var autoClearOptions = new Dictionary<ClearCacheTime, string>
            {
                { ClearCacheTime.Day, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceADay) },
                { ClearCacheTime.Week, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOptionOnceAWeek) },
                { ClearCacheTime.Month, LocalizationManager.Instance.GetString(LOC.CommonOnceAMonth) },
                { ClearCacheTime.ThreeMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery3Months) },
                { ClearCacheTime.SixMonths, LocalizationManager.Instance.GetString(LOC.CommonOnceEvery6Months) },
                { ClearCacheTime.Never, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteSettingsPlaytimeImportModeNever) }
            };
            AutoRemoveCompletedDownloadsCBo.ItemsSource = autoClearOptions;
        }
    }
}
