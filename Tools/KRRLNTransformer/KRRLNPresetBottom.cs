using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.KRRLNTransformer
{
    // 内置预设按钮组件，为KRRLNTransformer提供预设模板
    public abstract class KRRLNPresetBottom : Window
    {
        private readonly KRRLNTransformerViewModel _viewModel;

        private static readonly IReadOnlyDictionary<PresetKind, (string Name, KRRLNTransformerOptions Options)> PresetTemplates
            = new Dictionary<PresetKind, (string, KRRLNTransformerOptions)>
            {
                [PresetKind.Default] = ("Default", CreatePreset(opts => { /* 默认设置 */ })),
                [PresetKind.TenK] = ("10K Preset", CreatePreset(opts => {
                    opts.ShortPercentage.Value = 80;
                    opts.ShortLevel.Value = 60;
                    opts.ShortLimit.Value = 8;
                    opts.ShortRandom.Value = 20;
                    opts.LongPercentage.Value = 60;
                    opts.LongLevel.Value = 60;
                    opts.LongLimit.Value = 8;
                    opts.LongRandom.Value = 40;
                    opts.LengthThreshold.Value = 4;
                    opts.Alignment.Value = 4;
                    opts.LNAlignment.Value = 5;
                    opts.ProcessOriginalIsChecked.Value = true;
                    opts.ODValue.Value = 6;
                    opts.Seed.Value = 12345;
                })),
                [PresetKind.EightK] = ("8K Preset", CreatePreset(opts => {
                    opts.ShortPercentage.Value = 70;
                    opts.ShortLevel.Value = 70;
                    opts.ShortLimit.Value = 6;
                    opts.ShortRandom.Value = 30;
                    opts.LongPercentage.Value = 70;
                    opts.LongLevel.Value = 70;
                    opts.LongLimit.Value = 6;
                    opts.LongRandom.Value = 30;
                    opts.LengthThreshold.Value = 3;
                    opts.Alignment.Value = 3;
                    opts.LNAlignment.Value = 4;
                    opts.ProcessOriginalIsChecked.Value = true;
                    opts.ODValue.Value = 7;
                    opts.Seed.Value = 54321;
                })),
                [PresetKind.SevenK] = ("7K Preset", CreatePreset(opts => {
                    opts.ShortPercentage.Value = 60;
                    opts.ShortLevel.Value = 80;
                    opts.ShortLimit.Value = 5;
                    opts.ShortRandom.Value = 40;
                    opts.LongPercentage.Value = 80;
                    opts.LongLevel.Value = 80;
                    opts.LongLimit.Value = 5;
                    opts.LongRandom.Value = 20;
                    opts.LengthThreshold.Value = 2;
                    opts.Alignment.Value = 2;
                    opts.LNAlignment.Value = 3;
                    opts.ProcessOriginalIsChecked.Value = true;
                    opts.ODValue.Value = 8;
                    opts.Seed.Value = 99999;
                }))
            };

        private static KRRLNTransformerOptions CreatePreset(Action<KRRLNTransformerOptions> modifier)
        {
            var options = new KRRLNTransformerOptions();
            modifier(options);
            return options;
        }

        protected KRRLNPresetBottom(KRRLNTransformerViewModel viewModel)
        {
            _viewModel = viewModel;

            BuildUI();

            // Subscribe to language changes so preset button labels update
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => { SharedUIComponents.LanguageChanged -= OnLanguageChanged; };
        }

        private void OnLanguageChanged()
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(OnLanguageChanged));
                    return;
                }
                var dc = DataContext;
                Content = null;
                BuildUI();
                DataContext = dc;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[KRRLNPresetBottom] OnLanguageChanged failed: {0}", ex.Message);
            }
        }

        private void BuildUI()
        {
            Title = "Preset";
            Width = 400; Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(16), VerticalAlignment = VerticalAlignment.Center };

            // 全局切换本地化，其他组件可参考实现，但英文状态下字体偏小，需要修复
            foreach (var kv in PresetTemplates)
            {
                var opts = kv.Value.Options;

                // Use localized enum name (Description attribute) if available
                var localized = LocalizationService.GetLocalizedEnumDisplayName(kv.Key);
                var btn = new Button
                {
                    Content = localized,
                    Margin = new Thickness(0, 0, 0, 8),
                    Height = 36
                };
                btn.Click += (_, _) => { ApplyPresetToViewModel(_viewModel, opts); Close(); };
                root.Children.Add(btn);
            }

            Content = root;
        }

        // 获取预设选项
        public static KRRLNTransformerOptions GetPresetOptions(PresetKind kind)
        {
            if (PresetTemplates.TryGetValue(kind, out var entry))
                return entry.Options;
            return CreatePreset(opts => { /* 默认设置 */ });
        }

        public static IEnumerable<(PresetKind Kind, string Name, KRRLNTransformerOptions Options)> GetPresetTemplates()
        {
            foreach (var kv in PresetTemplates)
                yield return (kv.Key, kv.Value.Name, kv.Value.Options);
        }

        // 添加获取枚举显示名称的方法
        public static string GetEnumDescription(PresetKind value)
        {
            // 使用共享库的多语言支持功能
            return LocalizationService.GetLocalizedEnumDisplayName(value);
        }

        private static void ApplyPresetToViewModel(KRRLNTransformerViewModel viewModel, KRRLNTransformerOptions preset)
        {
            viewModel.Options.CopyFrom(preset);
        }
    }
}