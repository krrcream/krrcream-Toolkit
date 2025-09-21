// 在 Listener 文件夹中创建 GlobalHotkey.cs
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public GlobalHotkey(string hotkey, Action action, System.Windows.Window window)
        {
            _action = action;
            ParseHotkey(hotkey);
            
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.Handle;
            
            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("Window handle is invalid");
            }
            
            _id = GetHashCode();
            
            // 注册热键并检查是否成功
            bool success = RegisterHotKey(_hwnd, _id, _fsModifiers, _vk);
            if (!success)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Failed to register hotkey. Error code: {errorCode}");
            }
    
            // 监听窗口消息
            HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        }

        private void ParseHotkey(string hotkey)
        {
            _fsModifiers = 0;
            _vk = 0;
            
            string[] parts = hotkey.Split('+');
            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();
                switch (trimmedPart.ToUpper())
                {
                    case "CTRL":
                        _fsModifiers |= 2; // MOD_CONTROL
                        break;
                    case "SHIFT":
                        _fsModifiers |= 4; // MOD_SHIFT
                        break;
                    case "ALT":
                        _fsModifiers |= 1; // MOD_ALT
                        break;
                    default:
                        Key key = (Key)Enum.Parse(typeof(Key), trimmedPart);
                        _vk = KeyInterop.VirtualKeyFromKey(key);
                        break;
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312 && wParam.ToInt32() == _id) // WM_HOTKEY
            {
                _action?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Unregister()
        {
            UnregisterHotKey(_hwnd, _id);
        }
    }
}
