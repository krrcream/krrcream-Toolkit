using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using krrTools.Configuration;
using krrTools.Tools.DPtool;
using krrTools.Tools.N2NC;
// using krrTools.Tools.LNTransformer;
using krrTools.UI;

namespace krrTools.Tools.Preview
{
    // 独立弹出窗口：左侧为设置，右侧为预览（用于 Converter / DP / LN 工具），备份，未来再考虑是否保留或优化
    public class DetachedToolWindow : Window
    {
        public event EventHandler? MergeRequested;
        private readonly DualPreviewControl _previewControl = new();
        private readonly string _headerText;

        public DetachedToolWindow(string header, UIElement? settingsContent, ResourceDictionary? settingsResources, IPreviewProcessor processor)
        {
            _headerText = header;
            Title = header + " (Detached)";
            Width = 1100;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = SharedUIComponents.PanelBackgroundBrush;

            AddResourcesIfMissing(settingsResources);

            _previewControl.Processor = processor;
            
            try
            {
                if (settingsContent is FrameworkElement fe && processor is PreviewProcessor pp)
                {
                    // 根据工具类型设置相应的选项提供器
                    if (pp.CurrentTool == BaseOptionsManager.N2NCToolName && fe.DataContext is N2NCViewModel convVm)
                    {
                        pp.ConverterOptionsProvider = () => convVm.GetConversionOptions();
                        if (convVm is INotifyPropertyChanged npc)
                            npc.PropertyChanged += (_, _) => _previewControl.Refresh();
                    }
                    else if (pp.CurrentTool == BaseOptionsManager.DPToolName && fe.DataContext is DPToolViewModel dpVm)
                    {
                        pp.DPOptionsProvider = () => dpVm.Options;
                        if (dpVm is INotifyPropertyChanged npc)
                        {
                            npc.PropertyChanged += (_, _) => _previewControl.Refresh();
                            // Options 也会触发 PropertyChanged
                            dpVm.Options.PropertyChanged += (_, _) => _previewControl.Refresh();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DetachedToolWindow: Failed to bind settings to preview processor: {ex.Message}");
            }

            Content = BuildLayout(settingsContent);
            
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
        }
        
        private UIElement BuildLayout(UIElement? settingsContent)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new DockPanel { Margin = new Thickness(6, 6, 6, 0) };
            Grid.SetRow(toolbar, 0);
            var mergeBtn = SharedUIComponents.CreateStandardButton("Merge back|合并回主界面");
            mergeBtn.Width = 140; // 设置固定宽度以保持按钮大小一致
            mergeBtn.Padding = new Thickness(10, 4, 10, 4);
            mergeBtn.HorizontalAlignment = HorizontalAlignment.Right;
            mergeBtn.Margin = new Thickness(0, 0, 0, 4);
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
                Background = SharedUIComponents.PanelBackgroundBrush,
                BorderBrush = SharedUIComponents.PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(8),
                Padding = new Thickness(0)
            };
            // If settingsContent is already a ScrollViewer, use it directly to avoid nesting
            if (settingsContent is ScrollViewer existingScroll)
            {
                settingsBorder.Child = existingScroll;
            }
            else
            {
                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = settingsContent };
                settingsBorder.Child = scroll;
            }
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

        protected override void OnClosed(EventArgs e)
        {
            SharedUIComponents.LanguageChanged -= OnLanguageChanged;
            base.OnClosed(e);
        }

        private void OnLanguageChanged()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => { Title = _headerText + " (Detached)"; }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DetachedToolWindow OnLanguageChanged failed: {ex.Message}");
            }
        }
    }
}
