using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.Utilities;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace krrTools.Tools.Listener;

/// <summary>
/// 响应式监听器ViewModel - 文件监听和谱面分析的核心控制器
/// 核心功能：文件变更监听 + 重复处理防护 + 响应式状态管理
/// </summary>
public class ListenerViewModel : ReactiveViewModelBase
{
    private CancellationTokenSource? _monitoringCancellation; // 事件驱动监听的取消令牌
    private Task? _monitoringTask; // 异步监听任务
    private bool _isMonitoringActive; // 标记监听任务是否正在运行
    private int _currentDelayMs = 500; // 当前监听延迟时间，动态调整

    // 公共属性注入事件总线
    [Inject] private IEventBus EventBus { get; set; } = null!;
    [Inject] private StateBarManager StateBarManager { get; set; } = null!;

    // 服务实例
    private readonly OsuMonitorService _monitorService;
    private readonly BeatmapAnalysisService _analysisService;

    // 全局设置引用
    public GlobalSettings GlobalSettings { get; }

#region 包装属性，XAML数据绑定
    public string N2NCHotkey
    {
        get => GlobalSettings.N2NCHotkey.Value;
        set => GlobalSettings.N2NCHotkey.Value = value;
    }

    public string DPHotkey
    {
        get => GlobalSettings.DPHotkey.Value;
        set => GlobalSettings.DPHotkey.Value = value;
    }

    public string KRRLNHotkey
    {
        get => GlobalSettings.KRRLNHotkey.Value;
        set => GlobalSettings.KRRLNHotkey.Value = value;
    }

    public string SongsPath
    {
        get => GlobalSettings.SongsPath.Value;
        set => GlobalSettings.SongsPath.Value = value;
    }

    public RelayCommand BrowseCommand { get; }

    public void SetN2NCHotkey(string hotkey) => BaseOptionsManager.GetGlobalSettings().N2NCHotkey.Value = hotkey;

    public void SetDPHotkey(string hotkey) => BaseOptionsManager.GetGlobalSettings().DPHotkey.Value = hotkey;
    
    public void SetKRRLNHotkey(string hotkey) => BaseOptionsManager.GetGlobalSettings().KRRLNHotkey.Value = hotkey;
    
#endregion
    
    public string WindowTitle = Strings.OSUListener.Localize();

    // 当前文件信息属性 - 使用 Bindable 系统
    public Bindable<string> Title { get; set; } = new(string.Empty);
    public Bindable<string> Artist { get; set; } = new(string.Empty);
    public Bindable<string> Creator { get; set; } = new(string.Empty);
    public Bindable<string> Version { get; set; } = new(string.Empty);
    public Bindable<double> BPM { get; set; } = new();
    public Bindable<double> OD { get; set; } = new();
    public Bindable<double> HP { get; set; } = new();
    public Bindable<int> Keys { get; set; } = new();
    public Bindable<int> NotesCount { get; set; } = new();
    public Bindable<double> LNPercent { get; set; } = new();
    public Bindable<string> Status { get; set; } = new("Monitoring...");
    // LV 分析相关属性
    public Bindable<double> XxySR { get; set; } = new();
    public Bindable<double> KrrLV { get; set; } = new();
    public Bindable<double> YlsLV { get; set; } = new();
    public Bindable<double> MaxKPS { get; set; } = new();
    public Bindable<double> AvgKPS { get; set; } = new();

    public ObservableCollection<KeyValuePair<string, string>> FileInfoItems { get; } = new();

    public string BGPath { get; set; } = string.Empty;

    public string MonitorOsuFilePath
    {
        get => GlobalSettings.LastPreviewPath.Value;
        private set => GlobalSettings.LastPreviewPath.Value = value;
    }

    public ListenerViewModel()
    {
        // 初始化服务
        _monitorService = new OsuMonitorService();
        _analysisService = new BeatmapAnalysisService();

        // 获取全局设置引用
        GlobalSettings = BaseOptionsManager.GetGlobalSettings();

        BrowseCommand = new RelayCommand(SetSongsPathWindow);

        // 订阅分析结果变化事件
        EventBus.Subscribe<AnalysisResultChangedEvent>(OnAnalysisResultChanged);

        // 订阅监听器状态变化
        StateBarManager.ListenerState.OnValueChanged(OnListenerStateChanged);

        // 设置 Bindable 属性变化通知
        SetupAutoBindableNotifications();
    }



    private void OnListenerStateChanged(ListenerState state)
    {
        switch (state)
        {
            case ListenerState.Monitoring:
            case ListenerState.WaitingForOsu:
                if (!_isMonitoringActive)
                {
                    StartMonitoringAsync();
                }
                break;
            case ListenerState.Idle:
            case ListenerState.Stopped:
                if (_isMonitoringActive)
                {
                    StopMonitoring();
                }
                break;
        }
    }

    public Task StartMonitoringAsync()
    {
        if (_isMonitoringActive) return Task.CompletedTask;

        _isMonitoringActive = true;
        _currentDelayMs = 500; // 重置延迟
        _monitoringCancellation = new CancellationTokenSource();
        _monitoringTask = Task.Run(async () =>
        {
            Console.WriteLine("[ListenerViewModel] Monitoring task started");
            while (!_monitoringCancellation.Token.IsCancellationRequested)
            {
                var detected = CheckOsuBeatmap();
                _currentDelayMs = detected
                    ? 500 // 检测到进程，恢复正常频率
                    : Math.Min(_currentDelayMs * 2, 10000); // 检测失败，逐渐延长延迟，最多10秒

                await Task.Delay(_currentDelayMs, _monitoringCancellation.Token); // 动态间隔，可取消
            }

            Console.WriteLine("[ListenerViewModel] Monitoring task ended");
        });

        return Task.CompletedTask;
    }

