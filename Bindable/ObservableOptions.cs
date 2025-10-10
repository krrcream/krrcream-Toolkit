using System;
using System.Collections.Generic;

namespace krrTools.Bindable
{
    /// <summary>
    /// Simplified observable options using dictionary, alternative to DynamicData.
    /// </summary>
    public class ObservableOptions
    {
        private readonly Dictionary<string, object> _options = new Dictionary<string, object>();
        private readonly IEventBus _eventBus;

        public ObservableOptions(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            return _options.TryGetValue(key, out var value) && value is T t ? t : defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            _options[key] = value;
            _eventBus.Publish(new SettingsChangedEvent { Key = key, Value = value });
        }

        public IEnumerable<string> Keys => _options.Keys;

        public void Clear() => _options.Clear();
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