using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using krrTools.UI;

namespace krrTools.Tools.Listener
{
    internal class HotkeyWindow : Window
    {
        private TextBox? HotkeyTextBox;
        public string Hotkey { get; private set; }

        public HotkeyWindow(string? currentHotkey)
        {
            Hotkey = currentHotkey ?? string.Empty;
            BuildUI();
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            if (HotkeyTextBox != null) HotkeyTextBox.Text = Hotkey;
        }

        private void OnLanguageChanged()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        Title = SharedUIComponents.IsChineseLanguage() ? "设置热键" : "Set Hotkey";
                        // update any labels/buttons by rebuilding
                        var dc = DataContext;
                        Content = null;
                        BuildUI();
                        DataContext = dc;
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"HotkeyWindow inner rebuild failed: {ex.Message}"); }
                }));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"HotkeyWindow OnLanguageChanged invoke failed: {ex.Message}"); }
        }

        private void BuildUI()
        {
            Title = "Set Hotkey";
            Width = 300;
            Height = 200;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tb = new TextBlock { Text = "Press your desired key combination:", Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(tb, 0);
            grid.Children.Add(tb);

            HotkeyTextBox = new TextBox
            {
                Height = 30,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 0, 10),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(HotkeyTextBox, 1);
            grid.Children.Add(HotkeyTextBox);

            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(sp, 2);

            var saveBtn = SharedUIComponents.CreateStandardButton("Save|保存");
            saveBtn.Background = System.Windows.Media.Brushes.LightBlue;
            saveBtn.Width = 80; // 设置固定宽度以保持按钮大小一致
            saveBtn.Margin = new Thickness(0, 0, 10, 0);
            saveBtn.Click += SaveButton_Click;
            var cancelBtn = SharedUIComponents.CreateStandardButton("Cancel|取消");
            cancelBtn.Background = System.Windows.Media.Brushes.LightGray;
            cancelBtn.Width = 80; // 设置固定宽度以保持按钮大小一致
            cancelBtn.Click += CancelButton_Click;

            sp.Children.Add(saveBtn);
            sp.Children.Add(cancelBtn);
            grid.Children.Add(sp);

            Content = grid;
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

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt)
                return;

            string hotkey = string.Empty;
            if (ctrl) hotkey += "Ctrl+";
            if (shift) hotkey += "Shift+";
            if (alt) hotkey += "Alt+";

            hotkey += key.ToString();

            Hotkey = hotkey;
            if (HotkeyTextBox != null) HotkeyTextBox.Text = hotkey;
        }

        protected override void OnClosed(EventArgs e)
        {
            SharedUIComponents.LanguageChanged -= OnLanguageChanged;
            base.OnClosed(e);
        }
    }
}
