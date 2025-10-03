using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.Listener
{
    internal class HotkeyWindow : Window
    {
        private Wpf.Ui.Controls.TextBox? HotkeyTextBox;
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
                        Title = Strings.SetHotkeyTitle.Localize();
                        // update any labels/buttons by rebuilding
                        var dc = DataContext;
                        Content = null;
                        BuildUI();
                        DataContext = dc;
                    }
                    catch (Exception ex) { Logger.WriteLine(LogLevel.Error, "[HotkeyWindow] HotkeyWindow inner rebuild failed: {0}", ex.Message); }
                }));
            }
            catch (Exception ex) { Logger.WriteLine(LogLevel.Error, "[HotkeyWindow] HotkeyWindow OnLanguageChanged invoke failed: {0}", ex.Message); }
        }

        private void BuildUI()
        {
            Title = Strings.SetHotkeyTitle.Localize();
            Width = 350;
            Height = 250;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var card = new Wpf.Ui.Controls.Card { Margin = new Thickness(20) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tb = new Wpf.Ui.Controls.TextBlock { Text = Strings.PressDesiredKeyCombination.Localize(), Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(tb, 0);
            grid.Children.Add(tb);

            HotkeyTextBox = new Wpf.Ui.Controls.TextBox
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

            var saveBtn = new Wpf.Ui.Controls.Button { Content = Strings.Save.Localize(), Appearance = Wpf.Ui.Controls.ControlAppearance.Primary, Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            saveBtn.Click += SaveButton_Click;
            var cancelBtn = new Wpf.Ui.Controls.Button { Content = Strings.Cancel.Localize(), Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary, Width = 80 };
            cancelBtn.Click += CancelButton_Click;

            sp.Children.Add(saveBtn);
            sp.Children.Add(cancelBtn);
            grid.Children.Add(sp);

            card.Content = grid;
            Content = card;
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
