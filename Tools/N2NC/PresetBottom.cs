using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.N2NC
{
    // TODO: 未来抽象类，最好做成带+号的按钮列表组件，让模块自己去实例化
    // 现在先这样写着，N2NC里有预设模版的备份
    public abstract class PresetBottom : Window
    {
        private readonly N2NCViewModel _viewModel;

        private static readonly IReadOnlyDictionary<PresetKind, (string Name, N2NCOptions Options)> PresetTemplates
            = new Dictionary<PresetKind, (string, N2NCOptions)>
            {
                [PresetKind.Default] = ("Default", CreatePreset(10, 1.0, 114514)),
                [PresetKind.TenK] = ("10K Preset", CreatePreset(10, 2.0, 0)),
                [PresetKind.EightK] = ("8K Preset", CreatePreset(8, 1.0, 0)),
                [PresetKind.SevenK] = ("7K Preset", CreatePreset(7, 1.0, 0))
            };

        private static N2NCOptions CreatePreset(double targetKeys, double transformSpeed, int seed)
        {
            var options = new N2NCOptions();
            options.TargetKeys.Value = targetKeys;
            options.TransformSpeed.Value = transformSpeed;
            options.Seed = seed;
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
            return CreatePreset(10, 1.0, 114514);
        }

        public static IEnumerable<(PresetKind Kind, string Name, N2NCOptions Options)> GetPresetTemplates()
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

        private static void ApplyPresetToViewModel(N2NCViewModel viewModel, N2NCOptions preset)
        {
            viewModel.TargetKeys = Convert.ToInt32(preset.TargetKeys.Value);
            viewModel.TransformSpeed = preset.TransformSpeed.Value;
            viewModel.Seed = preset.Seed;

            if (preset.SelectedKeyFlags.HasValue)
            {
                viewModel.KeySelection = preset.SelectedKeyFlags.Value;
            }
            else if (preset.SelectedKeyTypes != null)
            {
                KeySelectionFlags flags = KeySelectionFlags.None;
                foreach (var k in preset.SelectedKeyTypes)
                {
                    switch (k)
                    {
                        case 4: flags |= KeySelectionFlags.K4; break;
                        case 5: flags |= KeySelectionFlags.K5; break;
                        case 6: flags |= KeySelectionFlags.K6; break;
                        case 7: flags |= KeySelectionFlags.K7; break;
                        case 8: flags |= KeySelectionFlags.K8; break;
                        case 9: flags |= KeySelectionFlags.K9; break;
                        case 10: flags |= KeySelectionFlags.K10; break;
                        case 11: flags |= KeySelectionFlags.K10Plus; break;
                    }
                }
                viewModel.KeySelection = flags;
            }
        }
    }
}