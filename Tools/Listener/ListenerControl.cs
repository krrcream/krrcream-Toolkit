using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;
using krrTools.Utilities;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace krrTools.Tools.Listener
{
    public partial class ListenerControl
    {

        public RelayCommand BrowseCommand { get; }

        private readonly ListenerViewModel _viewModel;
        public ListenerViewModel ViewModel => _viewModel;

        private readonly FileDropZoneViewModel _dropZoneViewModel;
        public FileDropZoneViewModel DropZoneViewModel => _dropZoneViewModel;

        internal ListenerControl()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Constructor called");

            InitializeComponent();
            _viewModel = new ListenerViewModel();
            DataContext = _viewModel;

            // 初始化拖拽区 ViewModel
            var fileDispatcher = new FileDispatcher();
            _dropZoneViewModel = new FileDropZoneViewModel(fileDispatcher);
            
            BrowseCommand = new RelayCommand(() => _viewModel.SetSongsPathWindow());

            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;

            _viewModel.WindowTitle = Strings.OSUListener.Localize();

            // 添加快捷键编辑事件
            N2NCHotkeyTextBox.KeyDown += OnHotkeyKeyDown;
            N2NCHotkeyTextBox.GotFocus += OnHotkeyTextBoxGotFocus;
            N2NCHotkeyTextBox.LostFocus += OnHotkeyTextBoxLostFocus;

            DPHotkeyTextBox.KeyDown += OnHotkeyKeyDown;
            DPHotkeyTextBox.GotFocus += OnHotkeyTextBoxGotFocus;
            DPHotkeyTextBox.LostFocus += OnHotkeyTextBoxLostFocus;

            KRRLNHotkeyTextBox.KeyDown += OnHotkeyKeyDown;
            KRRLNHotkeyTextBox.GotFocus += OnHotkeyTextBoxGotFocus;
            KRRLNHotkeyTextBox.LostFocus += OnHotkeyTextBoxLostFocus;

            Loaded += (_, _) =>
            {
                Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Loaded event fired");
            };
            Unloaded += Window_Closing;
        }

        private void Window_Closing(object? sender, RoutedEventArgs e)
        {
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => { _viewModel.WindowTitle = Strings.OSUListener; }));
        }

        private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            // 阻止默认行为
            e.Handled = true;

            // 获取按键组合
            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            // 忽略单独的修饰键和输入法处理键
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.System || key == Key.ImeProcessed) return;

            // 构建快捷键字符串
            var hotkeyParts = new List<string>();

            if ((modifiers & ModifierKeys.Control) != 0) hotkeyParts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Shift) != 0) hotkeyParts.Add("Shift");
            if ((modifiers & ModifierKeys.Alt) != 0) hotkeyParts.Add("Alt");

            // 转换键为字符串
            var keyString = KeyToString(key);
            if (!string.IsNullOrEmpty(keyString))
            {
                hotkeyParts.Add(keyString);
            }

            var hotkey = string.Join("+", hotkeyParts);

            // 根据TextBox设置对应的快捷键
            if (textBox == N2NCHotkeyTextBox)
            {
                _viewModel.SetN2NCHotkey(hotkey);
                textBox.Text = hotkey;
            }
            else if (textBox == DPHotkeyTextBox)
            {
                _viewModel.SetDPHotkey(hotkey);
                textBox.Text = hotkey;
            }
            else if (textBox == KRRLNHotkeyTextBox)
            {
                _viewModel.SetKRRLNHotkey(hotkey);
                textBox.Text = hotkey;
            }

            Logger.WriteLine(LogLevel.Debug, $"[ListenerControl] Set hotkey to: {hotkey}");
        }

        private string KeyToString(Key key)
        {
            // 处理特殊键
            switch (key)
            {
                case Key.OemPlus: return "+";
                case Key.OemMinus: return "-";
                case Key.OemQuestion: return "/";
                case Key.OemPeriod: return ".";
                case Key.OemComma: return ",";
                case Key.OemSemicolon: return ";";
                case Key.OemQuotes: return "'";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemBackslash: return "\\";
                case Key.OemTilde: return "`";
                default: return key.ToString();
            }
        }

        private void OnHotkeyTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 当TextBox获得焦点时，禁用输入法以避免ImeProcessed问题
                InputMethod.SetIsInputMethodEnabled(textBox, false);

                Logger.WriteLine(LogLevel.Debug, $"[ListenerControl] Hotkey TextBox got focus: {textBox.Name}, IME disabled");
            }
        }

        private void OnHotkeyTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 当TextBox失去焦点时，重新启用输入法
                InputMethod.SetIsInputMethodEnabled(textBox, true);

                Logger.WriteLine(LogLevel.Debug, $"[ListenerControl] Hotkey TextBox lost focus: {textBox.Name}, IME re-enabled");
            }
        }

        private void TestN2NCButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.WriteLine(LogLevel.Information, "[ListenerControl] TEST BUTTON: Manual trigger N2NC conversion");
            // Conversion is now handled by MainWindow global hotkeys
        }
    }
}