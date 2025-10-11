using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using System.Reflection;
using krrTools.Configuration;

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

        public TOptions Options { get; private set; }

        public ReactiveOptions(ConverterEnum converter, IEventBus? eventBus = null)
        {
            _converter = converter;
            _eventBus = eventBus;
            Options = BaseOptionsManager.LoadOptions<TOptions>(converter) ?? new TOptions();

            // Listen to options PropertyChanged for auto-save
            if (Options is INotifyPropertyChanged notifyOptions)
            {
                notifyOptions.PropertyChanged += OnOptionsPropertyChanged;
            }

            // Listen to BaseOptionsManager settings changed to reload when options are saved externally
            BaseOptionsManager.SettingsChanged += OnExternalSettingsChanged;
        }

        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Auto-save on property change
            BaseOptionsManager.SaveOptions(_converter, Options);

            // Forward PropertyChanged event so that listeners of ReactiveOptions instance can receive internal Options changes
            OnPropertyChanged(e.PropertyName);

            // Publish settings changed event if event bus is available (for compatibility with existing subscribers)
            if (_eventBus != null)
            {
                var settingsEvent = new SettingsChangedEvent
                {
                    PropertyName = e.PropertyName,
                    NewValue = Options.GetType().GetProperty(e.PropertyName ?? "")?.GetValue(Options),
                    SettingsType = typeof(TOptions)
                };
                _eventBus.Publish(settingsEvent);
            }
        }

        private void OnExternalSettingsChanged(ConverterEnum changedConverter)
        {
            if (changedConverter == _converter)
            {
                Options = BaseOptionsManager.LoadOptions<TOptions>(changedConverter) ?? new TOptions();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (Options is INotifyPropertyChanged notifyOptions)
            {
                notifyOptions.PropertyChanged -= OnOptionsPropertyChanged;
            }
            
            BaseOptionsManager.SettingsChanged -= OnExternalSettingsChanged;
        }

        public static implicit operator TOptions(ReactiveOptions<TOptions> reactive)
        {
            return reactive.Options;
        }
    }
}
