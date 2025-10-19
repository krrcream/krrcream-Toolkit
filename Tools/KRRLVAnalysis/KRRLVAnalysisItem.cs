using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using krrTools.Beatmaps;

namespace krrTools.Tools.KRRLVAnalysis
{
    public class KRRLVAnalysisItem : INotifyPropertyChanged
    {
        private OsuAnalysisResult? _result;
        // private string? _fileName;
        private string? _filePath;
        private string? _status;

        public KRRLVAnalysisItem()
        {
        }

        public KRRLVAnalysisItem(OsuAnalysisResult result, string fileName, string filePath)
        {
            _result = result;
            // _fileName = fileName;
            _filePath = filePath;
            _status = "√";
        }

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
                
                var property = typeof(OsuAnalysisResult).GetProperty(propertyName);
                return property?.GetValue(_result);
            }
        }

        // 为兼容性保留的常用属性代理
        public string? Title => _result?.Title;
        public string? Artist => _result?.Artist;
        public string? Diff => _result?.Diff;
        public string? Creator => _result?.Creator;
        public string? BPM => _result?.BPMDisplay;
        public double Keys => _result?.KeyCount ?? 0;
        public double OD => _result?.OD ?? 0;
        public double HP => _result?.HP ?? 0;
        public double LNPercent => _result?.LNPercent ?? 0;
        public double BeatmapID => _result?.BeatmapID ?? 0;
        public double BeatmapSetID => _result?.BeatmapSetID ?? 0;
        public double XxySR => _result?.XXY_SR ?? 0;
        public double KrrLV => _result?.KRR_LV ?? 0;
        public double YlsLV => _result?.YLs_LV ?? 0;
        public int NotesCount => _result?.NotesCount ?? 0;
        public double MaxKPS => _result?.MaxKPS ?? 0;
        public double AvgKPS => _result?.AvgKPS ?? 0;

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
    }
}