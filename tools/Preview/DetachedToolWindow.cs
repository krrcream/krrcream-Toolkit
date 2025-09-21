using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.Tools.Converter;
using krrTools.tools.DPtool;

namespace krrTools.tools.Preview
{
    // 独立弹出窗口：左侧为设置，右侧为预览（用于 Converter / DP / LN 等工具）
    public class DetachedToolWindow : Window
    {
        public event EventHandler? MergeRequested;
        private readonly DualPreviewControl _previewControl = new();

        public DetachedToolWindow(string header, UIElement? settingsContent, ResourceDictionary? settingsResources, IPreviewProcessor processor)
        {
            Title = header + " (Detached)";
            Width = 1100;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(250, 251, 253));

            // 将传入的资源合并到窗口资源（如果键不存在）
            AddResourcesIfMissing(settingsResources);

            _previewControl.Processor = processor;

            // 尝试从设置面板的 DataContext 或命名控件中绑定到实际的选项提供器，
            // 这样在弹出窗口中修改设置时能够实时刷新预览。
            try
            {
                if (settingsContent is FrameworkElement fe)
                {
                    // Converter
                    if (processor is ConverterPreviewProcessor cpp && fe.DataContext is ConverterViewModel convVm)
                    {
                        cpp.ConverterOptionsProvider = () => convVm.GetConversionOptions();
                        if (convVm is INotifyPropertyChanged npc)
                            npc.PropertyChanged += (_, _) => _previewControl.Refresh();
                    }

                    // DP tool
                    if (processor is DPPreviewProcessor dpp && fe.DataContext is DPToolViewModel dpVm)
                    {
                        dpp.DPOptionsProvider = () => dpVm.Options;
                        if (dpVm is INotifyPropertyChanged npc)
                        {
                            npc.PropertyChanged += (_, _) => _previewControl.Refresh();
                            // Options 也会触发 PropertyChanged
                            dpVm.Options.PropertyChanged += (_, _) => _previewControl.Refresh();
                        }
                    }

                    // LN 工具：没有独立 ViewModel，直接从设置面板中查找具名控件并建立提供器
                    if (processor is LNPreviewProcessor lpp)
                    {
                        // 在传入的设置视觉树中查找具名后代控件
                        T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
                        {
                            if (root is FrameworkElement feRoot && feRoot.Name == name && feRoot is T tt) return tt;
                            var count = VisualTreeHelper.GetChildrenCount(root);
                            for (int i = 0; i < count; i++)
                            {
                                var child = VisualTreeHelper.GetChild(root, i);
                                var found = FindDescendant<T>(child, name);
                                if (found != null) return found;
                            }
                            return null;
                        }

                        double GetSliderValue(string name) => FindDescendant<Slider>(fe, name) is { } s ? s.Value : 0;
                        bool GetCheckBoxValue(string name) => FindDescendant<CheckBox>(fe, name) is { IsChecked: true };
                        double GetTextBoxDouble(string name) => FindDescendant<TextBox>(fe, name) is { } t && double.TryParse(t.Text, out var v) ? v : 0;

                        lpp.LNParamsProvider = () => new PreviewTransformation.LNPreviewParameters
                        {
                            LevelValue = GetSliderValue("LevelValue"),
                            PercentageValue = GetSliderValue("PercentageValue"),
                            DivideValue = GetSliderValue("DivideValue"),
                            ColumnValue = GetSliderValue("ColumnValue"),
                            GapValue = GetSliderValue("GapValue"),
                            OriginalLN = GetCheckBoxValue("OriginalLN"),
                            FixError = GetCheckBoxValue("FixError"),
                            OverallDifficulty = GetTextBoxDouble("OverallDifficulty")
                        };

                        // 订阅控件变化以刷新预览（忽略订阅失败）
                        foreach (var nm in new[] { "LevelValue", "PercentageValue", "DivideValue", "ColumnValue", "GapValue" })
                            if (FindDescendant<Slider>(fe, nm) is { } s) s.ValueChanged += (_, _) => _previewControl.Refresh();

                        foreach (var nm in new[] { "OriginalLN", "FixError" })
                            if (FindDescendant<CheckBox>(fe, nm) is { } c)
                            {
                                c.Checked += (_, _) => _previewControl.Refresh();
                                c.Unchecked += (_, _) => _previewControl.Refresh();
                            }

                        if (FindDescendant<TextBox>(fe, "OverallDifficulty") is { } tb) tb.TextChanged += (_, _) => _previewControl.Refresh();
                    }
                }
            }
            catch
            {
                Debug.WriteLine("DetachedToolWindow: Failed to bind settings to preview processor.");
            }

            Content = BuildLayout(settingsContent);
        }
        
        private UIElement BuildLayout(UIElement? settingsContent)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new DockPanel { Margin = new Thickness(6, 6, 6, 0) };
            Grid.SetRow(toolbar, 0);
            var mergeBtn = new Button
            {
                Content = "Merge back",
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 4)
            };
            mergeBtn.Click += (_, _) => MergeRequested?.Invoke(this, EventArgs.Empty);
            DockPanel.SetDock(mergeBtn, Dock.Right);
            toolbar.Children.Add(mergeBtn);
            toolbar.Children.Add(new TextBlock
            {
                Text = Title,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 0, 0, 0)
            });
            root.Children.Add(toolbar);

            var main = new Grid { Margin = new Thickness(6, 0, 6, 6) };
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            Grid.SetRow(main, 1);
            root.Children.Add(main);

            var settingsBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(8),
                Padding = new Thickness(0)
            };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = settingsContent };
            settingsBorder.Child = scroll;
            main.Children.Add(settingsBorder);

            Grid.SetColumn(_previewControl, 1);
            _previewControl.Margin = new Thickness(8);
            main.Children.Add(_previewControl);

            return root;
        }

        // 将外部资源字典中的键/值添加到窗口资源（如果键不存在）
        private void AddResourcesIfMissing(ResourceDictionary? resources)
        {
            if (resources == null) return;
            var keys = resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                if (!Resources.Contains(k))
                    Resources.Add(k, resources[k]);
            }
        }

    }
}
