using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace krrTools.Bindable
{
    /// <summary>
    /// Base class for reactive ViewModels using the custom Bindable system.
    /// Alternative to ReactiveUI's ReactiveObject.
    /// </summary>
    public abstract class ReactiveViewModelBase : INotifyPropertyChanged, IDisposable
    {
        protected readonly List<IDisposable> Disposables = new List<IDisposable>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Helper to set a property and raise notification, similar to ReactiveUI's RaiseAndSetIfChanged.
        /// </summary>
        protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingField, value)) return false;
            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void Dispose()
        {
            foreach (var d in Disposables) d.Dispose();
            Disposables.Clear();
        }
    }
}