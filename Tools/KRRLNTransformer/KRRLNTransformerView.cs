using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerView : ToolViewBase<KRRLNTransformerOptions>
    {
        private readonly KRRLNTransformerViewModel _viewModel;
        private FrameworkElement seedPanel => SettingsBinder.CreateSeedPanel(_viewModel, x => x.Options.Seed);

        public event EventHandler? SettingsChanged;

        public KRRLNTransformerView() : base(ConverterEnum.KRRLN)
        {
            _viewModel = new KRRLNTransformerViewModel(Options);
            DataContext = _viewModel;
            BuildUI();
        }

        private void BuildUI()
        {
            var root = CreateRootScrollViewer();
            var stack = CreateMainStackPanel();

            // 长度阈值设置 - 使用模板化控件
            var lengthThresholdPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options,
                o => o.LengthThreshold.Value, null, KRRLNTransformerViewModel.LengthThresholdDict);
            stack.Children.Add(lengthThresholdPanel);

            // 短面条设置区域标题
            var shortHeader = new TextBlock { FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold };
            shortHeader.SetBinding(TextBlock.TextProperty,
                new Binding("Value") { Source = Strings.KRRShortLNHeader.GetLocalizedString() });
            stack.Children.Add(shortHeader);

            // 短面条设置 - 使用模板化控件
            var shortPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.PercentageValue);
            stack.Children.Add(shortPercPanel);

            var shortLevelPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.LevelValue);
            stack.Children.Add(shortLevelPanel);

            var shortLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.LimitValue);
            stack.Children.Add(shortLimitPanel);

            var shortRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Short.RandomValue);
            stack.Children.Add(shortRandomPanel);

            // 分隔线
            var separator1 = new Separator { Margin = new Thickness(0, 10, 0, 10) };
            stack.Children.Add(separator1);

            // 长面条设置区域标题
            var longHeader = new TextBlock
            {
                FontSize = UIConstants.HeaderFontSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 15, 0, 0)
            };
            longHeader.SetBinding(TextBlock.TextProperty,
                new Binding("Value") { Source = Strings.KRRLongLNHeader.GetLocalizedString() });
            stack.Children.Add(longHeader);

            // 长面条设置 - 使用模板化控件
            var longPercPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.PercentageValue);
            stack.Children.Add(longPercPanel);

            var longLevelPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.LevelValue);
            stack.Children.Add(longLevelPanel);

            var longLimitPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.LimitValue);
            stack.Children.Add(longLimitPanel);

            var longRandomPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Long.RandomValue);
            stack.Children.Add(longRandomPanel);

            // 分隔线
            var separator2 = new Separator { Margin = new Thickness(0, 10, 0, 10) };
            stack.Children.Add(separator2);

            // 对齐设置 - 使用模板化控件
            var alignPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.Alignment.Value,
                o => o.Alignment.IsChecked, KRRLNTransformerViewModel.AlignValuesDict);
            stack.Children.Add(alignPanel);

            // 处理原始面条复选框 - 使用模板化控件
            var processOriginalPanel =
                SettingsBinder.CreateTemplatedControl(_viewModel.Options, o => o.General.ProcessOriginalIsChecked);
            stack.Children.Add(processOriginalPanel);

            // OD设置 - 使用模板化控件
            var odPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.General.ODValue);
            stack.Children.Add(odPanel);

            // LN对齐设置 - 使用模板化控件
            var lnAlignPanel = SettingsBinder.CreateTemplatedSlider(_viewModel.Options, o => o.LNAlignment.Value,
                o => o.LNAlignment.IsChecked, KRRLNTransformerViewModel.AlignValuesDict);
            stack.Children.Add(lnAlignPanel);

            stack.Children.Add(seedPanel);

            root.Content = stack;
            Content = root;

            _viewModel.PropertyChanged += (_, _) => { SettingsChanged?.Invoke(this, EventArgs.Empty); };
        }

        private ScrollViewer CreateRootScrollViewer()
        {
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private StackPanel CreateMainStackPanel()
        {
            return new StackPanel { Margin = new Thickness(15), HorizontalAlignment = HorizontalAlignment.Stretch };
        }

        public KRRLNTransformerOptions GetOptions()
        {
            // 从ViewModel的Options获取值（所有控件都已模板化并自动绑定）
            return _viewModel.Options;
        }
    }

}