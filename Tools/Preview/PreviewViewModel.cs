using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using krrTools.Beatmaps;
using krrTools.Configuration;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.Preview;

/// <summary>
/// 预览刷新触发器类型
/// </summary>
public enum RefreshTrigger
{
    Manual, // 手动刷新
    BeatmapLoaded, // 谱面加载
    ProcessorChanged, // 处理器改变
    SettingsChanged, // 设置改变
    RealTimeToggle // 实时预览开关
}

/// <summary>
/// 预览刷新管理器，统一管理所有刷新请求
/// </summary>
public class PreviewRefreshManager
{
    private readonly PreviewViewModel _viewModel;
    private readonly DispatcherTimer _debounceTimer;
    private RefreshTrigger _pendingTrigger;
    private bool _hasPendingRefresh;
    private DateTime _lastRefreshTime = DateTime.MinValue;

    public PreviewRefreshManager(PreviewViewModel viewModel)
    {
        _viewModel = viewModel;
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounceTimer.Tick += OnDebounceTimerTick;
    }

    public void TriggerRefresh(RefreshTrigger trigger)
    {
        _pendingTrigger = trigger;

        // 立即处理紧急触发器
        if (trigger == RefreshTrigger.Manual || trigger == RefreshTrigger.BeatmapLoaded ||
            trigger == RefreshTrigger.ProcessorChanged)
        {
            ExecuteRefreshNow();
            return;
        }

        // 其他触发器使用防抖
        if (!_hasPendingRefresh)
        {
            _hasPendingRefresh = true;
            _debounceTimer.Start();
        }
    }

    private void OnDebounceTimerTick(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        _hasPendingRefresh = false;
        ExecuteRefreshNow();
    }

    private void ExecuteRefreshNow()
    {
        // 简单的频率限制，避免过于频繁的刷新
        var now = DateTime.Now;
        if ((now - _lastRefreshTime).TotalMilliseconds < 50) return;

        _lastRefreshTime = now;
        _viewModel.ExecuteRefresh();
    }
}

/// <summary>
/// 预览ViewModel，管理预览状态和逻辑
/// </summary>
public class PreviewViewModel : INotifyPropertyChanged
{
    private readonly PreviewRefreshManager _refreshManager;
    private FrameworkElement? _originalVisual;
    private FrameworkElement? _convertedVisual;
    private string _title = string.Empty;
    private string? _beatmapPath; // 保存谱面路径，用于重新加载

    public event PropertyChangedEventHandler? PropertyChanged;

    public FrameworkElement? OriginalVisual
    {
        get => _originalVisual;
        private set
        {
            if (_originalVisual != value)
            {
                _originalVisual = value;
                OnPropertyChanged();
            }
        }
    }

    public FrameworkElement? ConvertedVisual
    {
        get => _convertedVisual;
        private set
        {
            if (_convertedVisual != value)
            {
                _convertedVisual = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsOriginalPreview => OriginalVisual != null;
    public bool IsConvertedPreview => ConvertedVisual != null;

    public string Title
    {
        get => _title;
        private set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public IPreviewProcessor? Processor { get; private set; }

    public PreviewViewModel()
    {
        _refreshManager = new PreviewRefreshManager(this);
    }

    public void LoadFromPath(string path)
    {
        _beatmapPath = path;
        _refreshManager.TriggerRefresh(RefreshTrigger.BeatmapLoaded);
    }

    public void LoadBuiltInSample()
    {
        _beatmapPath = null;
        _refreshManager.TriggerRefresh(RefreshTrigger.BeatmapLoaded);
    }

    public void SetProcessor(IPreviewProcessor? processor)
    {
        var oldProcessor = Processor;
        Processor = processor;

        // 只有当处理器真正改变时才触发刷新
        if (oldProcessor != processor) _refreshManager.TriggerRefresh(RefreshTrigger.ProcessorChanged);
    }

    public void TriggerRefresh(RefreshTrigger trigger)
    {
        _refreshManager.TriggerRefresh(trigger);
    }

    internal void ExecuteRefresh()
    {
        // 每次刷新都重新加载谱面
        Beatmap? beatmap = null;
        if (!string.IsNullOrEmpty(_beatmapPath))
        {
            try
            {
                beatmap = BeatmapDecoder.Decode(_beatmapPath);
            }
            catch
            {
                // Ignore
            }
        }
        if (beatmap == null)
        {
            beatmap = PreviewManiaNote.BuiltInSampleStream();
        }

        // 总是显示原始预览
        var originalVisual = Processor?.BuildOriginalVisual(beatmap);
        OriginalVisual = originalVisual;

        // 如果有处理器，同时显示转换后的预览
        if (Processor != null)
        {
            var convertedVisual = Processor.BuildConvertedVisual(beatmap);
            ConvertedVisual = convertedVisual;
        }
        else
        {
            ConvertedVisual = null;
        }

        UpdateTitle(beatmap);
    }

    public void Reset()
    {
        _beatmapPath = null;
        OriginalVisual = null;
        ConvertedVisual = null;
        Title = string.Empty;
    }

    private void UpdateTitle(Beatmap beatmap)
    {
        if (beatmap.MetadataSection.Title == "Built-in Sample")
        {
            Title = "Built-in Sample";
        }
        else
        {
            var name = beatmap.GetOutputOsuFileName(true);
            Title = $"DIFF: {name}";
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}