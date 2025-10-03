using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using krrTools.Beatmaps;
using krrTools.Localization;
using Microsoft.Extensions.Logging;
using OsuMemoryDataProvider;
using OsuParsers.Decoders;
using Application = System.Windows.Application;

namespace krrTools.Tools.Listener
{
    internal sealed class ListenerViewModel : ObservableObject
    {
        private string _currentOsuFilePath = string.Empty;
        private System.Timers.Timer? _checkTimer; // 明确指定命名空间
#pragma warning disable CS0618 // IOsuMemoryReader is obsolete but kept for compatibility
        // TODO: 过时？是否需要找一个新的内存读取方法替换？
        private readonly IOsuMemoryReader _memoryReader;
#pragma warning restore CS0618
        private string _lastBeatmapId = string.Empty;
        private readonly string _configPath;
        private bool _songsPathAttempted;

        // 记忆路径、热键等配置
        internal ListenerConfig Config { get; }

        // 支持集中保存配置变化
        private bool _suppressConfigSave;

        internal event EventHandler? HotkeyChanged;

        internal event EventHandler<string>? BeatmapSelected;

        internal void SetHotkey(string hotkey)
        {
            Config.Hotkey = hotkey;
        }

        public string WindowTitle = Strings.OSUListener.Localize();
        public string BGPath = string.Empty; // 用于背景图片绑定

        public string CurrentOsuFilePath
        {
            get => _currentOsuFilePath;
            private set
            {
                if (SetProperty(ref _currentOsuFilePath, value))
                {
                    // 当文件路径改变时触发事件
                    if (!string.IsNullOrEmpty(value))
                    {
                        BeatmapSelected?.Invoke(this, value);
                    }
                }
            }
        }

        internal ListenerViewModel()
        {
            // 初始化内存读取器
            // OsuMemoryReader is marked obsolete by the package; suppress the warning for the assignment
#pragma warning disable CS0618
            _memoryReader = OsuMemoryReader.Instance;
#pragma warning restore CS0618

            // 初始化配置对象并订阅变化
            Config = new ListenerConfig();
            Config.PropertyChanged += Config_PropertyChanged;

            // 获取项目根目录并构建配置文件路径
            string projectDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(projectDirectory, "listenerConfig.fq");

            // 尝试加载已保存的配置
            LoadConfig();
            InitializeOsuMonitoring();
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_suppressConfigSave) return;

            // Persist all config changes centrally
            SaveConfig();