    private bool CheckOsuBeatmap()
    {
        try
        {
            // var stopwatch = Stopwatch.StartNew();
            _monitorService.DetectOsuProcess();

            var monitorFilePath = _monitorService.ReadMemoryData();
            var isMania = _analysisService.IsManiaBeatmapQuickCheck(monitorFilePath);

            if (!isMania)
            {
                Logger.WriteLine(LogLevel.Debug, "[ListenerViewModel] Skipping non-Mania beatmap: {0}",
                    monitorFilePath);
                return true; // 进程检测成功，但不是Mania谱面
            }

            // 检查文件路径是否与全局设置中的最后预览路径不同
            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            if (monitorFilePath != globalSettings.LastPreviewPath.Value)
            {
                // 足够条件确认为新谱面，文件正确，路径安全，发布事件
                EventBus.Publish(new BeatmapChangedEvent
                {
                    FilePath = monitorFilePath,
                    FileName = Path.GetFileName(monitorFilePath),
                    ChangeType = BeatmapChangeType.FromMonitoring
                });

                // 更新UI显示
                Application.Current?.Dispatcher?.Invoke(() => { MonitorOsuFilePath = monitorFilePath; });
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] CheckOsuBeatmap failed: {0}", ex.Message);
            return false;
        }
    }

    private bool _hasSongsPath;

    public void SetSongsPathWindow()
    {
        if (_hasSongsPath) return; // 如果已经弹出过窗口，直接返回

        var dialog = new FolderBrowserDialog
        {
            SelectedPath = BaseOptionsManager.GetGlobalSettings().SongsPath.Value,
            Description = "Please select the osu! Songs directory"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            BaseOptionsManager.GetGlobalSettings().SongsPath.Value = dialog.SelectedPath;
            _hasSongsPath = true;
        }
    }

    public void StopMonitoring()
    {
        _monitoringCancellation?.Cancel();
        try
        {
            _monitoringTask?.Wait(1000); // 等待最多1秒
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
        {
            // 忽略任务取消异常
            Console.WriteLine("[ListenerViewModel] StopMonitoring: Task was canceled, ignoring.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ListenerViewModel] StopMonitoring error: {ex.Message}");
        }

        _monitoringCancellation?.Dispose();
        _monitoringCancellation = null;
        _monitoringTask = null;
        _isMonitoringActive = false;
    }

    /// <summary>
    /// 处理分析结果变化事件，更新UI显示
    /// </summary>
    private void OnAnalysisResultChanged(AnalysisResultChangedEvent analysisEvent)
    {
        var result = analysisEvent.AnalysisResult;

        Application.Current?.Dispatcher?.Invoke(() =>
        {
            Title.Value = result.Title ?? string.Empty;
            Artist.Value = result.Artist ?? string.Empty;
            Creator.Value = result.Creator ?? string.Empty;
            Version.Value = result.Diff ?? string.Empty;
            Keys.Value = (int)result.Keys;
            OD.Value = result.OD;
            HP.Value = result.HP;
            NotesCount.Value = result.NotesCount;
            LNPercent.Value = result.LNPercent;
            XxySR.Value = result.XXY_SR;
            KrrLV.Value = result.KRR_LV;
            YlsLV.Value = OsuAnalyzer.CalculateYlsLevel(result.XXY_SR);
            MaxKPS.Value = result.MaxKPS;
            AvgKPS.Value = result.AvgKPS;
            Status.Value = "Analyzed";

            // 解析BPM显示字符串
            if (!string.IsNullOrEmpty(result.BPMDisplay))
            {
                // BPM格式通常是 "180(170-190)"
                var bpmParts = result.BPMDisplay.Split('(');
                if (double.TryParse(bpmParts[0], out var bpm)) BPM.Value = bpm;
            }

            // 更新文件信息项
            UpdateFileInfoItems();
        });
    }

    private void UpdateFileInfoItems()
    {
        FileInfoItems.Clear();
        FileInfoItems.Add(new KeyValuePair<string, string>("Title", Title.Value));
        FileInfoItems.Add(new KeyValuePair<string, string>("Artist", Artist.Value));
        FileInfoItems.Add(new KeyValuePair<string, string>("Creator", Creator.Value));
        FileInfoItems.Add(new KeyValuePair<string, string>("Version", Version.Value));
        FileInfoItems.Add(new KeyValuePair<string, string>("BPM", BPM.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("OD", OD.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("HP", HP.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("Keys", Keys.Value.ToString()));
        FileInfoItems.Add(new KeyValuePair<string, string>("Notes Count", NotesCount.Value.ToString()));
        FileInfoItems.Add(new KeyValuePair<string, string>("LN Percent", LNPercent.Value.ToString("F2") + "%"));
        FileInfoItems.Add(new KeyValuePair<string, string>("XXY SR", XxySR.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("KRR LV", KrrLV.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("YLS LV", YlsLV.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("Max KPS", MaxKPS.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("Avg KPS", AvgKPS.Value.ToString("F2")));
        FileInfoItems.Add(new KeyValuePair<string, string>("Status", Status.Value));
    }
}