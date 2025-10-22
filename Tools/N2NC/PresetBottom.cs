using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.N2NC
{
    public abstract class PresetBottom : Window
    {
        private readonly N2NCViewModel _viewModel;

        private static readonly IReadOnlyDictionary<PresetKind, (string Name, N2NCOptions Options)> PresetTemplates
            = new Dictionary<PresetKind, (string, N2NCOptions)>
            {
                [PresetKind.Default] = ("Default", CreatePreset(opts => {
                    opts.TargetKeys.Value = 8;
                    opts.MaxKeys.Value = 8;
                    opts.MinKeys.Value = 2;
                    opts.TransformSpeed.Value = 4;
                    opts.Seed = 114514;
                    opts.SelectedKeyFlags = KeySelectionFlags.None;
                })),
                [PresetKind.TenK] = ("10K Preset", CreatePreset(opts => {
                    opts.TargetKeys.Value = 10;
                    opts.MaxKeys.Value = 8;
                    opts.MinKeys.Value = 2;
                    opts.TransformSpeed.Value = 3;
                    opts.Seed = 0;
                    opts.SelectedKeyFlags = (KeySelectionFlags)0b0001111110;
                })),
                [PresetKind.EightK] = ("8K Preset", CreatePreset(opts => {
                    opts.TargetKeys.Value = 8;
                    opts.MaxKeys.Value = 8;
                    opts.MinKeys.Value = 2;
                    opts.TransformSpeed.Value = 4;
                    opts.Seed = 0;
                    opts.SelectedKeyFlags = (KeySelectionFlags)0b0000011110;
                })),
                [PresetKind.SevenK] = ("7K Preset", CreatePreset(opts => {
                    opts.TargetKeys.Value = 7;
                    opts.MaxKeys.Value = 7;
                    opts.MinKeys.Value = 2;
                    opts.TransformSpeed.Value = 4;
                    opts.Seed = 0;
                    opts.SelectedKeyFlags = (KeySelectionFlags)0b0000001110;
                }))
            };

        private static N2NCOptions CreatePreset(Action<N2NCOptions> modifier)
        {
            var options = new N2NCOptions();
            modifier(options);
            return options;
        }

        protected PresetBottom(N2NCViewModel viewModel)
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
                Logger.WriteLine(LogLevel.Error, "[PresetBottom] PresetBottom OnLanguageChanged failed: {0}", ex.Message);
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

        // 备用的，N2N中有预设按钮，目前没加载
        public static N2NCOptions GetPresetOptions(PresetKind kind)
        {
            if (PresetTemplates.TryGetValue(kind, out var entry))
                return entry.Options;
            return CreatePreset(opts => {
                opts.TargetKeys.Value = 10;
                opts.TransformSpeed.Value = 1.0;
                opts.Seed = 114514;
            });
        }

        public static IEnumerable<(PresetKind Kind, string Name, N2NCOptions Options)> GetPresetTemplates()
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

        private static void ApplyPresetToViewModel(N2NCViewModel viewModel, N2NCOptions preset)
        {
            viewModel.TargetKeys = Convert.ToInt32(preset.TargetKeys.Value);
            viewModel.TransformSpeed = preset.TransformSpeed.Value;
            viewModel.Seed = preset.Seed;
            viewModel.MaxKeys = Convert.ToInt32(preset.MaxKeys.Value);
            viewModel.MinKeys = Convert.ToInt32(preset.MinKeys.Value);
            viewModel.KeySelection = preset.SelectedKeyFlags ?? KeySelectionFlags.None; 
        }
    }
}