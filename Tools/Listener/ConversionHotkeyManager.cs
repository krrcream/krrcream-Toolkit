using System.Windows.Input;
using krrTools.Configuration;
using Microsoft.Extensions.Logging;
using NHotkey.Wpf;

namespace krrTools.Tools.Listener
{
    /// <summary>
    /// 转换快捷键管理器
    /// </summary>
    public class ConversionHotkeyManager : IDisposable
    {
        private readonly Action<ConverterEnum> _convertAction;

        private bool _hotkeysRegistered;

        public ConversionHotkeyManager(Action<ConverterEnum> convertAction)
        {
            _convertAction = convertAction ?? throw new ArgumentNullException(nameof(convertAction));
        }

        /// <summary>
        /// 注册所有转换快捷键
        /// </summary>
        public void RegisterHotkeys(GlobalSettings settings)
        {
            if (_hotkeysRegistered) return; // 避免重复注册

            UnregisterAllHotkeys();

            Logger.WriteLine(LogLevel.Information,
                             "[ConversionHotkeyManager] Registering hotkeys: N2NC='{0}', DP='{1}', KRRLN='{2}'",
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
                if (ParseHotkey(hotkey, out Key key, out ModifierKeys modifiers))
                {
                    HotkeyManager.Current.AddOrReplace(converter.ToString(), key, modifiers, (sender, e) => _convertAction(converter));
                    Logger.WriteLine(LogLevel.Debug,
                                     $"[ConversionHotkeyManager] Successfully registered hotkey for {converter}");
                }
                else
                {
                    Logger.WriteLine(LogLevel.Warning,
                                     $"[ConversionHotkeyManager] Failed to parse hotkey '{hotkey}' for {converter}");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error,
                                 $"[ConversionHotkeyManager] Failed to register hotkey for {converter}: {ex.Message}");
            }
        }

        /// <summary>
        /// 注销所有快捷键
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            HotkeyManager.Current.Remove(nameof(ConverterEnum.N2NC));
            HotkeyManager.Current.Remove(nameof(ConverterEnum.DP));
            HotkeyManager.Current.Remove(nameof(ConverterEnum.KRRLN));
            _hotkeysRegistered = false;
        }

        private bool ParseHotkey(string hotkey, out Key key, out ModifierKeys modifiers)
        {
            key = Key.None;
            modifiers = ModifierKeys.None;
            if (string.IsNullOrWhiteSpace(hotkey)) return false;

            string[] parts = hotkey.Split(['+'], StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in parts)
            {
                string trimmed = p.Trim();

                switch (trimmed.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= ModifierKeys.Control;
                        break;

                    case "SHIFT":
                        modifiers |= ModifierKeys.Shift;
                        break;

                    case "ALT":
                        modifiers |= ModifierKeys.Alt;
                        break;

                    default:
                        // 尝试解析键值
                        if (Enum.TryParse(trimmed, true, out key))
                        {
                            // 成功
                        }
                        else
                        {
                            Logger.WriteLine(LogLevel.Warning,
                                             "[ConversionHotkeyManager] Unrecognized key part '{0}' in '{1}'",
                                             trimmed, hotkey);
                            return false;
                        }

                        break;
                }
            }

            return key != Key.None;
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
        }
    }
}
