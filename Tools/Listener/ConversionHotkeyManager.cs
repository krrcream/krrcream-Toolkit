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
        private bool _hotkeysRegistered;

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
            if (_hotkeysRegistered) return; // 避免重复注册

            UnregisterAllHotkeys();

            Logger.WriteLine(LogLevel.Debug, "[ConversionHotkeyManager] Registering hotkeys: N2NC='{0}', DP='{1}', KRRLN='{2}'",
                settings.N2NCHotkey.Value, settings.DPHotkey.Value, settings.KRRLNHotkey.Value);

            RegisterHotkey(ConverterEnum.N2NC, settings.N2NCHotkey.Value);
            RegisterHotkey(ConverterEnum.DP, settings.DPHotkey.Value);
            RegisterHotkey(ConverterEnum.KRRLN, settings.KRRLNHotkey.Value);

            _hotkeysRegistered = true;
        }

        private void RegisterHotkey(ConverterEnum converter, string? hotkey)
        {
            if (string.IsNullOrEmpty(hotkey)) return;

            try
            {
                Logger.WriteLine(LogLevel.Debug, $"[ConversionHotkeyManager] Registering hotkey for {converter}: '{hotkey}'");
                var globalHotkey = new GlobalHotkey(hotkey, () => _convertAction(converter), _window);
                _hotkeys[converter] = globalHotkey;
                Logger.WriteLine(LogLevel.Debug, $"[ConversionHotkeyManager] Successfully registered hotkey for {converter}");
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
            _hotkeysRegistered = false;
        }

        /// <summary>
        /// 检查快捷键是否冲突
        /// </summary>
        public Dictionary<ConverterEnum, bool> CheckHotkeyConflicts(GlobalSettings settings)
        {
            var conflicts = new Dictionary<ConverterEnum, bool>();

            conflicts[ConverterEnum.N2NC] = CheckHotkeyConflict(settings.N2NCHotkey.Value);
            conflicts[ConverterEnum.DP] = CheckHotkeyConflict(settings.DPHotkey.Value);
            conflicts[ConverterEnum.KRRLN] = CheckHotkeyConflict(settings.KRRLNHotkey.Value);

            return conflicts;
        }

        private bool CheckHotkeyConflict(string? hotkey)
        {
            if (string.IsNullOrEmpty(hotkey)) return false;

            try
            {
                // 临时注册来检查冲突
                var tempHotkey = new GlobalHotkey(hotkey, () => { }, _window);
                tempHotkey.Unregister(); // 立即注销
                return false; // 没有冲突
            }
            catch
            {
                return true; // 注册失败，说明冲突
            }
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
        }
    }
}