            // Raise HotkeyChanged when hotkey property changes
            if (e.PropertyName == nameof(ListenerConfig.Hotkey))
            {
                HotkeyChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // 在 LoadConfig 方法中加载热键
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<ListenerConfig>(json);
                    if (config != null)
                    {
                        _suppressConfigSave = true;

                        if (!string.IsNullOrEmpty(config.SongsPath) && Directory.Exists(config.SongsPath))
                        {
                            Config.SongsPath = config.SongsPath;
                        }

                        if (!string.IsNullOrEmpty(config.Hotkey))
                        {
                            Config.Hotkey = config.Hotkey;
                        }

                        // Assign RealTimePreview directly; missing field will default to false
                        Config.RealTimePreview = config.RealTimePreview;

                        _suppressConfigSave = false;
                    }
                }
            }
            catch (IOException ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to load config (IO): {0}", ex.Message);
            }
            catch (JsonException ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to load config (JSON): {0}", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to load config (unauthorized): {0}", ex.Message);
            }
        }

        internal void SaveConfig()
        {
            try
            {
                // Serialize the Config object directly
                string json = JsonSerializer.Serialize(Config);
                File.WriteAllText(_configPath, json);
            }
            catch (IOException ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to save config (IO): {0}", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to save config (unauthorized): {0}", ex.Message);
            }
            catch (JsonException ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to save config (JSON): {0}", ex.Message);
            }
        }

        // 配置类：现在是一个可观察对象，便于集中订阅变化并保存
        internal class ListenerConfig : ObservableObject
        {

            public string? SongsPath { get; set; }
            public string? Hotkey { get; set; } = "Ctrl+Shift+Alt+X";
            public bool RealTimePreview { get; set; }
        }

        private void InitializeOsuMonitoring()
        {
            try
            {
                // 设置定时检查
                SetupTimer();
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] InitializeOsuMonitoring failed: {0}", ex.Message);
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                }));
            }
        }

        private void SetupTimer()
        {
            _checkTimer = new System.Timers.Timer(1000);
            _checkTimer.Elapsed += (_, _) => CheckOsuBeatmap();
            _checkTimer.Start();
        }

        private void CheckOsuBeatmap()
        {
            try
            {
                var osuProcesses = Process.GetProcessesByName("osu!");
                if (osuProcesses.Length == 0)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        CurrentOsuFilePath = string.Empty;
                    }));
                    return;
                }

                // 自动设置Songs路径
                if (string.IsNullOrEmpty(Config.SongsPath) && !_songsPathAttempted)
                {
                    _songsPathAttempted = true;
                    Process? selectedProcess = null;
                    if (osuProcesses.Length == 1)
                    {
                        selectedProcess = osuProcesses[0];
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var selectionWindow = new ProcessSelectionWindow(osuProcesses);
                            if (selectionWindow.ShowDialog() == true)
                            {
                                selectedProcess = selectionWindow.SelectedProcess;
                            }
                        });
                    }
                    if (selectedProcess != null)
                    {
                        try
                        {
                            if (selectedProcess.MainModule?.FileName is { } exePath)
                            {
                                string? osuDir = Path.GetDirectoryName(exePath);
                                if (osuDir != null)
                                {
                                    string songsPath = Path.Combine(osuDir, "Songs");
                                    if (Directory.Exists(songsPath))
                                    {
                                        Config.SongsPath = songsPath;
                                        Logger.WriteLine(LogLevel.Information, "[ListenerViewModel] 客户端: osu!, 进程ID: {0}, 加载Songs路径: {1}", selectedProcess.Id, songsPath);
                                    }
                                    else
                                    {
                                        Logger.WriteLine(LogLevel.Warning, "[ListenerViewModel] Songs path not found: {0}", songsPath);
                                        Application.Current.Dispatcher.Invoke(SetSongsPath);
                                    }
                                }
                                else
                                {
                                    Application.Current.Dispatcher.Invoke(SetSongsPath);
                                }
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(SetSongsPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] Failed to get songs path: {0}", ex.Message);
                            Application.Current.Dispatcher.Invoke(SetSongsPath);
                        }
                    }
                }

                // 尝试读取当前谱面信息
                var beatmapFile = _memoryReader.GetOsuFileName();
                var mapFolderName = _memoryReader.GetMapFolderName();

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (!string.IsNullOrEmpty(beatmapFile) && !string.IsNullOrEmpty(mapFolderName))
                    {
                        // 只有当谱面改变时才更新
                        if (_lastBeatmapId != beatmapFile)
                        {
                            // 构建完整的文件路径
                            if (!string.IsNullOrEmpty(Config.SongsPath))
                            {
                                CurrentOsuFilePath = Path.Combine(Config.SongsPath, mapFolderName, beatmapFile);

                                // TODO: 监听信息未来统一整理
                                ManiaBeatmap beatmap = BeatmapDecoder.Decode(CurrentOsuFilePath).GetManiaBeatmap();
                                String BG = beatmap.EventsSection.BackgroundImage;
                                if (!string.IsNullOrWhiteSpace(BG))
                                {
                                    BGPath = Path.Combine(Config.SongsPath, mapFolderName, BG);
                                }
                            }
                            else
                            {
                                CurrentOsuFilePath = Path.Combine(mapFolderName, beatmapFile);
                            }
                            _lastBeatmapId = beatmapFile;
                        }
                    }
                    else
                    {
                    }
                }));
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerViewModel] CheckOsuBeatmap failed: {0}", ex.Message);
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                }));
            }
        }

        internal void SetSongsPath()
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Please select the osu! Songs directory",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Config.SongsPath = dialog.SelectedPath;
            }
        }

        internal void Cleanup()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
        }
    }
}
