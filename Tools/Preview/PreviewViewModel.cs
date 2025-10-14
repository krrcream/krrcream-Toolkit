using System;
using System.Collections.Generic;
using System.Windows;
using System.Reflection;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.Preview;

/// <summary>
/// 预览响应式ViewModel - 谱面预览的响应式管理
/// 核心功能：设置变更监听 + 智能刷新 + 事件驱动更新
/// </summary>
public class PreviewViewModel : ReactiveViewModelBase
{
    [Inject] protected IEventBus EventBus { get; set; } = null!;

    // 响应式属性
    private Bindable<FrameworkElement?> _originalVisual = new();
    private Bindable<FrameworkElement?> _convertedVisual = new();
    private Bindable<string> _title = new(string.Empty);

    private ConverterEnum? _currentTool;

    public PreviewViewModel()
    {
        _originalVisual.PropertyChanged += (_, _) => OnPropertyChanged(nameof(OriginalVisual));
        _convertedVisual.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ConvertedVisual));
        _title.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Title));

        var settingsSubscription = EventBus.Subscribe<SettingsChangedEvent>(OnSettingsChanged);
        Disposables.Add(settingsSubscription);

        var beatmapChangedSubscription = EventBus.Subscribe<BeatmapChangedEvent>(OnBeatmapChanged);
        Disposables.Add(beatmapChangedSubscription);

        var refreshSubscription = EventBus.Subscribe<PreviewRefreshEvent>(OnPreviewRefreshRequested);
        Disposables.Add(refreshSubscription);
    }

    public FrameworkElement? OriginalVisual
    {
        get => _originalVisual.Value;
        private set => _originalVisual.Value = value;
    }
    
    public FrameworkElement? ConvertedVisual
    {
        get => _convertedVisual.Value;
        private set => _convertedVisual.Value = value;
    }
    
    public string Title
    {
        get => _title.Value;
        private set => _title.Value = value;
    }

    public IPreviewProcessor? Processor { get; private set; }
    
    public void SetCurrentTool(ConverterEnum? tool)
    {
        _currentTool = tool;
    }

    /// <summary>
    /// 响应设置变更事件 - 智能刷新逻辑
    /// </summary>
    private void OnSettingsChanged(SettingsChangedEvent settingsEvent)
    {
        // 检查此设置是否会触发预览刷新
        if (!ShouldTriggerRefresh(settingsEvent)) return;


        if (Equals(settingsEvent.OldValue, settingsEvent.NewValue))
            return; // 值没有变化，不刷新
        
        // 输出设置变化日志
        if (_currentTool != null && !string.IsNullOrEmpty(settingsEvent.PropertyName) && settingsEvent.NewValue != null)
        {
            var moduleName = _currentTool.Value switch
            {
                ConverterEnum.N2NC => "N2N",
                ConverterEnum.DP => "DP",
                ConverterEnum.KRRLN => "KRRLN",
                _ => "未知"
            };
            Console.WriteLine($"[{moduleName}模块]-{settingsEvent.PropertyName}-变更为{settingsEvent.NewValue}");
        }

        // 直接调用RefreshConverted，在测试环境中Dispatcher可能不可用
        RefreshConverted();
    }

    /// <summary>
    /// 检查设置变化是否应该触发预览刷新
    /// </summary>
    private bool ShouldTriggerRefresh(SettingsChangedEvent settingsEvent)
    {
        if (settingsEvent.SettingsType == null || string.IsNullOrEmpty(settingsEvent.PropertyName)) return false;

        // 使用反射检查属性是否有IsRefresher特性
        var property = settingsEvent.SettingsType.GetProperty(settingsEvent.PropertyName);
        if (property == null) return false;

        var optionAttribute = property.GetCustomAttribute<OptionAttribute>();
        return optionAttribute?.IsRefresher == true;
    }

    /// <summary>
    /// 响应谱面变化事件 - 路径变化时刷新所有预览
    /// </summary>
    private void OnBeatmapChanged(BeatmapChangedEvent e)
    {
        // 更新全局设置中的最后预览路径
        BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = e.FilePath);

        switch (e.ChangeType)
        {
            case BeatmapChangeType.FromMonitoring:
                Title = $"[监听] {e.FileName}";
                break;

            case BeatmapChangeType.FromDropZone:
                Title = $"[拖入] {e.FileName}";
                break;

            default:
                Title = Strings.DropHint.GetLocalizedString();
                break;
        }
    }

    /// <summary>
    /// 响应预览刷新请求事件
    /// </summary>
    private void OnPreviewRefreshRequested(PreviewRefreshEvent e)
    {
        if (e.NewValue)
        {
            RefreshConverted();
        }
        else
        {
            LoadBuiltInSample();
        }
    }


    public void LoadFromPath(string path)
    {
        // 更新全局最后预览路径
        BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = path);

    }

    public void LoadBuiltInSample()
    {
        // 清空全局最后预览路径以使用内置样本
        BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = string.Empty);
        RefreshOriginal();
        RefreshConverted();
    }

    public void SetProcessor(IPreviewProcessor? processor)
    {
        var oldProcessor = Processor;
        Processor = processor;

        if (oldProcessor != processor) OnPropertyChanged(nameof(Processor));
    }

    public virtual void TriggerRefresh()
    {
        RefreshConverted();
    }

    /// <summary>
    /// 只刷新原始预览
    /// </summary>
    private void RefreshOriginal()
    {
        if (Processor == null) return;

        try
        {
            var decodeStartTime = DateTime.Now;

            // 从全局设置获取最后预览路径，如果为空则使用内置样本
            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            var lastPreviewPath = globalSettings.LastPreviewPath.Value;

            var beatmap = !string.IsNullOrEmpty(lastPreviewPath)
                ? BeatmapDecoder.Decode(lastPreviewPath)
                : PreviewManiaNote.BuiltInSampleStream();

            if (beatmap == null) return;

            if (Application.Current != null)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 只刷新原始预览
                    OriginalVisual = Processor.BuildOriginalVisual(beatmap);
                });
            else
                // For testing without WPF Application
                OriginalVisual = Processor.BuildOriginalVisual(beatmap);

            var duration = DateTime.Now - decodeStartTime;
            Console.WriteLine($"[PreviewViewModel] 原始预览刷新完成，耗时: {duration.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PreviewViewModel] RefreshOriginal failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 只刷新转换后预览
    /// </summary>
    private void RefreshConverted()
    {
        if (Processor == null) return;

        try
        {
            var decodeStartTime = DateTime.Now;

            // 从全局设置获取最后预览路径，如果为空则使用内置样本
            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            var lastPreviewPath = globalSettings.LastPreviewPath.Value;

            var beatmap = !string.IsNullOrEmpty(lastPreviewPath)
                ? BeatmapDecoder.Decode(lastPreviewPath)
                : PreviewManiaNote.BuiltInSampleStream();

            if (beatmap == null) return;

            if (Application.Current != null)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 只刷新转换后预览
                    ConvertedVisual = Processor.BuildConvertedVisual(beatmap);
                });
            else
                // For testing without WPF Application
                ConvertedVisual = Processor.BuildConvertedVisual(beatmap);

            var duration = DateTime.Now - decodeStartTime;
            Console.WriteLine($"[PreviewViewModel] 转换后预览刷新完成，耗时: {duration.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PreviewViewModel] RefreshConverted failed: {ex.Message}");
        }
    }

    public void Reset()
    {
        OriginalVisual = null;
        ConvertedVisual = null;
        Title = string.Empty;
    }
}