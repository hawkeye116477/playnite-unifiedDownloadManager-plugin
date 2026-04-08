using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnifiedDownloadManagerApiNS
{
    public class UnifiedDownload : ObservableObject
    {
        public string gameID { get; set; }
        public string name { get; set; }
        public string fullInstallPath { get; set; }

        private double _downloadSizeBytes;
        public double downloadSizeBytes
        {
            get => _downloadSizeBytes;
            set => SetValue(ref _downloadSizeBytes, value);
        }

        private double _installSizeBytes;
        public double installSizeBytes
        {
            get => _installSizeBytes;
            set => SetValue(ref _installSizeBytes, value);
        }

        public long addedTime { get; set; }

        private long _completedTime;
        public long completedTime
        {
            get => _completedTime;
            set => SetValue(ref _completedTime, value);
        }

        private UnifiedDownloadStatus _status;
        public UnifiedDownloadStatus status
        {
            get => _status;
            set => SetValue(ref _status, value);
        }


        private double _progress;
        public double progress
        {
            get => _progress;
            set => SetValue(ref _progress, value);
        }

        private double _downloadedBytes;
        public double downloadedBytes
        {
            get => _downloadedBytes;
            set => SetValue(ref _downloadedBytes, value);
        }
        public string pluginId { get; set; }
        public string sourceName { get; set; }

        private string _activity;
        [DontSerialize]
        /* This is extended description of status (ex. Verifying example file)*/
        public string activity
        {
            get => _activity;
            set => SetValue(ref _activity, value);
        }

        private TimeSpan _elapsed;
        [DontSerialize]
        public TimeSpan elapsed
        {
            get => _elapsed;
            set => SetValue(ref _elapsed, value);
        }

        private TimeSpan _eta;
        [DontSerialize]
        public TimeSpan eta
        {
            get => _eta;
            set => SetValue(ref _eta, value);
        }

        private double _downloadSpeedBytes;
        [DontSerialize]
        public double downloadSpeedBytes
        {
            get => _downloadSpeedBytes;
            set => SetValue(ref _downloadSpeedBytes, value);
        }

        private double _diskWriteSpeedBytes;
        [DontSerialize]
        public double diskWriteSpeedBytes
        {
            get => _diskWriteSpeedBytes;
            set => SetValue(ref _diskWriteSpeedBytes, value);
        }

        [DontSerialize]
        public CancellationTokenSource gracefulCts { get; set; }
        [DontSerialize]
        public CancellationTokenSource forcefulCts { get; set; }
    }
}