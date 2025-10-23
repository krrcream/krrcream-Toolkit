using System.Reflection;
using System.Windows;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using Microsoft.Extensions.Logging;
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
    private readonly Bindable<FrameworkElement?> _originalVisual = new();
    private readonly Bindable<FrameworkElement?> _convertedVisual = new();
    private readonly Bindable<string> _title = new(string.Empty);

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

        var refreshSubscription = EventBus.Subscribe<ConvPrevRefreshOnlyEvent>(OnPreviewRefreshRequested);
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
        // // Debug日志：显示所有发生的设置变化，由于异步通知管线，这里不生效
        // Logger.WriteLine(LogLevel.Debug, "[SettingsChanged] Tool: {0}, Property: {1}, Type: {2}, Value: {3}", 
        //     _currentTool?.ToString() ?? "None", 
        //     settingsEvent.PropertyName ?? "null", 
        //     settingsEvent.SettingsType?.Name ?? "Unknown", 
        //     settingsEvent.NewValue?.ToString() ?? "null");

        // 只处理当前工具的设置变化
        if (_currentTool == null || settingsEvent.SettingsType == null) return;
        ConverterEnum current = _currentTool.Value;
        string toolName = current.ToString();
        if (!settingsEvent.SettingsType.Name.Contains(toolName)) return;

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
            Logger.WriteLine(LogLevel.Information, "[{0}模块]-{1}-变更为{2}", moduleName, settingsEvent.PropertyName, settingsEvent.NewValue);
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
        LoadPreviewPath(e.FilePath);
    }

    /// <summary>
    /// 响应预览刷新请求事件
    /// </summary>
    private void OnPreviewRefreshRequested(ConvPrevRefreshOnlyEvent e)
    {
        if (e.NewValue)
        {
            RefreshConverted();
        }
        else
        {
            ResetPreview();
        }
    }

    public void LoadPreviewPath(string path)
    {
        // 更新全局最后预览路径
        BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = path);
        
        RefreshOriginal();
        RefreshConverted();
    }

    public void ResetPreview()
    {
        // 清空全局最后预览路径以使用内置样本
        BaseOptionsManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = string.Empty);
        
        RefreshOriginal();
        RefreshConverted();
        
        // 重置后，拖拽区要重置吗？
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
                    OriginalVisual = Processor.BuildOriginalVisual(beatmap);
                });
            else
                OriginalVisual = Processor.BuildOriginalVisual(beatmap);

            var duration = DateTime.Now - decodeStartTime;
            Logger.WriteLine(LogLevel.Debug, "[PreviewViewModel] 原始预览刷新完成，耗时: {0:F1}ms", duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[PreviewViewModel] RefreshOriginal failed: {0}", ex.Message);
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
                    ConvertedVisual = Processor.BuildConvertedVisual(beatmap);
                });
            else
                ConvertedVisual = Processor.BuildConvertedVisual(beatmap);

            var duration = DateTime.Now - decodeStartTime;
            Logger.WriteLine(LogLevel.Information, "[PreviewViewModel] 转换后预览刷新完成，耗时: {0:F1}ms", duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[PreviewViewModel] RefreshConverted failed: {0}", ex.Message);
        }
    }
}