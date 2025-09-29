using System.ComponentModel;
using krrTools.UI;

namespace krrTools.Localization;

public static class LocalizedStringHelper
{
    /// <summary>
    /// A class that provides localized strings with automatic updates when language changes.
    /// </summary>
    public class LocalizedString : INotifyPropertyChanged
    {
        private string _localizedText;

        public LocalizedString(string key)
        {
            Key = key;
            _localizedText = key.Localize();
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
        }

        public string Key { get; }

        public string Value
        {
            get => _localizedText;
            private set
            {
                if (_localizedText != value)
                {
                    _localizedText = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        private void OnLanguageChanged()
        {
            Value = Key.Localize();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static implicit operator string(LocalizedString ls) => ls.Value;

        ~LocalizedString()
        {
            SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }
    }
}