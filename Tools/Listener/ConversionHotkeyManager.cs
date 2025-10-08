using System;
using System.Collections.Generic;
using krrTools.Configuration;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.Listener
{
    /// <summary>
    /// 转换快捷键管理器
    /// </summary>
    public class ConversionHotkeyManager : IDisposable
    {
        private readonly Dictionary<ConverterEnum, GlobalHotkey?> _hotkeys = new();
        private readonly Action<ConverterEnum> _convertAction;
        private readonly System.Windows.Window _window;

        public ConversionHotkeyManager(Action<ConverterEnum> convertAction, System.Windows.Window window)
        {
            _convertAction = convertAction ?? throw new ArgumentNullException(nameof(convertAction));
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        /// <summary>
        /// 注册所有转换快捷键
        /// </summary>
        public void RegisterHotkeys(GlobalSettings settings)
        {
            UnregisterAllHotkeys();

            RegisterHotkey(ConverterEnum.N2NC, settings.N2NCHotkey);
            RegisterHotkey(ConverterEnum.DP, settings.DPHotkey);
            RegisterHotkey(ConverterEnum.KRRLN, settings.KRRLNHotkey);
        }

        private void RegisterHotkey(ConverterEnum converter, string? hotkey)
        {
            if (string.IsNullOrEmpty(hotkey)) return;

            try
            {
                var globalHotkey = new GlobalHotkey(hotkey, () => _convertAction(converter), _window);
                _hotkeys[converter] = globalHotkey;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, $"[ConversionHotkeyManager] Failed to register hotkey for {converter}: {ex.Message}");
            }
        }

        /// <summary>
        /// 注销所有快捷键
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            foreach (var hotkey in _hotkeys.Values)
            {
                hotkey?.Unregister();
            }
            _hotkeys.Clear();
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
        }
    }
}