using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using krrTools.Configuration;
using krrTools.Core;

namespace krrTools.Bindable
{
    /// <summary>
    /// Reactive module options wrapper that provides automatic persistence for module options.
    /// Similar to ReactiveOptions but for ModuleEnum instead of ConverterEnum.
    /// </summary>
    /// <typeparam name="TOptions">Options type, should inherit from ToolOptionsBase.</typeparam>
    public class ReactiveModuleOptions<TOptions> : INotifyPropertyChanged, IDisposable where TOptions : ToolOptionsBase, new()
    {
        private readonly ModuleEnum _module;
        private readonly IEventBus? _eventBus;

        public TOptions Options { get; private set; }

        public ReactiveModuleOptions(ModuleEnum module, IEventBus? eventBus = null)
        {
            _module = module;
            _eventBus = eventBus;
            Options = BaseOptionsManager.LoadModuleOptions<TOptions>(module) ?? new TOptions();

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
            BaseOptionsManager.SaveModuleOptions(_module, Options);

            // Forward PropertyChanged event so that listeners of ReactiveModuleOptions instance can receive internal Options changes
            OnPropertyChanged(e.PropertyName);

            // Publish settings changed event if event bus is available
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
            // For module options, we don't need to reload on converter changes
            // Module options are independent of converter settings
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

        public static implicit operator TOptions(ReactiveModuleOptions<TOptions> reactive)
        {
            return reactive.Options;
        }
    }
}