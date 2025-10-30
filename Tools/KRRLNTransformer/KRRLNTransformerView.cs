using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;
using Button = Wpf.Ui.Controls.Button;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerView : ToolViewBase<KRRLNTransformerOptions>
    {
        private readonly KRRLNTransformerViewModel _viewModel;
        private FrameworkElement seedPanel
        {
            get => SettingsBinder.CreateSeedPanel(_viewModel, x => x.Options.Seed);
        }

        public event EventHandler? SettingsChanged;

        public KRRLNTransformerView()
            : base(ConverterEnum.KRRLN)
        {
            _viewModel = new KRRLNTransformerViewModel(Options);
            DataContext = _viewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch };

            // 长度阈值设置 - 可空滑条自动带勾选框
            UIElement lengthThresholdPanel = SettingsBinder.CreateTemplatedSlider(
                _viewModel.Options,
                o => o.LengthThreshold);
            stack.Children.Add(lengthThresholdPanel);

            // 短面条设置区域标题
            var shortHeader = new TextBlock
            {
                FontSize = UIConstants.HEADER_FONT_SIZE,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            shortHeader.SetBinding(TextBlock.TextProperty,
                                   new Binding("Value") { Source = Strings.KRRShortLNHeader.GetLocalizedString() });
            stack.Children.Add(shortHeader);

            // 短面条设置 - 使用模板化控件
            UIElement shortPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ShortPercentage);
            stack.Children.Add(shortPercPanel);

            UIElement shortLevelPanel = SettingsBinder.CreateTemplatedSliderWithDynamicMax(_viewModel.Options,
                                                                                           o => o.ShortLevel,
                                                                                           _viewModel,
                                                                                           nameof(_viewModel.ShortLevelMaximum),
                                                                                           valueDisplayMap: Options.ShortLengthDict);

            stack.Children.Add(shortLevelPanel);

            UIElement shortLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ShortLimit);
            stack.Children.Add(shortLimitPanel);

            UIElement shortRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ShortRandom);
            stack.Children.Add(shortRandomPanel);

            // 分隔线
            var separator1 = new Separator { Margin = new Thickness(0, 5, 0, 5) };
            stack.Children.Add(separator1);

            // 长面条设置区域标题
            var longHeader = new TextBlock
            {
                FontSize = UIConstants.HEADER_FONT_SIZE,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            longHeader.SetBinding(TextBlock.TextProperty,
                                  new Binding("Value") { Source = Strings.KRRLongLNHeader.GetLocalizedString() });
            stack.Children.Add(longHeader);

            // 长面条设置 - 使用模板化控件
            UIElement longPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongPercentage);
            stack.Children.Add(longPercPanel);

            UIElement longLevelPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongLevel);
            stack.Children.Add(longLevelPanel);

            UIElement longLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongLimit);
            stack.Children.Add(longLimitPanel);

            UIElement longRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LongRandom);
            stack.Children.Add(longRandomPanel);

            // 分隔线
            var separator2 = new Separator { Margin = new Thickness(0, 5, 0, 5) };
            stack.Children.Add(separator2);

            // 对齐设置 - 可空滑条自动带勾选框
            UIElement alignPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Alignment);
            stack.Children.Add(alignPanel);

            // LN对齐设置 - 可空滑条自动带勾选框 暂时隐藏
            /*var lnAlignPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LNAlignment);
            stack.Children.Add(lnAlignPanel);*/

            // 处理原始面条复选框
            FrameworkElement processOriginalPanel = SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.ProcessOriginalIsChecked);
            stack.Children.Add(processOriginalPanel);

            // OD设置 - 带勾选的滑条
            UIElement odPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.ODValue);
            stack.Children.Add(odPanel);

            stack.Children.Add(seedPanel);

            // 预设面板
            FrameworkElement presetsBorder = PresetPanelFactory.CreatePresetPanel(
                "KRRLN",
                () => _viewModel.GetPreviewOptions() as KRRLNTransformerOptions,
                (opt) =>
                {
                    if (opt == null) return;
                    _viewModel.Options.CopyFrom(opt);
                }
            );

            // 预设面板中插入内置预设按钮
            if (presetsBorder is StackPanel outerPanel)
            {
                var builtinPresetsPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

                foreach ((PresetKind kind, string _, KRRLNTransformerOptions options) in KRRLNPresetBottom.GetPresetTemplates())
                {
                    Button btn = SharedUIComponents.CreateStandardButton(KRRLNPresetBottom.GetEnumDescription(kind));
                    btn.Width = 100;
                    btn.Click += (_, _) => { _viewModel.Options.CopyFrom(options); };
                    builtinPresetsPanel.Children.Add(btn);
                }

                outerPanel.Children.Insert(0, builtinPresetsPanel);
            }

            FrameworkElement presetsPanel = SharedUIComponents.CreateLabeledRow(Strings.PresetsLabel, presetsBorder, new Thickness(0, 6, 0, 6));
            stack.Children.Add(presetsPanel);

            root.Content = stack;
            Content = root;

            _viewModel.PropertyChanged += (_, _) => { SettingsChanged?.Invoke(this, EventArgs.Empty); };
        }
    }
}
