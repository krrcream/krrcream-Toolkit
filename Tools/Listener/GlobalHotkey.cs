using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.Listener
{
    public class GlobalHotkey
    {
        private const int MOD_ALT     = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT   = 0x0004;

        private readonly Action _action;
        private readonly IntPtr _hwnd;

        private static   int _nextId = 1;
        private readonly int _id;
        private          int _vk;
        private          int _fsModifiers;

        private HwndSource? _source;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public GlobalHotkey(string hotkey, Action action, Window window)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            ParseHotkey(hotkey);
            var helper = new WindowInteropHelper(window ?? throw new ArgumentNullException(nameof(window)));
            _hwnd = helper.Handle;
            if (_hwnd == IntPtr.Zero) throw new InvalidOperationException("Window handle is invalid");

            // Use unique id to avoid collisions
            _id = Interlocked.Increment(ref _nextId);

            Logger.WriteLine(LogLevel.Debug,
                             $"[GlobalHotkey] Attempting to register hotkey '{hotkey}' with id {_id}, modifiers {_fsModifiers}, vk {_vk}, hwnd {_hwnd}");

            bool success   = RegisterHotKey(_hwnd, _id, _fsModifiers, _vk);
            int  errorCode = Marshal.GetLastWin32Error();

            if (!success)
            {
                // If hotkey already registered, try to unregister it first
                if (errorCode == 1409) // ERROR_HOTKEY_ALREADY_REGISTERED
                {
                    Logger.WriteLine(LogLevel.Warning,
                                     $"[GlobalHotkey] Hotkey '{hotkey}' already registered, attempting to unregister first");
                    UnregisterHotKey(_hwnd, _id);

                    // Try registering again
                    success   = RegisterHotKey(_hwnd, _id, _fsModifiers, _vk);
                    errorCode = Marshal.GetLastWin32Error();
                }

                if (!success)
                {
                    Logger.WriteLine(LogLevel.Error,
                                     $"[GlobalHotkey] RegisterHotKey FAILED for '{hotkey}': success={success}, errorCode={errorCode}, hwnd={_hwnd}, id={_id}, modifiers={_fsModifiers}, vk={_vk}");
                    throw new InvalidOperationException($"Failed to register hotkey. Error code: {errorCode}");
                }
            }

            // Logger.WriteLine(LogLevel.Debug,
            //                  $"[GlobalHotkey] RegisterHotKey SUCCESS for '{hotkey}': hwnd={_hwnd}, id={_id}, modifiers={_fsModifiers}, vk={_vk}");

            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(WndProc);
        }

        private void ParseHotkey(string hotkey)
        {
            _fsModifiers = 0;
            _vk          = 0;
            if (string.IsNullOrWhiteSpace(hotkey)) return;

            string[] parts = hotkey.Split(['+'], StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in parts)
            {
                string trimmed = p.Trim();

                switch (trimmed.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        _fsModifiers |= MOD_CONTROL;
                        break;

                    case "SHIFT":
                        _fsModifiers |= MOD_SHIFT;
                        break;

                    case "ALT":
                        _fsModifiers |= MOD_ALT;
                        break;

                    default:
                        // 尝试解析键值
                        if (Enum.TryParse(trimmed, true, out Key key))
                        {
                            _vk = KeyInterop.VirtualKeyFromKey(key);

                            // Logger.WriteLine(LogLevel.Debug, $"[GlobalHotkey] Parsed key '{trimmed}' to vk {_vk}");
                        }
                        else
                        {
                            Logger.WriteLine(LogLevel.Warning,
                                             "[GlobalHotkey] GlobalHotkey: unrecognized key part '{0}' in '{1}'",
                                             trimmed, hotkey);
                        }

                        break;
                }
            }

            // Logger.WriteLine(LogLevel.Debug, $"[GlobalHotkey] Parsed hotkey '{hotkey}' to modifiers {_fsModifiers}, vk {_vk}");
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            {
                Logger.WriteLine(LogLevel.Information,
                                 $"[GlobalHotkey] HOTKEY TRIGGERED: id {_id}, modifiers {_fsModifiers}, vk {_vk}");

                try
                {
                    _action.Invoke();
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[GlobalHotkey] GlobalHotkey action failed: {0}", ex.Message);
                }

                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Unregister()
        {
            try
            {
                if (_source != null)
                {
                    _source.RemoveHook(WndProc);
                    _source = null;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[GlobalHotkey] GlobalHotkey failed removing hook: {0}", ex.Message);
            }

            try
            {
                if (_hwnd != IntPtr.Zero) UnregisterHotKey(_hwnd, _id);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[GlobalHotkey] GlobalHotkey UnregisterHotKey failed: {0}",
                                 ex.Message);
            }
        }
    }
}
