using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace krrTools.Bindable
{
    /// <summary>
    /// A bindable value container that supports change notifications and binding to other bindables.
    /// Inspired by osu! framework's Bindable system.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class Bindable<T>(T defaultValue = default!) : INotifyPropertyChanged
    {
        private T _value = defaultValue;
        private bool _disabled;
        private Func<T, T>? _mapping;
        private Action<T>? _onValueChanged;
        private Func<T, Task>? _onValueChangedAsync;
        private bool _isNotifying; // 防递归标志

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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void Set(T value, [CallerMemberName] string? propertyName = null)
        {
            if (_disabled || _isNotifying) return; // 防递归
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;

            _value = value;

            // 异步通知，避免阻塞
            _ = NotifyValueChangedAsync(_value);
            OnPropertyChanged(propertyName ?? nameof(Value));
        }

        private async Task NotifyValueChangedAsync(T value)
        {
            _isNotifying = true;
            try
            {
                // 同步回调（保持兼容性，但不推荐）
                _onValueChanged?.Invoke(value);
                
                // 异步回调
                if (_onValueChangedAsync != null)
                {
                    await _onValueChangedAsync(value);
                }
            }
            finally
            {
                _isNotifying = false;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
        /// Set a callback for value changes (synchronous, may block).
        /// </summary>
        public Bindable<T> OnValueChanged(Action<T> callback)
        {
            _onValueChanged = callback;
            return this;
        }

        /// <summary>
        /// Set an async callback for value changes (recommended).
        /// </summary>
        public Bindable<T> OnValueChangedAsync(Func<T, Task> callback)
        {
            _onValueChangedAsync = callback;
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