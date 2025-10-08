using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace krrTools.Configuration;

// 工具选项接口，所有工具选项类必须实现此接口
public interface IToolOptions
{
    void Validate();
}

/// <summary>
/// 预览选项提供者接口
/// </summary>
public interface IPreviewOptionsProvider
{
    IToolOptions GetPreviewOptions();
}

// 基类，实现了基本的选项加载和保存逻辑
public abstract class ToolOptionsBase : ObservableObject, IToolOptions
{
    protected bool IsValidating { get; set; }

    /// <summary>
    /// Validate and normalize option values (called by UI or callers before use)
    /// Default implementation clamps numeric properties based on OptionAttribute Min/Max.
    /// </summary>
    public virtual void Validate()
    {
        if (IsValidating) return;
        IsValidating = true;
        try
        {
            var properties = GetType().GetProperties();
            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<OptionAttribute>();
                if (attr?.Min != null && attr.Max != null && prop.PropertyType == typeof(int))
                {
                    var value = (int)prop.GetValue(this)!;
                    var min = Convert.ToInt32(attr.Min);
                    var max = Convert.ToInt32(attr.Max);
                    var clamped = Math.Clamp(value, min, max);
                    if (clamped != value)
                    {
                        prop.SetValue(this, clamped);
                    }
                }
                else if (attr?.Min != null && attr.Max != null && prop.PropertyType == typeof(double))
                {
                    var value = (double)prop.GetValue(this)!;
                    var min = Convert.ToDouble(attr.Min);
                    var max = Convert.ToDouble(attr.Max);
                    var clamped = Math.Clamp(value, min, max);
                    if (Math.Abs(clamped - value) > 1e-9)
                    {
                        prop.SetValue(this, clamped);
                    }
                }
            }
        }
        finally
        {
            IsValidating = false;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!IsValidating)
            // 设置变化时，通过UI或其他方式触发BaseOptionsManager.SaveOptions
            Console.WriteLine($"[ToolOptions] Property changed: {e.PropertyName}");
        else
            Console.WriteLine($"[ToolOptions] Property changed during validation, not sending message");
    }
}

/// <summary>
/// 统一的工具选项基类，包含通用设置
/// </summary>
public abstract class UnifiedToolOptions : ToolOptionsBase
{
    public PresetKind SelectedPreset { get; init; } = PresetKind.Default;
}

/// <summary>
/// 选项属性，用于定义选项的元数据
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class OptionAttribute : Attribute
{
    public string? LabelKey { get; set; } // Strings中的键，如 "DPModifyKeysCheckbox"
    public string? TooltipKey { get; set; } // Strings中的键，如 "DPModifyKeysTooltip"
    public object? DefaultValue { get; set; }
    public object? Min { get; set; }
    public object? Max { get; set; }
    public UIType UIType { get; set; } = UIType.Toggle;
    public Type? DataType { get; set; } // 数据类型，如 typeof(int), typeof(double) 等
    public double? TickFrequency { get; set; } = 1;
    public double? KeyboardStep { get; set; } = 1;
}

/// <summary>
/// UI控件类型枚举
/// </summary>
public enum UIType
{
    Toggle, // CheckBox
    Slider, // Slider for numeric
    Text, // TextBox for string
    ComboBox, // 下拉框

    NumberBox // 数字输入框
    // 可根据需要添加更多类型
}

/// <summary>
/// 基类，提供选项加载和保存功能
/// </summary>
/// <typeparam name="TOptions">The options type for this tool</typeparam>
public abstract class   ToolViewModelBase<TOptions> : ObservableObject where TOptions : class, IToolOptions, new()
{
    private TOptions _options;
    private readonly ConverterEnum _toolEnum;
    private readonly bool _autoSave;
    private readonly DispatcherTimer? _saveTimer;

