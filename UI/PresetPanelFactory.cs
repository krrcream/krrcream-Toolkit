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
                    var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Apply button uses shared button (handles language hooks internally)
                    var btn = SharedUIComponents.CreateStandardButton(name);
                    btn.HorizontalAlignment = HorizontalAlignment.Stretch;
                    btn.Click += (_, _) => applyOptions(opt);
                    Grid.SetColumn(btn, 0);
                    row.Children.Add(btn);

                    // Delete button - localized content using shared helper
                    var del = SharedUIComponents.CreateStandardButton("Delete|删除");
                    del.Click += (_, _) =>
                    {
                        BaseOptionsManager.DeletePreset(toolName, name);
                        Refresh();
                    };
                    Grid.SetColumn(del, 1);
                    row.Children.Add(del);
                    list.Children.Add(row);
                }
            }

            Refresh();

            var controlRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };

            // Input panel for new preset creation (initially hidden)
            var inputPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
            var inputTextBox = SharedUIComponents.CreateStandardTextBox();
            inputTextBox.Width = 120; // Fixed width to match preset button width
            inputTextBox.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    SaveNewPreset();
                }
            };
            
            var confirmBtn = SharedUIComponents.CreateStandardButton("✓");
            confirmBtn.Width = 30;
            confirmBtn.Click += (_, _) => SaveNewPreset();
            
            var cancelBtn = SharedUIComponents.CreateStandardButton("✕");
            cancelBtn.Width = 30;
            cancelBtn.Click += (_, _) => ToggleInputMode(false);
            
            inputPanel.Children.Add(inputTextBox);
            inputPanel.Children.Add(confirmBtn);
            inputPanel.Children.Add(cancelBtn);

            // Save as preset button - localized and uses shared button behavior
            var saveBtn = SharedUIComponents.CreateStandardButton("Save as Preset|保存为预设");
            saveBtn.Width = 140; // 设置固定宽度以保持按钮大小一致
            saveBtn.Click += (_, _) => ToggleInputMode(true);

            var refreshBtn = SharedUIComponents.CreateStandardButton("Refresh|刷新");
            refreshBtn.Margin = new Thickness(8, 0, 0, 0);
            refreshBtn.Width = 90; // 设置固定宽度以保持按钮大小一致
            refreshBtn.Click += (_, _) => Refresh();
            
            controlRow.Children.Add(saveBtn);
            controlRow.Children.Add(refreshBtn);

            void ToggleInputMode(bool showInput)
            {
                if (showInput)
                {
                    controlRow.Visibility = Visibility.Collapsed;
                    inputPanel.Visibility = Visibility.Visible;
                    inputTextBox.Text = "";
                    inputTextBox.Focus();
                }
                else
                {
                    controlRow.Visibility = Visibility.Visible;
                    inputPanel.Visibility = Visibility.Collapsed;
                }
            }

            void SaveNewPreset()
            {
                var name = inputTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    var current = getCurrentOptions();
                    if (current != null)
                    {
                        BaseOptionsManager.SavePreset(toolName, name, current);
                        Refresh();
                    }
                }
                ToggleInputMode(false);
            }

            outer.Children.Add(list);
            outer.Children.Add(inputPanel);
            outer.Children.Add(controlRow);

            return outer;
        }
    }
}
