using System;
using System.Windows;
using System.Windows.Input;

namespace krrTools.tools.Listener
{
    public partial class HotkeyWindow : Window
    {
        public string Hotkey { get; private set; }

        public HotkeyWindow(string currentHotkey)
        {
            InitializeComponent();
            Hotkey = currentHotkey;
            HotkeyTextBox.Text = currentHotkey;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            
            // 获取修饰键
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            
            // 获取主键
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            
            // 忽略修饰键本身
            if (key == Key.LeftCtrl || key == Key.RightCtrl || 
                key == Key.LeftShift || key == Key.RightShift || 
                key == Key.LeftAlt || key == Key.RightAlt)
                return;
            
            // 构造热键字符串
            string hotkey = "";
            if (ctrl) hotkey += "Ctrl+";
            if (shift) hotkey += "Shift+";
            if (alt) hotkey += "Alt+";
            
            hotkey += key.ToString();
            
            Hotkey = hotkey;
            HotkeyTextBox.Text = hotkey;
        }
    }
}