using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using krrTools.tools.Shared;

namespace krrTools.tools.N2NC
{
    // Flags enum for selected key types (keeps config compact and easier to reason about)
    [Flags]
    public enum KeySelectionFlags
    {
        None = 0,
        K4 = 1 << 0,
        K5 = 1 << 1,
        K6 = 1 << 2,
        K7 = 1 << 3,
        K8 = 1 << 4,
        K9 = 1 << 5,
        K10 = 1 << 6,
        K10Plus = 1 << 7
    }

    public class N2NCViewModel : ObservableObject
    {
        private double _targetKeys = 10;
        private double _maxKeys = 10;
        private double _minKeys = 2;
        // TransformSpeed is a numeric slider value (double) — slider granularity handled in UI
        private double _transformSpeed = 1.0;
        private int? _seed = 114514;

        // Backing field for flags-based selection
        private KeySelectionFlags _keySelection = KeySelectionFlags.None;

        // Track currently selected preset
        private PresetKind _selectedPreset = PresetKind.Default;

        public N2NCViewModel()
        {
            // Try to load saved ConverterOptions
            try
            {
                var saved = OptionsManager.LoadOptions<N2NCOptions>(OptionsManager.ConverterToolName, OptionsManager.OptionsFileName);
                if (saved != null)
                {
                    TargetKeys = saved.TargetKeys;
                    MaxKeys = saved.MaxKeys;
                    MinKeys = saved.MinKeys;
                    TransformSpeed = saved.TransformSpeed;
                    Seed = saved.Seed;

                    // Restore key flags if available
                    if (saved.SelectedKeyFlags.HasValue)
                    {
                        KeySelection = saved.SelectedKeyFlags.Value;
                    }
                    else if (saved.SelectedKeyTypes != null)
                    {
                        KeySelection = KeySelectionFlags.None;
                        foreach (var k in saved.SelectedKeyTypes)
                        {
                            switch (k)
                            {
                                case 4: KeySelection |= KeySelectionFlags.K4; break;
                                case 5: KeySelection |= KeySelectionFlags.K5; break;
                                case 6: KeySelection |= KeySelectionFlags.K6; break;
                                case 7: KeySelection |= KeySelectionFlags.K7; break;
                                case 8: KeySelection |= KeySelectionFlags.K8; break;
                                case 9: KeySelection |= KeySelectionFlags.K9; break;
                                case 10: KeySelection |= KeySelectionFlags.K10; break;
                                default: KeySelection |= KeySelectionFlags.K10Plus; break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // best-effort load
            }
        }

        // Expose the flags property directly for new code
        public KeySelectionFlags KeySelection
        {
            get => _keySelection;
            set => SetProperty(ref _keySelection, value);
        }

        public PresetKind SelectedPreset
        {
            get => _selectedPreset;
            set => SetProperty(ref _selectedPreset, value);
        }

        // Keep the boolean properties as wrappers for compatibility with existing UI/code
        public bool Is4KSelected
        {
            get => (_keySelection & KeySelectionFlags.K4) == KeySelectionFlags.K4;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K4;
                else KeySelection &= ~KeySelectionFlags.K4;
                OnPropertyChanged();
            }
        }

        public bool Is5KSelected
        {
            get => (_keySelection & KeySelectionFlags.K5) == KeySelectionFlags.K5;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K5;
                else KeySelection &= ~KeySelectionFlags.K5;
                OnPropertyChanged();
            }
        }

        public bool Is6KSelected
        {
            get => (_keySelection & KeySelectionFlags.K6) == KeySelectionFlags.K6;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K6;
                else KeySelection &= ~KeySelectionFlags.K6;
                OnPropertyChanged();
            }
        }

        public bool Is7KSelected
        {
            get => (_keySelection & KeySelectionFlags.K7) == KeySelectionFlags.K7;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K7;
                else KeySelection &= ~KeySelectionFlags.K7;
                OnPropertyChanged();
            }
        }

        public bool Is8KSelected
        {
            get => (_keySelection & KeySelectionFlags.K8) == KeySelectionFlags.K8;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K8;
                else KeySelection &= ~KeySelectionFlags.K8;
                OnPropertyChanged();
            }
        }

