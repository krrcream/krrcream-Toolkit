using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using krrTools.Beatmaps;

namespace krrTools.Tools.KRRLVAnalysis
{
    public class KRRLVAnalysisItem : INotifyPropertyChanged, IDisposable
    {
        private OsuAnalysisResult? _result;
        // private string? _fileName;
        private string? _filePath;
        private string? _status;
        private bool _disposed;

        public OsuAnalysisResult? Result
        {
            get => _result;
            set
            {
                _result = value;
                OnPropertyChanged(string.Empty); // 通知所有属性变化
            }
        }

        // public string? FileName
        // {
        //     get => _fileName;
        //     set => SetProperty(ref _fileName, value);
        // }

        public string? FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string? Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        // 动态属性访问器，用于反射绑定
        public object? this[string propertyName]
        {
            get
            {
                if (_result == null) return null;

                PropertyInfo? property = typeof(OsuAnalysisResult).GetProperty(propertyName);
                return property?.GetValue(_result);
            }
        }

        // 为兼容性保留的常用属性代理
        public string? Title
        {
            get => _result?.Title;
        }
        public string? Artist
        {
            get => _result?.Artist;
        }
        public string? Diff
        {
            get => _result?.Diff;
        }
        public string? Creator
        {
            get => _result?.Creator;
        }
        public string? BPM
        {
            get => _result?.BPMDisplay;
        }
        public double Keys
        {
            get => _result?.KeyCount ?? 0;
        }
        public double OD
        {
            get => _result?.OD ?? 0;
        }
        public double HP
        {
            get => _result?.HP ?? 0;
        }
        public double LNPercent
        {
            get => _result?.LNPercent ?? 0;
        }
        public double BeatmapID
        {
            get => _result?.BeatmapID ?? 0;
        }
        public double BeatmapSetID
        {
            get => _result?.BeatmapSetID ?? 0;
        }
        public double XxySR
        {
            get => _result?.XXY_SR ?? 0;
        }
        public double KrrLV
        {
            get => _result?.KRR_LV ?? 0;
        }
        public double YlsLV
        {
            get => _result?.YLs_LV ?? 0;
        }
        public int NotesCount
        {
            get => _result?.NotesCount ?? 0;
        }
        public double MaxKPS
        {
            get => _result?.MaxKPS ?? 0;
        }
        public double AvgKPS
        {
            get => _result?.AvgKPS ?? 0;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            OnPropertyChanged(propertyName);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 清理托管资源
                _result = null;
                _filePath = null;
                _status = null;

                // 清理事件订阅
                PropertyChanged = null;
            }

            _disposed = true;
        }
    }
}
