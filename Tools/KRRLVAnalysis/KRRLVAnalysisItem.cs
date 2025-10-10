using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace krrTools.Tools.KRRLVAnalysis
{
    public class KRRLVAnalysisItem : INotifyPropertyChanged
    {
        private string? _fileName;
        private string? _status;
        private string? _diff;
        private string? _title;
        private string? _artist;
        private string? _creator;
        private double _keys;
        private string? _bpm;
        private double _od;
        private double _hp;
        private double _lnPercent;
        private double _beatmapID;
        private double _beatmapSetID;
        private double _xxySR;
        private double _krrLV;
        private double _ylsLV;

        public string? FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string? FilePath { get; init; }

        public string? Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string? Diff
        {
            get => _diff;
            set => SetProperty(ref _diff, value);
        }

        public string? Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string? Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, value);
        }

        public string? Creator
        {
            get => _creator;
            set => SetProperty(ref _creator, value);
        }

        public double Keys
        {
            get => _keys;
            set => SetProperty(ref _keys, value);
        }

        public string? BPM
        {
            get => _bpm;
            set => SetProperty(ref _bpm, value);
        }

        public double OD
        {
            get => _od;
            set => SetProperty(ref _od, value);
        }

        public double HP
        {
            get => _hp;
            set => SetProperty(ref _hp, value);
        }

        public double LNPercent
        {
            get => _lnPercent;
            set => SetProperty(ref _lnPercent, value);
        }

        public double BeatmapID
        {
            get => _beatmapID;
            set => SetProperty(ref _beatmapID, value);
        }

        public double BeatmapSetID
        {
            get => _beatmapSetID;
            set => SetProperty(ref _beatmapSetID, value);
        }

        public double XxySR
        {
            get => _xxySR;
            set => SetProperty(ref _xxySR, value);
        }

        public double KrrLV
        {
            get => _krrLV;
            set => SetProperty(ref _krrLV, value);
        }

        public double YlsLV
        {
            get => _ylsLV;
            set => SetProperty(ref _ylsLV, value);
        }

        private double _notesCount;
        private double _maxKPS;
        private double _avgKPS;

        public double NotesCount
        {
            get => _notesCount;
            set => SetProperty(ref _notesCount, value);
        }

        public double MaxKPS
        {
            get => _maxKPS;
            set => SetProperty(ref _maxKPS, value);
        }

        public double AvgKPS
        {
            get => _avgKPS;
            set => SetProperty(ref _avgKPS, value);
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
    }
}