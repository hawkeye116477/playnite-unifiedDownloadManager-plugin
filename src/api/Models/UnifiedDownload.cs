using System;
using System.Collections.Generic;
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
        private double _downloadSizeBytes;
        
        [ObservableProperty]
        private double _installSizeBytes;

        public long AddedTime { get; set; }

        [ObservableProperty]
        private long _completedTime;

        [ObservableProperty] 
        private UnifiedDownloadStatus _status;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private double _downloadedBytes;

        public required string PluginId { get; set; }
        public required string SourceName { get; set; }
        
        [JsonIgnore] 
        [ObservableProperty]
        /* This is extended description of status (ex. Verifying example file)*/
        private string _activity = "";

        [JsonIgnore]
        [ObservableProperty]
        private TimeSpan _elapsed;
        
        [JsonIgnore]
        [ObservableProperty]
        private TimeSpan _eta;

        [JsonIgnore]
        [ObservableProperty]
        private double _downloadSpeedBytes;
        
        [JsonIgnore]
        [ObservableProperty]
        private double _diskWriteSpeedBytes;
        
        [JsonIgnore]
        public CancellationTokenSource? GracefulCts { get; set; }
        [JsonIgnore]
        public CancellationTokenSource? ForcefulCts { get; set; }

        public bool AllowParallel { get; set; } = false;
    }
}