        public bool Is9KSelected
        {
            get => (_keySelection & KeySelectionFlags.K9) == KeySelectionFlags.K9;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K9;
                else KeySelection &= ~KeySelectionFlags.K9;
                OnPropertyChanged();
            }
        }

        public bool Is10KSelected
        {
            get => (_keySelection & KeySelectionFlags.K10) == KeySelectionFlags.K10;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K10;
                else KeySelection &= ~KeySelectionFlags.K10;
                OnPropertyChanged();
            }
        }

        public bool Is10KPlusSelected
        {
            get => (_keySelection & KeySelectionFlags.K10Plus) == KeySelectionFlags.K10Plus;
            set
            {
                if (value) KeySelection |= KeySelectionFlags.K10Plus;
                else KeySelection &= ~KeySelectionFlags.K10Plus;
                OnPropertyChanged();
            }
        }

        public int? Seed
        {
            get => _seed;
            set => SetProperty(ref _seed, value);
        }

        // 获取选中的键数列表 - now derived from KeySelection flags
        private List<int> GetSelectedKeyTypes()
        {
            var selectedKeys = new List<int>();

            if (Is4KSelected) selectedKeys.Add(4);
            if (Is5KSelected) selectedKeys.Add(5);
            if (Is6KSelected) selectedKeys.Add(6);
            if (Is7KSelected) selectedKeys.Add(7);
            if (Is8KSelected) selectedKeys.Add(8);
            if (Is9KSelected) selectedKeys.Add(9);
            if (Is10KSelected) selectedKeys.Add(10);
            if (Is10KPlusSelected) selectedKeys.Add(11); // 10K+用11表示

            return selectedKeys;
        }

        public double TargetKeys
        {
            get => _targetKeys;
            set
            {
                SetProperty(ref _targetKeys, value);
                // 更新MaxKeys的最大值，并确保MaxKeys不超过TargetKeys
                MaxKeys = TargetKeys; // 保持最大键数滑块在最右边

                // 更新MinKeys的最大值
                if (MinKeys > MaxKeys)
                    MinKeys = MaxKeys;
            }
        }

        public double MaxKeys
        {
            get => _maxKeys;
            set 
            {
                if (SetProperty(ref _maxKeys, value))
                {
                    // 当最大键数改变时，更新最小键数
                    // 如果最大键数等于1，最小键数等于1；否则最小键数等于2
                    MinKeys = Math.Abs(_maxKeys - 1.0) < 0 ? 1 : 2;

                    // 确保MinKeys不超过MaxKeys
                    if (MinKeys > MaxKeys)
                        MinKeys = MaxKeys;
                    OnPropertyChanged(nameof(MinKeysMaximum));
                }
            }
        }

        public double MinKeys
        {
            get => _minKeys;
            set => SetProperty(ref _minKeys, value);
        }

       public double MinKeysMaximum => MaxKeys;

        // TransformSpeed is a double representing the actual configured speed (slider-controlled)
        public double TransformSpeed
        {
            get => _transformSpeed;
            set
            {
                if (SetProperty(ref _transformSpeed, value))
                {
                    // 当 TransformSpeed 更新时，通知 TransformSpeedDisplay 也已更新
                    OnPropertyChanged(nameof(TransformSpeedDisplay));
                }
            }
        }

        // TransformSpeedSlot是TransformSpeed的整数档位表示 (1-8)
        public int TransformSpeedSlot
        {
            get
            {
                // 将实际速度值转换为滑块档位
                double v = _transformSpeed;
                if (Math.Abs(v - 0.0625) < 1e-8) return 1;
                if (Math.Abs(v - 0.125) < 1e-8) return 2;
                if (Math.Abs(v - 0.25) < 1e-8) return 3;
                if (Math.Abs(v - 0.5) < 1e-8) return 4;
                if (Math.Abs(v - 1.0) < 1e-8) return 5;
                if (Math.Abs(v - 2.0) < 1e-8) return 6;
                if (Math.Abs(v - 4.0) < 1e-8) return 7;
                if (Math.Abs(v - 8.0) < 1e-8) return 8;
                return 1; // 默认返回第一档
            }
            set
            {
                // 将滑块档位转换为实际速度值
                double[] speedValues = [0.0625, 0.125, 0.25, 0.5, 1.0, 2.0, 4.0, 8.0];
                if (value is >= 1 and <= 8)
                {
                    TransformSpeed = speedValues[value - 1];
                }
            }
        }

        public string TransformSpeedDisplay
        {
            get
            {
                // 根据当前速度值显示对应的节拍标签
                double v = _transformSpeed;
                if (Math.Abs(v - 0.0625) < 1e-8) return "1/16";
                if (Math.Abs(v - 0.125) < 1e-8) return "1/8";
                if (Math.Abs(v - 0.25) < 1e-8) return "1/4";
                if (Math.Abs(v - 0.5) < 1e-8) return "1/2";
                if (Math.Abs(v - 1.0) < 1e-8) return "1";
                if (Math.Abs(v - 2.0) < 1e-8) return "2";
                if (Math.Abs(v - 4.0) < 1e-8) return "4";
                if (Math.Abs(v - 8.0) < 1e-8) return "8";
                return v.ToString("G");
            }
        }


        public N2NCOptions GetConversionOptions()
        {
            var selectedKeys = GetSelectedKeyTypes();

            return new N2NCOptions
            {
                TargetKeys = TargetKeys,
                MaxKeys = MaxKeys,
                MinKeys = MinKeys,
                TransformSpeed = TransformSpeed,
                SelectedKeyTypes = selectedKeys,
                Seed = Seed,
                SelectedKeyFlags = KeySelection
            };
        }

    }
}