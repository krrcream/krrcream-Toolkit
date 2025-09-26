﻿// 在 Listener 文件夹中创建 GlobalHotkey.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace krrTools.tools.Listener
{
    public class GlobalHotkey
    {
        private int _id;
        private IntPtr _hwnd;
        private int _fsModifiers;
        private int _vk;
        private Action _action;
        private HwndSource? _source;

        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public GlobalHotkey(string hotkey, Action action, System.Windows.Window window)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            ParseHotkey(hotkey);
            var helper = new WindowInteropHelper(window ?? throw new ArgumentNullException(nameof(window)));
            _hwnd = helper.Handle;
            if (_hwnd == IntPtr.Zero) throw new InvalidOperationException("Window handle is invalid");

            // Use instance-based id to minimize collisions
            _id = Environment.TickCount & 0x7FFFFFFF;

            var success = RegisterHotKey(_hwnd, _id, _fsModifiers, _vk);
            if (!success)
            {
                var err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to register hotkey. Error code: {err}");
            }

            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(WndProc);
        }

        private void ParseHotkey(string hotkey)
        {
            _fsModifiers = 0;
            _vk = 0;
            if (string.IsNullOrWhiteSpace(hotkey)) return;

            var parts = hotkey.Split(['+'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
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
                        // Try parse key safely
                        if (Enum.TryParse<Key>(trimmed, true, out var key))
                        {
                            _vk = KeyInterop.VirtualKeyFromKey(key);
                        }
                        else
                        {
                            Debug.WriteLine($"GlobalHotkey: unrecognized key part '{trimmed}' in '{hotkey}'");
                        }
                        break;
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            {
                try { _action.Invoke(); } catch (Exception ex) { Debug.WriteLine($"GlobalHotkey action failed: {ex.Message}"); }
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
                Debug.WriteLine($"GlobalHotkey failed removing hook: {ex.Message}");
            }

            try
            {
                if (_hwnd != IntPtr.Zero)
                {
                    UnregisterHotKey(_hwnd, _id);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GlobalHotkey UnregisterHotKey failed: {ex.Message}");
            }
        }
    }
}
