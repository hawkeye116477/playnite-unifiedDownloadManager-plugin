using System;
using System.Text.Json.Serialization;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UnifiedDownloadManagerApiNS.Models
{
    public partial class UnifiedDownload : ObservableObject
    {
        public required string GameId { get; set; }
        public required string Name { get; set; }
        public string FullInstallPath { get; set; } = "";

        [ObservableProperty]
        public partial double DownloadSizeBytes { get; set; }

        [ObservableProperty]
        private double installSizeBytes;

        public long AddedTime { get; set; }

        [ObservableProperty]
        public partial long CompletedTime { get; set; }

        [ObservableProperty]
        public partial UnifiedDownloadStatus Status { get; set; }

        [ObservableProperty]
        public partial double Progress { get; set; }

        [ObservableProperty]
        public partial double DownloadedBytes { get; set; }
        public required string PluginId { get; set; }
        public required string SourceName { get; set; }

        [field: JsonIgnore]
        [ObservableProperty]
        public partial string Activity { get; set; } = "";

        [field: JsonIgnore]
        [ObservableProperty]
        public partial TimeSpan Elapsed { get; set; }

        [JsonIgnore]
        [ObservableProperty]
        private TimeSpan eta;

        [field: JsonIgnore]
        [ObservableProperty]
        public partial double DownloadSpeedBytes { get; set; }

        [JsonIgnore]
        [ObservableProperty]
        private double diskWriteSpeedBytes;
        
        [JsonIgnore]
        public CancellationTokenSource? GracefulCts { get; set; }
        [JsonIgnore]
        public CancellationTokenSource? ForcefulCts { get; set; }

        public bool AllowParallel { get; set; } = false;
    }
}