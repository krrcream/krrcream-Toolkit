using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using krrTools.tools.Shared;

namespace krrTools.tools.N2NC
{
    public enum PresetKind
    {
        [Description("Default|默认")]
        Default = 0,
        [Description("10K Preset|10K预设")]
        TenK = 1,
        [Description("8K Preset|8K预设")]
        EightK = 2,
        [Description("7K Preset|7K预设")]
        SevenK = 3
    }
    
    public abstract class PresetBottom : Window
    {
        private readonly N2NCViewModel _viewModel;

        private static readonly IReadOnlyDictionary<PresetKind, (string Name, N2NCOptions Options)> PresetTemplates
            = new Dictionary<PresetKind, (string, N2NCOptions)>
        {
            [PresetKind.Default] = ("Default", new N2NCOptions { TargetKeys = 10, TransformSpeed = 1.0, Seed = 114514 }),
            [PresetKind.TenK] = ("10K Preset", new N2NCOptions { TargetKeys = 10, TransformSpeed = 2.0, Seed = 0 }),
            [PresetKind.EightK] = ("8K Preset", new N2NCOptions { TargetKeys = 8, TransformSpeed = 1.0, Seed = 0 }),
            [PresetKind.SevenK] = ("7K Preset", new N2NCOptions { TargetKeys = 7, TransformSpeed = 1.0, Seed = 0 })
        };

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
                System.Diagnostics.Debug.WriteLine($"PresetBottom OnLanguageChanged failed: {ex.Message}");
            }
        }
        
        private void BuildUI()
        {
            Title = "Preset";
            Width = 400; Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(16), VerticalAlignment = VerticalAlignment.Center };

            foreach (var kv in PresetTemplates)
            {
                var opts = kv.Value.Options;

                // Use localized enum name (Description attribute) if available
                var localized = SharedUIComponents.GetLocalizedEnumDisplayName(kv.Key);
                var btn = new Button { Content = localized, Margin = new Thickness(0, 0, 0, 8), Height = 36 };
                btn.Click += (_, _) => { ApplyPresetToViewModel(_viewModel, opts); Close(); };
                root.Children.Add(btn);
            }

            Content = root;
        }

        public static N2NCOptions GetPresetOptions(PresetKind kind)
        {
            if (PresetTemplates.TryGetValue(kind, out var entry))
                return entry.Options;
            return new N2NCOptions { TargetKeys = 10, TransformSpeed = 1.0, Seed = 114514 };
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
            return SharedUIComponents.GetLocalizedEnumDisplayName(value);
        }

        private static void ApplyPresetToViewModel(N2NCViewModel viewModel, N2NCOptions preset)
        {
            viewModel.TargetKeys = Convert.ToInt32(preset.TargetKeys);
            viewModel.TransformSpeed = preset.TransformSpeed;
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