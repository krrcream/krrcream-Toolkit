using System;
using System.ComponentModel;

namespace krrTools.Configuration
{
    // Provider that exposes get/set by enum key and notifies on changes
    public interface IEnumSettingsProvider : INotifyPropertyChanged
    {
        object? GetValue(Enum key);
        void SetValue(Enum key, object? value);
    }

    /// <summary>
    /// A small adapter that exposes get/set delegates as an IEnumSettingsProvider.
    /// Useful to adapt existing config systems which index settings by enum keys.
    /// </summary>
    public class EnumSettingsProviderDelegate(Func<Enum, object?> getter, Action<Enum, object?> setter)
        : IEnumSettingsProvider
    {
        private readonly Func<Enum, object?> _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        private readonly Action<Enum, object?> _setter = setter ?? throw new ArgumentNullException(nameof(setter));

        public object? GetValue(Enum key) => _getter(key);

        public void SetValue(Enum key, object? value)
        {
            _setter(key, value);
            OnPropertyChanged(key.ToString());
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// If external systems change values, call this to notify listeners.
        /// </summary>
        public void NotifyChanged(Enum key) => OnPropertyChanged(key.ToString());
    }
}