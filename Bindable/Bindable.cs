using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace krrTools.Bindable
{
    /// <summary>
    /// A bindable value container that supports change notifications and binding to other bindables.
    /// Inspired by osu! framework's Bindable system.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class Bindable<T> : INotifyPropertyChanged
    {
        private T _value;
        private bool _disabled;
        private Func<T, T> _mapping;
        private Action<T> _onValueChanged;

        public T Value
        {
            get => _mapping != null ? _mapping(_value) : _value;
            set => Set(value);
        }

        public T RawValue => _value;

        public bool Disabled
        {
            get => _disabled;
            set
            {
                if (_disabled == value) return;
                _disabled = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public Bindable(T defaultValue = default)
        {
            _value = defaultValue;
        }

        protected virtual void Set(T value, [CallerMemberName] string propertyName = null)
        {
            if (_disabled) return;
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;

            var oldValue = _value;
            _value = value;

            _onValueChanged?.Invoke(_value);
            OnPropertyChanged(propertyName ?? nameof(Value));
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Bind this bindable to another bindable (one-way).
        /// </summary>
        public void BindTo(Bindable<T> other)
        {
            other.PropertyChanged += (_, _) => Value = other.Value;
        }

        /// <summary>
        /// Bind this bindable with another bindable (two-way).
        /// </summary>
        public void BindWith(Bindable<T> other)
        {
            // One-way from other to this
            other.PropertyChanged += (_, _) => Value = other.Value;
            // One-way from this to other
            this.PropertyChanged += (_, _) => other.Value = this.Value;
        }

        /// <summary>
        /// Bind with a mapping function.
        /// </summary>
        public Bindable<T> WithMapping(Func<T, T> mapping)
        {
            _mapping = mapping;
            return this;
        }

        /// <summary>
        /// Set a callback for value changes.
        /// </summary>
        public Bindable<T> OnValueChanged(Action<T> callback)
        {
            _onValueChanged = callback;
            return this;
        }

        /// <summary>
        /// Unbind from another bindable (simplified, assumes single binding).
        /// </summary>
        public void Unbind()
        {
            // In a full implementation, track bindings and remove them.
            // For simplicity, this is a placeholder.
        }
    }
}