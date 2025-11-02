using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.Utilities;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace krrTools.Tools.Listener
{
    /// <summary>
    /// 响应式监听器ViewModel - 文件监听和谱面分析的核心控制器
    /// 核心功能：文件变更监听 + 重复处理防护 + 响应式状态管理
    /// </summary>
    public class ListenerViewModel : ReactiveViewModelBase
    {
        private CancellationTokenSource? _monitoringCancellation; // 事件驱动监听的取消令牌

        private Task? _monitoringTask; // 异步监听任务

        private bool _isMonitoringActive; // 标记监听任务是否正在运行
        private bool _hasLoggedNonMania; // 是否已记录非Mania谱面日志
        private int _currentDelayMs = 500; // 当前监听延迟时间，动态调整

        // 公共属性注入事件总线
        [Inject]
        private IEventBus eventBus { get; set; } = null!;

        [Inject]
        private StateBarManager stateBarManager { get; set; } = null!;

        // 服务实例 - 通过依赖注入获取
        [Inject]
        private OsuMonitorService monitorService { get; set; } = null!;

        [Inject]
        private BeatmapAnalysisService analysisService { get; set; } = null!; //分析服务自订阅监听变更

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

        public void SetN2NCHotkey(string hotkey)
        {
            ConfigManager.GetGlobalSettings().N2NCHotkey.Value = hotkey;
        }

        public void SetDPHotkey(string hotkey)
        {
            ConfigManager.GetGlobalSettings().DPHotkey.Value = hotkey;
        }

        public void SetKRRLNHotkey(string hotkey)
        {
            ConfigManager.GetGlobalSettings().KRRLNHotkey.Value = hotkey;
        }

#endregion

        public string WindowTitle = Strings.OSUListener.Localize();

        // 当前文件信息属性 - 使用 Bindable 系统
        public Bindable<string> FilePath { get; set; } = new Bindable<string>(string.Empty);
        public Bindable<string> FileName { get; set; } = new Bindable<string>(string.Empty);
        public Bindable<int> BeatmapID { get; set; } = new Bindable<int>(-1);
        public Bindable<int> BeatmapSetID { get; set; } = new Bindable<int>(-1);
        public Bindable<string> Title { get; set; } = new Bindable<string>(string.Empty);
        public Bindable<string> Artist { get; set; } = new Bindable<string>(string.Empty);
        public Bindable<string> Creator { get; set; } = new Bindable<string>(string.Empty);
        public Bindable<string> Version { get; set; } = new Bindable<string>(string.Empty);
        public Bindable<double> BPM { get; set; } = new Bindable<double>();
        public Bindable<double> OD { get; set; } = new Bindable<double>();
        public Bindable<double> HP { get; set; } = new Bindable<double>();
        public Bindable<double> Keys { get; set; } = new Bindable<double>();
        public Bindable<double> NotesCount { get; set; } = new Bindable<double>();
        public Bindable<double> LNPercent { get; set; } = new Bindable<double>();

        public Bindable<string> Status { get; set; } = new Bindable<string>("Monitoring...");

        // LV 分析相关属性
        public Bindable<double> XxySR { get; set; } = new Bindable<double>();
        public Bindable<double> KrrLV { get; set; } = new Bindable<double>(-1.0);
        public Bindable<double> YlsLV { get; set; } = new Bindable<double>(-1.0);
        public Bindable<double> MaxKPS { get; set; } = new Bindable<double>();
        public Bindable<double> AvgKPS { get; set; } = new Bindable<double>();

        public ObservableCollection<KeyValuePair<string, string>> FileInfoItems { get; } = new ObservableCollection<KeyValuePair<string, string>>();

        public string BGPath { get; set; } = string.Empty;

        public string MonitorOsuFilePath
        {
            get => GlobalSettings.LastPreviewPath.Value;
            private set => GlobalSettings.LastPreviewPath.Value = value;
        }

        public Bindable<bool> N2NCHotkeyConflict { get; } = new Bindable<bool>();
        public Bindable<bool> DPHotkeyConflict { get; } = new Bindable<bool>();
        public Bindable<bool> KRRLNHotkeyConflict { get; } = new Bindable<bool>();

        public ListenerViewModel()
        {
            // 获取全局设置引用
            GlobalSettings = ConfigManager.GetGlobalSettings();

            BrowseCommand = new RelayCommand(SetSongsPathWindow);

            // 订阅分析结果变化事件
            eventBus.Subscribe<AnalysisResultChangedEvent>(OnAnalysisResultChanged);

            // 订阅监听器状态变化
            stateBarManager.ListenerStateBindable.OnValueChanged(OnListenerStateChanged);

            // 设置 Bindable 属性变化通知
            SetupAutoBindableNotifications();
        }

        private void OnListenerStateChanged(ListenerState state)
        {
            switch (state)
            {
                case ListenerState.Monitoring:
                case ListenerState.WaitingForOsu:
                    if (!_isMonitoringActive) StartMonitoringAsync();
                    break;

                case ListenerState.Idle:
                case ListenerState.Stopped:
                    if (_isMonitoringActive) StopMonitoring();
                    break;
            }
        }

        private void StartMonitoringAsync()
        {
            // if (_isMonitoringActive) return Task.CompletedTask;

            _isMonitoringActive = true;
            _currentDelayMs = 500; // 重置延迟
            _monitoringCancellation = new CancellationTokenSource();
            _monitoringTask = Task.Run(async () =>
            {
                Logger.WriteLine(LogLevel.Debug, "[ListenerViewModel] Monitoring task started");

                while (!_monitoringCancellation.Token.IsCancellationRequested)
                {
                    bool detected = CheckOsuBeatmap();
                    _currentDelayMs = detected
                                          ? 500 // 检测到进程，恢复正常频率
                                          : Math.Min(_currentDelayMs * 2, 10000); // 检测失败，逐渐延长延迟，最多10秒

                    await Task.Delay(_currentDelayMs, _monitoringCancellation.Token); // 动态间隔，可取消
                }

                Logger.WriteLine(LogLevel.Debug, "[ListenerViewModel] Monitoring task ended");
            });

            // return Task.CompletedTask;
        }

        private bool CheckOsuBeatmap()
        {
            try
            {
                // var stopwatch = Stopwatch.StartNew();
                monitorService.DetectOsuProcess();

                string monitorFilePath = monitorService.ReadMemoryData();
                bool isMania = BeatmapFileHelper.IsManiaBeatmap(monitorFilePath);

                if (!isMania)
                {
                    if (!_hasLoggedNonMania)
                    {
                        Logger.WriteLine(LogLevel.Critical, "[ListenerViewModel] Skipping non-Mania beatmap: {0}", monitorFilePath);
                        _hasLoggedNonMania = true;
                    }

                    return true; // 进程检测成功，但不是Mania谱面
                }

                _hasLoggedNonMania = false; // 重置标志，以便下次非Mania谱面时记录日志
                // 检查文件路径是否与全局设置中的最后预览路径不同
                GlobalSettings globalSettings = ConfigManager.GetGlobalSettings();

                if (monitorFilePath != globalSettings.LastPreviewPath.Value)
                {
                    ConfigManager.UpdateGlobalSettings(settings => settings.LastPreviewPath.Value = monitorFilePath);

                    // 足够条件确认为新谱面，文件正确，路径安全，发布事件
                    eventBus.Publish(new BeatmapChangedEvent
                    {
                        FilePath = monitorFilePath,
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

            string selectedPath = FilesHelper.ShowFolderBrowserDialog("Please select the osu! Songs directory");

            if (!string.IsNullOrEmpty(selectedPath))
            {
                ConfigManager.GetGlobalSettings().SongsPath.Value = selectedPath;
                _hasSongsPath = true;
            }
        }

        private void StopMonitoring()
        {
            _monitoringCancellation?.Cancel();

            try
            {
                _monitoringTask?.Wait(1000); // 等待最多1秒
            }
            catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
            {
                // 忽略任务取消异常
                Logger.WriteLine(LogLevel.Warning, "[ListenerViewModel] StopMonitoring: Task was canceled, ignoring.");
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] StopMonitoring error: {0}", ex.Message);
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
            OsuAnalysisResult result = analysisEvent.AnalysisResult;

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                FilePath.Value = result.FilePath;
                FileName.Value = result.FileName;
                Status.Value = result.Status;
                Title.Value = result.Title;
                Artist.Value = result.Artist;
                Creator.Value = result.Creator;
                Version.Value = result.Diff;
                Keys.Value = (int)result.KeyCount;
                OD.Value = result.OD;
                HP.Value = result.HP;
                NotesCount.Value = result.NotesCount;
                LNPercent.Value = result.LN_Percent;
                XxySR.Value = result.XXY_SR;
                KrrLV.Value = result.KRR_LV;
                YlsLV.Value = result.YLs_LV;
                MaxKPS.Value = result.MaxKPS;
                AvgKPS.Value = result.AvgKPS;

                // 解析BPM显示字符串
                if (!string.IsNullOrEmpty(result.BPMDisplay))
                {
                    // BPM格式通常是 "180(170-190)"
                    string[] bpmParts = result.BPMDisplay.Split('(');
                    if (double.TryParse(bpmParts[0], out double bpm))
                        BPM.Value = bpm;
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
            FileInfoItems.Add(new KeyValuePair<string, string>("Keys", Keys.Value.ToString(CultureInfo.InvariantCulture)));
            FileInfoItems.Add(new KeyValuePair<string, string>("Notes Count", NotesCount.Value.ToString(CultureInfo.InvariantCulture)));
            FileInfoItems.Add(new KeyValuePair<string, string>("LN Percent", LNPercent.Value.ToString("F2") + "%"));
            FileInfoItems.Add(new KeyValuePair<string, string>("XXY SR", XxySR.Value.ToString("F2")));
            FileInfoItems.Add(new KeyValuePair<string, string>("KRR LV", KrrLV.Value.ToString("F2")));
            FileInfoItems.Add(new KeyValuePair<string, string>("YLS LV", YlsLV.Value.ToString("F2")));
            FileInfoItems.Add(new KeyValuePair<string, string>("Max KPS", MaxKPS.Value.ToString("F2")));
            FileInfoItems.Add(new KeyValuePair<string, string>("Avg KPS", AvgKPS.Value.ToString("F2")));
            FileInfoItems.Add(new KeyValuePair<string, string>("Status", Status.Value));
        }
    }
}
