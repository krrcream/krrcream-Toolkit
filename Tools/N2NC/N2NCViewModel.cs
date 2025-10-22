using System;
using System.Collections.Generic;
using System.ComponentModel;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;

namespace krrTools.Tools.N2NC;

// Flags enum for selected key types (keeps config compact and easier to reason about)
[Flags]
public enum KeySelectionFlags
{
    None = 0,
    K3Minus = 1 << 0,
    K4 = 1 << 1,
    K5 = 1 << 2,
    K6 = 1 << 3,
    K7 = 1 << 4,
    K8 = 1 << 5,
    K9 = 1 << 6,
    K10 = 1 << 7,
    K10Plus = 1 << 8
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

    public static readonly Dictionary<double, string> TransformSpeedSlotDict = new()
    {
        { 0, "1/8" },
        { 1, "1/4" },
        { 2, "1/2" },
        { 3, "3/4" },
        { 4, "1" },
        { 5, "2" },
        { 6, "3" },
        { 7, "4" },
        { 8, "∞" }
    };

 
    private bool _isUpdatingOptions;

    public N2NCViewModel(N2NCOptions options) : base(ConverterEnum.N2NC, true, options)
    {
        InitializeLocalized();

        // 约束逻辑通过监听 Options.PropertyChanged 实现
        Options.PropertyChanged += OnOptionsPropertyChanged;

        // 触发初始的动态最大值通知，确保UI滑条范围正确
        OnPropertyChanged(nameof(MaxKeysMaximum));
        OnPropertyChanged(nameof(MinKeysMaximum));
    }

    /// <summary>
    /// 处理选项属性变化 - 实现约束逻辑
    /// </summary>
    private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingOptions) return;
        _isUpdatingOptions = true;

        try
        {
            switch (e.PropertyName)
            {
                case nameof(Options.TargetKeys):
                    // 智能约束: TargetKeys变更时自动调整MaxKeys
                    if (Options.MaxKeys.Value > Options.TargetKeys.Value)
                    {
                        Options.MaxKeys.Value = Options.TargetKeys.Value;
                    }
                    // 确保MinKeys不超过TargetKeys
                    if (Options.MinKeys.Value > Options.TargetKeys.Value)
                    {
                        Options.MinKeys.Value = Options.TargetKeys.Value;
                    }
                    OnPropertyChanged(nameof(MaxKeysMaximum)); // 通知计算属性更新
                    break;
                case nameof(Options.MaxKeys):
                    // 确保MaxKeys不超过TargetKeys
                    if (Options.MaxKeys.Value > Options.TargetKeys.Value)
                    {
                        Options.MaxKeys.Value = Options.TargetKeys.Value;
                    }
                    // 确保MaxKeys >= MinKeys
                    if (Options.MaxKeys.Value < Options.MinKeys.Value)
                    {
                        Options.MaxKeys.Value = Options.MinKeys.Value;
                    }
                    OnPropertyChanged(nameof(MinKeysMaximum)); // 通知计算属性更新
                    break;
                case nameof(Options.MinKeys):
                    // 确保MinKeys <= MaxKeys
                    if (Options.MinKeys.Value > Options.MaxKeys.Value)
                    {
                        Options.MinKeys.Value = Options.MaxKeys.Value;
                    }
                    OnPropertyChanged(nameof(MinKeysMaximum)); // 通知计算属性更新
                    break;
                case nameof(Options.TransformSpeed):
                    OnPropertyChanged(nameof(TransformSpeedSlotDict));
                    OnPropertyChanged(nameof(TransformSpeedSlot));
                    break;
            }
        }
        finally
        {
            _isUpdatingOptions = false;
        }
    }

    private KeySelectionFlags _keySelection = KeySelectionFlags.None;

    private void InitializeLocalized()
    {
        _maxKeysDisplayHandler = (_, _) => OnPropertyChanged(nameof(MaxKeysDisplay));
        _minKeysDisplayHandler = (_, _) => OnPropertyChanged(nameof(MinKeysDisplay));

        Strings.N2NCMaxKeysTemplate.GetLocalizedString().PropertyChanged += _maxKeysDisplayHandler;
        Strings.N2NCMinKeysTemplate.GetLocalizedString().PropertyChanged += _minKeysDisplayHandler;
    }

    public KeySelectionFlags KeySelection 
    { 
        get => _keySelection;
        set
        {
            if (_keySelection != value)
            {
                _keySelection = value;
                Options.SelectedKeyFlags = value;  // 同步更新到 Options
                OnPropertyChanged();
            }
        }
    }

    public bool Is3KMinusSelected
    {
        get => (_keySelection & KeySelectionFlags.K3Minus) == KeySelectionFlags.K3Minus;
        set
        {
            if (value) KeySelection |= KeySelectionFlags.K3Minus;
            else KeySelection &= ~KeySelectionFlags.K3Minus;
            OnPropertyChanged();
        }
    }
    
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
        get => Options.Seed;
        set => Options.Seed = value;
    }

    // 获取选中的键数列表 - now derived from KeySelection flags

    /// <summary>
    /// 目标键数 - 响应式属性，自动触发约束和事件
    /// </summary>
    public double TargetKeys
    {
        get => Options.TargetKeys.Value;
        set => Options.TargetKeys.Value = value;
    }

    private const double TOLERANCE = 1e-8;

    /// <summary>
    /// 最大键数 - 响应式属性，自动触发约束和事件
    /// </summary>
    public double MaxKeys
    {
        get => Options.MaxKeys.Value;
        set => Options.MaxKeys.Value = value;
    }

    /// <summary>
    /// 最小键数 - 响应式属性，自动触发约束和事件
    /// </summary>
    public double MinKeys
    {
        get => Options.MinKeys.Value;
        set => Options.MinKeys.Value = value;
    }

    public double MinKeysMaximum => MaxKeys;
    public double MaxKeysMaximum => TargetKeys;

    /// <summary>
    /// 变换速度 - 响应式属性，自动触发约束和事件
    /// </summary>
    public double TransformSpeed
    {
        get => Options.TransformSpeed.Value;
        set => Options.TransformSpeed.Value = value;
    }

    // TransformSpeedSlot是TransformSpeed的整数档位表示 (1-8)
    [Option(LabelKey = nameof(Strings.N2NCTransformSpeedTemplate), Min = 1, Max = 8, UIType = UIType.Slider,
        DisplayMapField = "TransformSpeedSlotDict", ActualMapField = "TransformSpeedActualDict",
        DataType = typeof(double))]
    public double TransformSpeedSlot
    {
        get => _transformSpeedSlot;
        set
        {
            if (Math.Abs(_transformSpeedSlot - value) > TOLERANCE)
            {
                _transformSpeedSlot = value;
                OnPropertyChanged();
            }
        }
    }

    private double _transformSpeedSlot = 5.0; // 默认为档位5 (速度1.0)




    public N2NCOptions GetConversionOptions()
    {
        var options = new N2NCOptions
        {
            Seed = Options.Seed,
            SelectedKeyFlags = KeySelection,
            SelectedPreset = Options.SelectedPreset
        };

        // 设置 Bindable 值
        options.TargetKeys.Value = Options.TargetKeys.Value;
        options.MaxKeys.Value = Options.MaxKeys.Value;
        options.MinKeys.Value = Options.MinKeys.Value;
        options.TransformSpeed.Value = Options.TransformSpeed.Value;

        return options;
    }

    public IToolOptions GetPreviewOptions()
    {
        return GetConversionOptions();
    }

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