using System.Collections.Generic;
using System.ComponentModel;

namespace krrTools.Tools.FilesManager
{
    public class EditableProperty<T> : INotifyPropertyChanged
    {
        private T _value = default!;
        private T _previousValue = default!;

        public event PropertyChangedEventHandler? PropertyChanged;

        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _previousValue = _value;
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        public void Undo()
        {
            if (!EqualityComparer<T>.Default.Equals(_previousValue, default))
            {
                _value = _previousValue;
                OnPropertyChanged(nameof(Value));
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
