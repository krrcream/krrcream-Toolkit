using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using krrTools.Beatmaps;

namespace krrTools.Tools.KRRLVAnalysis
{
    public class KRRLVAnalysisItem : INotifyPropertyChanged, IDisposable
    {
        private OsuAnalysisResult? _result;

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

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                Result = null;
                PropertyChanged = null;
            }

            _disposed = true;
        }
    }
}
