using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace krrTools.Bindable
{
    /// <summary>
    /// A bindable value container that supports change notifications and binding to other bindables.
    /// Inspired by osu! framework's Bindable system.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class Bindable<T> : INotifyPropertyChanged
    {
        private bool _disabled;
        private Func<T, T>? _mapping;
        private readonly List<Action<T>> _onValueChangedCallbacks = new List<Action<T>>();
        private readonly List<Func<T, Task>> _onValueChangedAsyncCallbacks = new List<Func<T, Task>>();
        private bool _isNotifying; // 防递归标志
        private INotifyCollectionChanged? _collectionChanged;
        private T _previousValue; // 用于撤销功能

        public Bindable(T defaultValue = default!)
        {
            RawValue = defaultValue;
            _previousValue = defaultValue; // 初始化上一个值

            // 如果默认值是集合，监听其改变
            if (RawValue is INotifyCollectionChanged collection)
            {
                _collectionChanged = collection;
                _collectionChanged.CollectionChanged += OnCollectionChanged;
            }
        }

        public T Value
        {
            get => _mapping != null ? _mapping(RawValue) : RawValue;
            set => Set(value);
        }

        public T RawValue { get; private set; }

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
            if (EqualityComparer<T>.Default.Equals(RawValue, value)) return;

            // 保存上一个值用于撤销
            _previousValue = RawValue;

            // 移除旧值的监听
            if (_collectionChanged != null)
            {
                _collectionChanged.CollectionChanged -= OnCollectionChanged;
                _collectionChanged = null;
            }

            RawValue = value;

            // 如果新值是集合，监听其改变
            if (RawValue is INotifyCollectionChanged newCollection)
            {
                _collectionChanged = newCollection;
                _collectionChanged.CollectionChanged += OnCollectionChanged;
            }

            // 异步通知，避免阻塞
            _ = notifyValueChangedAsync(RawValue);

            // 调试测试绑定变化
            // Logger.WriteLine(LogLevel.Debug, $"[Bindable] Property '{propertyName}' changed to '{value}'");
            OnPropertyChanged(propertyName ?? nameof(Value));
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当集合内容改变时，触发 PropertyChanged 以更新 UI
            OnPropertyChanged(nameof(Value));
        }

        private async Task notifyValueChangedAsync(T value)
        {
            _isNotifying = true;

            try
            {
                // 同步回调
                foreach (Action<T> callback in _onValueChangedCallbacks) callback(value);

                // 异步回调
                foreach (Func<T, Task> callback in _onValueChangedAsyncCallbacks) await callback(value);
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
            PropertyChanged += (_, _) => other.Value = Value;
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
            _onValueChangedCallbacks.Add(callback);
            return this;
        }

        /// <summary>
        /// Set an async callback for value changes (recommended).
        /// </summary>
        public Bindable<T> OnValueChangedAsync(Func<T, Task> callback)
        {
            _onValueChangedAsyncCallbacks.Add(callback);
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

        /// <summary>
        /// Undo the last value change.
        /// </summary>
        public void Undo()
        {
            if (!EqualityComparer<T>.Default.Equals(_previousValue, default)) Value = _previousValue;
        }
    }
}
