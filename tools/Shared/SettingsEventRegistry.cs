using System;
using System.Collections.Concurrent;

namespace krrTools.Tools.Shared
{
    /// <summary>
    /// Central registry for callbacks associated with enum-based settings keys.
    /// Allows different tools to register behavior to run when a specific option changes.
    /// </summary>
    public static class SettingsEventRegistry
    {
        private static readonly ConcurrentDictionary<string, Action<object?>> _callbacks = new();

        /// <summary>
        /// Register or replace a callback for the given key name (typically Enum.ToString()).
        /// </summary>
        public static void Register(string keyName, Action<object?> callback)
        {
            if (string.IsNullOrEmpty(keyName) || callback == null) return;
            _callbacks.AddOrUpdate(keyName, callback, (_, __) => callback);
        }

        /// <summary>
        /// Unregister a previously registered callback.
        /// </summary>
        public static void Unregister(string keyName)
        {
            if (string.IsNullOrEmpty(keyName)) return;
            _callbacks.TryRemove(keyName, out _);
        }

        /// <summary>
        /// Notify registered callback for this key if exists.
        /// </summary>
        public static void Notify(string keyName, object? value)
        {
            if (string.IsNullOrEmpty(keyName)) return;
            if (_callbacks.TryGetValue(keyName, out var cb))
            {
                cb(value);
            }
        }
    }
}
