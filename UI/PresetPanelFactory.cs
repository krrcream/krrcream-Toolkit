using System;
using System.Windows;
using System.Windows.Controls;
using krrTools.Configuration;
using krrTools.Localization;

namespace krrTools.UI
{
    /// <summary>
    /// A small factory that creates a shared presets panel for any options type T.
    /// The panel lists saved presets for the tool (from OptionsService) and allows applying and saving presets.
    /// </summary>
    public static class PresetPanelFactory
    {
        public static FrameworkElement CreatePresetPanel<T>(string toolName, Func<T?> getCurrentOptions, Action<T?> applyOptions)
            where T : class
        {
            var outer = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 10, 0, 10) };

            var list = new StackPanel { Orientation = Orientation.Vertical };

            // Load current presets
            void Refresh()
            {
                list.Children.Clear();
                foreach (var (name, opt) in BaseOptionsManager.LoadPresets<T>(toolName))
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                    // Apply button uses shared button (handles language hooks internally)
                    var btn = SharedUIComponents.CreateStandardButton(name);
                    btn.HorizontalAlignment = HorizontalAlignment.Left;
                    btn.Width = 120; // 设置固定宽度以保持按钮大小一致
                    btn.Click += (_, _) => applyOptions(opt);
                    row.Children.Add(btn);

                    // Delete button - localized content using shared helper
                    var del = SharedUIComponents.CreateStandardButton("Delete|删除");
                    del.Margin = new Thickness(8, 0, 0, 0);
                    del.Width = 80; // 设置固定宽度以保持按钮大小一致
                    del.Click += (_, _) =>
                    {
                        BaseOptionsManager.DeletePreset(toolName, name);
                        Refresh();
                    };
                    row.Children.Add(del);
                    list.Children.Add(row);
                }
            }

            Refresh();

            var controlRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 250 };

            // Save as preset button - localized and uses shared button behavior
            var saveBtn = SharedUIComponents.CreateStandardButton("Save as Preset|保存为预设");
            saveBtn.Width = 140; // 设置固定宽度以保持按钮大小一致
            saveBtn.Click += (_, _) =>
            {
                var current = getCurrentOptions();
                if (current == null) return;
                var dialog = new InputDialog("Preset name|预设名称", "Enter preset name:|请输入新预设名称:");
                if (dialog.ShowDialog() == true)
                {
                    var name = dialog.Value.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        BaseOptionsManager.SavePreset(toolName, name, current);
                        Refresh();
                    }
                }
            };

            var refreshBtn = SharedUIComponents.CreateStandardButton("Refresh|刷新");
            refreshBtn.Margin = new Thickness(8, 0, 0, 0);
            refreshBtn.Width = 90; // 设置固定宽度以保持按钮大小一致
            refreshBtn.Click += (_, _) => Refresh();
            controlRow.Children.Add(saveBtn);
            controlRow.Children.Add(refreshBtn);

            outer.Children.Add(list);
            outer.Children.Add(controlRow);

            return outer;
        }

        private class InputDialog : Window
        {
            private readonly TextBox _textBox = new TextBox();
            private readonly string _titleParam;
            public string Value => _textBox.Text;
            public InputDialog(string title, string prompt)
            {
                _titleParam = title;
                if (!string.IsNullOrEmpty(title) && title.Contains('|'))
                {
                    void UpdateTitle() => Title = _titleParam.Localize();
                    UpdateTitle();
                    SharedUIComponents.LanguageChanged += UpdateTitle;
                    Closed += (_, _) => SharedUIComponents.LanguageChanged -= UpdateTitle;
                }
                else
                {
                    Title = title.Localize();
                }
                SizeToContent = SizeToContent.WidthAndHeight;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;

                var stack = new StackPanel { Margin = new Thickness(10) };

                // Prompt -- support localized form
                var promptTb = SharedUIComponents.CreateHeaderLabel(prompt.Localize());
                void UpdatePrompt() => promptTb.Text = prompt.Localize();
                SharedUIComponents.LanguageChanged += UpdatePrompt;
                Closed += (_, _) => SharedUIComponents.LanguageChanged -= UpdatePrompt;

                stack.Children.Add(promptTb);
                _textBox.Margin = new Thickness(0, 0, 0, 8);
                stack.Children.Add(_textBox);

                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var ok = SharedUIComponents.CreateStandardButton("OK|确定");
                ok.Margin = new Thickness(0, 0, 8, 0);
                var cancel = SharedUIComponents.CreateStandardButton("Cancel|取消");
                ok.Click += (_, _) => { DialogResult = true; Close(); };
                cancel.Click += (_, _) => { DialogResult = false; Close(); };
                row.Children.Add(ok);
                row.Children.Add(cancel);
                stack.Children.Add(row);

                Content = stack;
            }
        }
    }
}