    protected ToolViewModelBase(ConverterEnum toolEnum, bool autoSave = true, TOptions? injectedOptions = null)
    {
        _toolEnum = toolEnum;
        _autoSave = autoSave;
        _options = injectedOptions ?? new TOptions();

        // Load options on initialization if not injected
        if (injectedOptions == null) DoLoadOptions();

        // Subscribe to settings changes
        BaseOptionsManager.SettingsChanged += OnSettingsChanged;

        // Initialize save timer for debouncing
        if (_autoSave)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveTimer.Tick += (_, _) =>
            {
                _saveTimer.Stop();
                DoSaveOptions();
            };
        }

        // Subscribe to property changes for auto-save if enabled
        if (_autoSave)
        {
            PropertyChanged += OnPropertyChanged;
            if (_options is ObservableObject observableOptions)
                observableOptions.PropertyChanged += OnOptionsPropertyChanged;
        }
    }

    private void OnSettingsChanged(ConverterEnum changedConverter)
    {
        if (changedConverter == _toolEnum) DoLoadOptions();
    }

    /// <summary>
    /// The options for this tool
    /// </summary>
    public TOptions Options
    {
        get => _options;
        set
        {
            if (SetProperty(ref _options, value))
            {
                // Validate the new options
                _options.Validate();
                // Unsubscribe from old options and subscribe to new ones
                if (_autoSave)
                {
                    if (_options is ObservableObject oldObservable && oldObservable != value)
                        oldObservable.PropertyChanged -= OnOptionsPropertyChanged;
                    if (value is ObservableObject newObservable)
                        newObservable.PropertyChanged += OnOptionsPropertyChanged;
                }
            }
        }
    }

    private void DoLoadOptions()
    {
        try
        {
            var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolEnum);
            if (saved != null)
            {
                saved.Validate();
                Options = saved;
            }
        }
        catch
        {
            Console.WriteLine("[DEBUG] Failed to load options; using defaults.");
        }
    }

    private void DoSaveOptions()
    {
        try
        {
            var optionsToSave = Options;
            optionsToSave.Validate();
            BaseOptionsManager.SaveOptions(_toolEnum, optionsToSave);
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, $"[ToolOptions] Failed to save options for {_toolEnum}: {ex.Message}");
            Console.WriteLine("[DEBUG] Failed to save options; changes may be lost.");
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_autoSave && e.PropertyName != nameof(Options)) StartDelayedSave();
    }

    private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 触发ViewModel的PropertyChanged事件，以便UI（如预览）能监听到选项变化
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Options)));
        if (_autoSave) DoSaveOptions();
    }

    private void StartDelayedSave()
    {
        if (_saveTimer == null) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }
}

/// <summary>
/// Unified base class for tool controls that need options management
/// </summary>
/// <typeparam name="TOptions">The options type for this tool</typeparam>
public abstract class ToolViewBase<TOptions> : UserControl where TOptions : class, IToolOptions, new()
{
    private readonly ConverterEnum _toolEnum;

    protected ToolViewBase(ConverterEnum toolEnum, TOptions? injectedOptions = null)
    {
        _toolEnum = toolEnum;
        Options = injectedOptions ?? new TOptions();

        // Load options on initialization if not injected
        if (injectedOptions == null) DoLoadOptions();

        // 订阅设置变化事件
        BaseOptionsManager.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(ConverterEnum changedConverter)
    {
        if (changedConverter == _toolEnum) DoLoadOptions();
    }

    /// <summary>
    /// The options for this tool
    /// </summary>
    protected TOptions Options { get; private set; }

    private void DoLoadOptions()
    {
        var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolEnum);
        if (saved != null) Options = saved;
    }

    private void DoSaveOptions()
    {
        var optionsToSave = Options;
        optionsToSave.Validate();
        BaseOptionsManager.SaveOptions(_toolEnum, optionsToSave);
    }
}

/// <summary>
/// 预设类型枚举
/// </summary>
public enum PresetKind
{
    [Description("Default|默认")] Default = 0,
    [Description("10K Preset|10K预设")] TenK = 1,
    [Description("8K Preset|8K预设")] EightK = 2,
    [Description("7K Preset|7K预设")] SevenK = 3
}