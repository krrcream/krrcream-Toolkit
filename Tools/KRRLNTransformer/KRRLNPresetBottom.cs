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
                [PresetKind.Inverse4] = ("Inverse Space=1/4", CreatePreset(opts => {
                    opts.LengthThreshold.Value = KRRLNTransformerOptions.SAllIsLongLN;
                    opts.ShortPercentage.Value = 100;
                    opts.ShortLevel.Value = 0;
                    opts.ShortLimit.Value = 10;
                    opts.ShortRandom.Value = 0;
                    opts.LongPercentage.Value = 100;
                    opts.LongLevel.Value = 100;
                    opts.LongLimit.Value = 10;
                    opts.LongRandom.Value = 0;
                    opts.Alignment.Value = 5;
                    opts.LNAlignment.Value = 5;
                    opts.ProcessOriginalIsChecked.Value = true;
                    opts.ODValue.Value = 5;
                    opts.Seed.Value = 114514;
                })),
                [PresetKind.NoteToOneHalfLN] = ("NoteToOneHalfLN", CreatePreset(opts => {
                    opts.LengthThreshold.Value = KRRLNTransformerOptions.SAllIsShortLN;
                    opts.ShortPercentage.Value = 100;
                    opts.ShortLevel.Value = 8;
                    opts.ShortLimit.Value = 10;
                    opts.ShortRandom.Value = 0;
                    opts.LongPercentage.Value = 0;
                    opts.LongLevel.Value = 0;
                    opts.LongLimit.Value = 0;
                    opts.LongRandom.Value = 0;
                    opts.Alignment.Value = 5;
                    opts.LNAlignment.Value = 5;
                    opts.ProcessOriginalIsChecked.Value = false;
                    opts.ODValue.Value = 5;
                    opts.Seed.Value = 114514;
                })),
                [PresetKind.ShortLN1] = ("ShortLN", CreatePreset(opts => {
                    opts.LengthThreshold.Value = 4;
                    opts.ShortPercentage.Value = 66;
                    opts.ShortLevel.Value = 4;
                    opts.ShortLimit.Value = 10;
                    opts.ShortRandom.Value = 100;
                    opts.LongPercentage.Value = 33;
                    opts.LongLevel.Value = 35;
                    opts.LongLimit.Value = 10;
                    opts.LongRandom.Value = 30;
                    opts.Alignment.Value = 7;
                    opts.LNAlignment.Value = 5;
                    opts.ProcessOriginalIsChecked.Value = false;
                    opts.ODValue.Value = 5;
                    opts.Seed.Value = 114514;
                })),
                [PresetKind.MidLN1] = ("MidLN", CreatePreset(opts => {
                    opts.LengthThreshold.Value = 2;
                    opts.ShortPercentage.Value = 50;
                    opts.ShortLevel.Value = 8;
                    opts.ShortLimit.Value = 10;
                    opts.ShortRandom.Value = 50;
                    opts.LongPercentage.Value = 50;
                    opts.LongLevel.Value = 50;
                    opts.LongLimit.Value = 10;
                    opts.LongRandom.Value = 55;
                    opts.Alignment.Value = 5;
                    opts.LNAlignment.Value = 5;
                    opts.ProcessOriginalIsChecked.Value = false;
                    opts.ODValue.Value = 5;
                    opts.Seed.Value = 114514;
                })),
                [PresetKind.LongLN1] = ("HardLN", CreatePreset(opts => {
                    opts.LengthThreshold.Value = 2;
                    opts.ShortPercentage.Value = 100;
                    opts.ShortLevel.Value = 8;
                    opts.ShortLimit.Value = 10;
                    opts.ShortRandom.Value = 0;
                    opts.LongPercentage.Value = 75;
                    opts.LongLevel.Value = 75;
                    opts.LongLimit.Value = 10;
                    opts.LongRandom.Value = 55;
                    opts.Alignment.Value = 5;
                    opts.LNAlignment.Value = 5;
                    opts.ProcessOriginalIsChecked.Value = false;
                    opts.ODValue.Value = 5;
                    opts.Seed.Value = 114514;
                })),
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
            // 返回原始的Description字符串以支持动态本地化
            var field = value.GetType().GetField(value.ToString());
            if (field != null)
            {
                var attr = Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) as System.ComponentModel.DescriptionAttribute;
                return attr?.Description ?? value.ToString();
            }
            return value.ToString();
        }

        private static void ApplyPresetToViewModel(KRRLNTransformerViewModel viewModel, KRRLNTransformerOptions preset)
        {
            viewModel.Options.CopyFrom(preset);
        }
    }
}