using System;
using System.Collections.Generic;
using System.ComponentModel;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Localization;

namespace krrTools.Tools.N2NC
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

    /// <summary>
    /// N2NC响应式ViewModel - 键数转换配置的响应式管理
    /// 核心功能：实时配置更新 + 预览联动 + 智能约束处理
    /// 融合ToolViewModelBase基础功能和Bindable的响应式能力
    /// </summary>
    public class N2NCViewModel : ToolViewModelBase<N2NCOptions>, IPreviewOptionsProvider
    {
        // 本地化事件处理器委托引用，用于Dispose时取消订阅
        private PropertyChangedEventHandler? _maxKeysDisplayHandler;
        private PropertyChangedEventHandler? _minKeysDisplayHandler;

        // Transform speed slot display mapping dictionary (for UI binding)
        public static readonly Dictionary<double, string> TransformSpeedSlotDict = new Dictionary<double, string>
        {
            { 1, "1/16" },
            { 2, "1/8" },
            { 3, "1/4" },
            { 4, "1/2" },
            { 5, "1" },
            { 6, "2" },
            { 7, "4" },
            { 8, "8" }
        };

        // 实际值映射 - 滑条值转换为实际速度值
        public static readonly Dictionary<double, double> TransformSpeedActualDict = new Dictionary<double, double>
        {
            { 1, 0.0625 },
             { 2, 0.125 },
             { 3, 0.25 },
             { 4, 0.5 },
            { 5, 1.0 },
            { 6, 2.0 },
            { 7, 4.0 },
            { 8, 8.0 }
        };
        // 响应式属性 - 核心配置项
        private Bindable<double> _targetKeys = null!;
        private Bindable<double> _maxKeys = null!;
        private Bindable<double> _minKeys = null!;
        private Bindable<double> _transformSpeed = null!;
        private Bindable<int?> _seed = null!;
        private Bindable<PresetKind> _selectedPreset = null!;    
        public N2NCViewModel(N2NCOptions options) : base(ConverterEnum.N2NC, true, options)
    {
        InitializeLocalized();
        
        // 初始化响应式属性
        _targetKeys = new Bindable<double>(Options.TargetKeys);
        _maxKeys = new Bindable<double>(Options.MaxKeys);
        _minKeys = new Bindable<double>(Options.MinKeys);
        _transformSpeed = new Bindable<double>(Options.TransformSpeed);
        _seed = new Bindable<int?>(Options.Seed);
        _selectedPreset = new Bindable<PresetKind>(Options.SelectedPreset);

        // 绑定到Options
        _targetKeys.OnValueChanged(v => Options.TargetKeys = v);
        _maxKeys.OnValueChanged(v => Options.MaxKeys = v);
        _minKeys.OnValueChanged(v => Options.MinKeys = v);
        _transformSpeed.OnValueChanged(v => Options.TransformSpeed = v);
        _seed.OnValueChanged(v => Options.Seed = v);
        // SelectedPreset is init-only, don't bind back

        // 智能约束1: TargetKeys变更时自动调整MaxKeys
        _targetKeys.OnValueChanged(targetKeys =>
        {
            if (_maxKeys.Value > targetKeys)
            {
                Console.WriteLine($"[N2NC约束] TargetKeys变更 {targetKeys}，自动调整MaxKeys: {_maxKeys.Value} → {targetKeys}");
                _maxKeys.Value = targetKeys;
            }
            // 同时确保MinKeys不超过TargetKeys
            if (_minKeys.Value > targetKeys)
            {
                Console.WriteLine($"[N2NC约束] TargetKeys变更 {targetKeys}，自动调整MinKeys: {_minKeys.Value} → {targetKeys}");
                _minKeys.Value = targetKeys;
            }
        });

        // 智能约束2: MaxKeys变更时确保MinKeys不超过它
        _maxKeys.OnValueChanged(maxKeys =>
        {
            if (_minKeys.Value > maxKeys)
            {
                Console.WriteLine($"[N2NC约束] MaxKeys变更 {maxKeys}，自动调整MinKeys: {_minKeys.Value} → {maxKeys}");
                _minKeys.Value = maxKeys;
            }
        });

        // 触发初始的动态最大值通知，确保UI滑条范围正确
        OnPropertyChanged(nameof(MaxKeysMaximum));
        OnPropertyChanged(nameof(MinKeysMaximum));
    }
    
    /// <summary>
    /// 设置响应式约束 - 创建响应式属性并设置约束逻辑
    /// <summary>
    /// 处理选项属性变化 - 同步响应式属性
    /// </summary>
    protected override void OnOptionsPropertyChangedInternal(PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Options.TargetKeys):
                if (Math.Abs(_targetKeys.Value - Options.TargetKeys) > TOLERANCE)
                {
                    _targetKeys.Value = Options.TargetKeys;
                }
                break;
            case nameof(Options.MaxKeys):
                if (Math.Abs(_maxKeys.Value - Options.MaxKeys) > TOLERANCE)
                {
                    _maxKeys.Value = Options.MaxKeys;
                }
                break;
            case nameof(Options.MinKeys):
                if (Math.Abs(_minKeys.Value - Options.MinKeys) > TOLERANCE)
                {
                    _minKeys.Value = Options.MinKeys;
                }
                break;
            case nameof(Options.TransformSpeed):
                if (Math.Abs(_transformSpeed.Value - Options.TransformSpeed) > TOLERANCE)
                {
                    _transformSpeed.Value = Options.TransformSpeed;
                }
                break;
            case nameof(Options.Seed):
                if (_seed.Value != Options.Seed)
                {
                    _seed.Value = Options.Seed;
                }
                break;
            case nameof(Options.SelectedPreset):
                if (_selectedPreset.Value != Options.SelectedPreset)
                {
                    _selectedPreset.Value = Options.SelectedPreset;
                }
                break;
        }
    }

    private KeySelectionFlags _keySelection = KeySelectionFlags.None;        private void InitializeLocalized()
        {
            // Listen to language changes for display updates
            _maxKeysDisplayHandler = (_, _) => OnPropertyChanged(nameof(MaxKeysDisplay));
            _minKeysDisplayHandler = (_, _) => OnPropertyChanged(nameof(MinKeysDisplay));
            
            Strings.N2NCMaxKeysTemplate.GetLocalizedString().PropertyChanged += _maxKeysDisplayHandler;
            Strings.N2NCMinKeysTemplate.GetLocalizedString().PropertyChanged += _minKeysDisplayHandler;
        }

        public KeySelectionFlags KeySelection { get; set; } = KeySelectionFlags.None;

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
            get => _seed.Value;
            set
            {
                if (_seed.Value != value)
                {
                    _seed.Value = value;
                }
            }
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

        /// <summary>
        /// 目标键数 - 响应式属性，自动触发约束和事件
        /// </summary>
        public double TargetKeys
        {
            get => _targetKeys.Value;
            set 
            {
                if (Math.Abs(_targetKeys.Value - value) > TOLERANCE)
                {
                    _targetKeys.Value = value;
                    OnPropertyChanged(nameof(MaxKeysMaximum)); // 通知计算属性更新
                    // 响应式约束会自动处理，无需手动调用UpdateKeyConstraints
                }
            }
        }

        private const double TOLERANCE = 1e-8;

        /// <summary>
        /// 最大键数 - 响应式属性，自动触发约束和事件
        /// </summary>
        public double MaxKeys
        {
            get => _maxKeys.Value;
            set
            {
                if (Math.Abs(_maxKeys.Value - value) > TOLERANCE)
                {
                    _maxKeys.Value = value;
                    OnPropertyChanged(nameof(MinKeysMaximum)); // 通知计算属性更新
                    // 响应式约束会自动处理
                }
            }
        }

        /// <summary>
        /// 最小键数 - 响应式属性，自动触发约束和事件
        /// </summary>
        public double MinKeys
        {
            get => _minKeys.Value;
            set
            {
                if (Math.Abs(_minKeys.Value - value) > TOLERANCE)
                {
                    _minKeys.Value = value;
                    // 响应式约束会自动处理
                }
            }
        }

        public double MinKeysMaximum => MaxKeys;
        public double MaxKeysMaximum => TargetKeys;

        /// <summary>
        /// 变换速度 - 响应式属性，自动更新显示
        /// </summary>
        public double TransformSpeed
        {
            get => _transformSpeed.Value;
            set
            {
                if (Math.Abs(_transformSpeed.Value - value) > TOLERANCE)
                {
                    _transformSpeed.Value = value;
                    OnPropertyChanged(nameof(TransformSpeedDisplay));
                    OnPropertyChanged(nameof(TransformSpeedSlot));
                }
            }
        }

        // TransformSpeedSlot是TransformSpeed的整数档位表示 (1-8)
        [Option(LabelKey = nameof(Strings.N2NCTransformSpeedTemplate), Min = 1, Max = 8, UIType = UIType.Slider, 
            DisplayMapField = "TransformSpeedSlotDict", ActualMapField = "TransformSpeedActualDict", DataType = typeof(double))]
        public double TransformSpeedSlot
        {
            get => _transformSpeedSlot;
            set 
            { 
                if (Math.Abs(_transformSpeedSlot - value) > TOLERANCE)
                {
                    _transformSpeedSlot = value;
                    // 自动映射到实际速度值
                    if (TransformSpeedActualDict.TryGetValue(value, out var actualSpeed))
                    {
                        TransformSpeed = actualSpeed;
                    }
                    OnPropertyChanged();
                }
            }
        }
        private double _transformSpeedSlot = 5.0; // 默认为档位5 (速度1.0)

        public string TransformSpeedDisplay
        {
            get
            {
                // 根据当前速度值显示对应的节拍标签
                double v = Options.TransformSpeed;
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
                TargetKeys = Options.TargetKeys,
                MaxKeys = Options.MaxKeys,
                MinKeys = Options.MinKeys,
                TransformSpeed = Options.TransformSpeed,
                SelectedKeyTypes = selectedKeys,
                Seed = Options.Seed,
                SelectedKeyFlags = KeySelection,
                SelectedPreset = Options.SelectedPreset
            };
        }

        public IToolOptions GetPreviewOptions() => GetConversionOptions();

        // Localized display properties
        public string MaxKeysDisplay => string.Format(Strings.N2NCMaxKeysTemplate.GetLocalizedString().Value, MaxKeys);
        public string MinKeysDisplay => string.Format(Strings.N2NCMinKeysTemplate.GetLocalizedString().Value, MinKeys);

        /// <summary>
        /// 释放资源，取消所有事件订阅
        /// </summary>
        public new void Dispose()
        {
            // 调用基类Dispose处理响应式属性清理
            base.Dispose();
            
            // 取消本地化字符串的事件订阅
            if (_maxKeysDisplayHandler != null)
                Strings.N2NCMaxKeysTemplate.GetLocalizedString().PropertyChanged -= _maxKeysDisplayHandler;
            if (_minKeysDisplayHandler != null)
                Strings.N2NCMinKeysTemplate.GetLocalizedString().PropertyChanged -= _minKeysDisplayHandler;
        }
    }
}