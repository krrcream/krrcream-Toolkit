using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using krrTools.Configuration;
using krrTools.Core;
using Microsoft.Extensions.Logging;

namespace krrTools.Bindable
{
    /// <summary>
    /// 设置更改事件参数
    /// Reactive options wrapper that provides automatic persistence for bindable options.
    /// Replaces ObservableOptions with simplified Bindable&lt;T&gt; integration.
    /// </summary>
    /// <typeparam name="TOptions">设置类型, 应该使用 Bindable&lt;T&gt; 属性.</typeparam>
    public class ReactiveOptions<TOptions> : INotifyPropertyChanged, IDisposable where TOptions : ToolOptionsBase, new()
    {
        private readonly ConverterEnum _converter;
        private readonly IEventBus? _eventBus;
        private readonly Dictionary<string, object?> _oldValues = new Dictionary<string, object?>();

        public TOptions Options { get; private set; }

        public ReactiveOptions(ConverterEnum converter, IEventBus? eventBus = null)
        {
            _converter = converter;
            _eventBus = eventBus;
            Options = BaseOptionsManager.LoadOptions<TOptions>(converter) ?? new TOptions();

            // 初始化旧值缓存
            InitializeOldValues();

            // Listen to options PropertyChanged for auto-save
            if (Options is INotifyPropertyChanged notifyOptions)
                notifyOptions.PropertyChanged += OnOptionsPropertyChanged;

            // Listen to BaseOptionsManager settings changed to reload when options are saved externally
            BaseOptionsManager.SettingsChanged += OnExternalSettingsChanged;
            BaseOptionsManager.GlobalSettingsChanged += OnGlobalSettingsChanged;
        }

        /// <summary>
        /// 初始化旧值缓存
        /// </summary>
        private void InitializeOldValues()
        {
            PropertyInfo[] properties = typeof(TOptions).GetProperties();

            foreach (PropertyInfo property in properties)
            {
                if (property.PropertyType.IsGenericType &&
                    property.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                {
                    object? bindable = property.GetValue(Options);

                    if (bindable != null)
                    {
                        PropertyInfo? valueProperty = bindable.GetType().GetProperty("Value");
                        object? currentValue = valueProperty?.GetValue(bindable);
                        _oldValues[property.Name] = currentValue;
                    }
                }
            }
        }

        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Debug日志：记录所有转换工具设置的变化
            object? oldValue = _oldValues.GetValueOrDefault(e.PropertyName ?? "");
            object? newValue = Options.GetType().GetProperty(e.PropertyName ?? "")?.GetValue(Options);

            // 由于异步通知管线，此处很难生效
            // Logger.WriteLine(LogLevel.Debug, "[ReactiveOptions] Converter: {0}, Property: {1}, OldValue: {2}, NewValue: {3}",
            //                  _converter,
            //                  e.PropertyName ?? "null",
            //                  oldValue ?? "null",
            //                  newValue ?? "null");

            // Auto-save on property change
            BaseOptionsManager.SaveOptions(_converter, Options);

            // Forward PropertyChanged event so that listeners of ReactiveOptions instance can receive internal Options changes
            OnPropertyChanged(e.PropertyName);

            // Publish settings changed event if event bus is available (for compatibility with existing subscribers)
            if (_eventBus != null)
            {
                // 只在NewValue不为null时发布事件（设置通常不为null）
                if (newValue != null)
                {
                    var settingsEvent = new SettingsChangedEvent
                    {
                        PropertyName = e.PropertyName,
                        OldValue = oldValue,
                        NewValue = newValue,
                        SettingsType = typeof(TOptions)
                    };
                    _eventBus.Publish(settingsEvent);

                    // 更新旧值缓存
                    _oldValues[e.PropertyName ?? ""] = newValue;
                }
            }
        }

        private void OnExternalSettingsChanged(ConverterEnum changedConverter)
        {
            if (changedConverter == _converter)
            {
                var toolOptions = (ToolOptionsBase)Options;
                toolOptions.IsLoading = true;
                Options = BaseOptionsManager.LoadOptions<TOptions>(changedConverter) ?? new TOptions();

                // 暂时没用的
                // // Unsubscribe from old options
                // if (Options is INotifyPropertyChanged oldNotifyOptions)
                // {
                //     oldNotifyOptions.PropertyChanged -= OnOptionsPropertyChanged;
                // }

                // // Subscribe to new options
                // if (Options is INotifyPropertyChanged newNotifyOptions)
                // {
                //     newNotifyOptions.PropertyChanged += OnOptionsPropertyChanged;
                // }

                toolOptions.IsLoading = false;
            }
        }

        private void OnGlobalSettingsChanged()
        {
            // 重新加载选项以应用全局设置变化
            var toolOptions = (ToolOptionsBase)Options;
            toolOptions.IsLoading = true;
            Options = BaseOptionsManager.LoadOptions<TOptions>(_converter) ?? new TOptions();
            toolOptions.IsLoading = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (Options is INotifyPropertyChanged notifyOptions) notifyOptions.PropertyChanged -= OnOptionsPropertyChanged;

            BaseOptionsManager.SettingsChanged -= OnExternalSettingsChanged;
        }

        public static implicit operator TOptions(ReactiveOptions<TOptions> reactive)
        {
            return reactive.Options;
        }
    }

    public class SettingsChangedEvent
    {
        public string? Key { get; set; }
        public object? Value { get; set; }
        public string? PropertyName { get; set; }
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public Type? SettingsType { get; set; }
    }
}
