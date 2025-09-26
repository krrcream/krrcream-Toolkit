﻿using System;
using System.Diagnostics;
using OsuMemoryDataProvider;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using Application = System.Windows.Application;
using krrTools.tools.DPtool; // for ObservableObject

namespace krrTools.tools.Listener
{
    internal sealed class ListenerViewModel : ObservableObject
    {
        private string _currentOsuFilePath = string.Empty;
        private string _statusMessage = string.Empty;
        private System.Timers.Timer? _checkTimer; // 明确指定命名空间
#pragma warning disable CS0618 // IOsuMemoryReader is obsolete but kept for compatibility
        private readonly IOsuMemoryReader _memoryReader;
#pragma warning restore CS0618
        private string _lastBeatmapId = string.Empty;
        private string _bgPath = string.Empty;
        private string _windowTitle = "osu!Listener";
        private readonly string _configPath;

        // Config object centralizes related settings (SongsPath, Hotkey, RealTimePreview)
        internal ListenerConfig Config { get; }

        // Suppress config saving during load to avoid unnecessary writes or recursion
        private bool _suppressConfigSave;

        internal event EventHandler? HotkeyChanged;

        internal event EventHandler<string>? BeatmapSelected;

        internal void SetHotkey(string hotkey)
        {
            Config.Hotkey = hotkey;
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }
        public string BGPath
        {
            get => _bgPath;
            set => SetProperty(ref _bgPath, value);
        }

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

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
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
                Debug.WriteLine($"Failed to load config (IO): {ex.Message}");
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to load config (JSON): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Failed to load config (unauthorized): {ex.Message}");
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
                Debug.WriteLine($"Failed to save config (IO): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Failed to save config (unauthorized): {ex.Message}");
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to save config (JSON): {ex.Message}");
            }
        }
    
        // 配置类：现在是一个可观察对象，便于集中订阅变化并保存
        internal class ListenerConfig : ObservableObject
          {
              private string? _songsPath;
              private string? _hotkey = "Ctrl+Shift+Alt+X";
              private bool _realTimePreview;

              public string? SongsPath { get => _songsPath; set => SetProperty(ref _songsPath, value); }
              public string? Hotkey { get => _hotkey; set => SetProperty(ref _hotkey, value); }
              public bool RealTimePreview { get => _realTimePreview; set => SetProperty(ref _realTimePreview, value); }
          }
        
        private void InitializeOsuMonitoring()
        {
            try
            {
                StatusMessage = "Monitoring osu! song selection...";
                
                // 设置定时检查
                SetupTimer();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeOsuMonitoring failed: {ex.Message}");
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() => StatusMessage = "Initialization failed"));
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
                        StatusMessage = "osu! is not running";
                        CurrentOsuFilePath = string.Empty;
                    }));
                    return;
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

                                string Mes = $"Detected selected beatmap:\n{beatmapFile}\n" +
                                             "\n" + $"OD:{_memoryReader.GetMapOd()}" + 
                                             "\n" + $"HP:{_memoryReader.GetMapHp()}" + 
                                              "\n" + $"CS:{_memoryReader.GetMapCs()}";
                                
                                StatusMessage = Mes;
                                
                                Beatmap beatmap = BeatmapDecoder.Decode(CurrentOsuFilePath);
                                String BG = beatmap.EventsSection.BackgroundImage;
                                if (!string.IsNullOrWhiteSpace(BG))
                                {
                                    BGPath = Path.Combine(Config.SongsPath, mapFolderName, BG);
                                }
                            }
                            else
                            {
                                CurrentOsuFilePath = Path.Combine(mapFolderName, beatmapFile);
                                StatusMessage = "Please set Songs directory for full path";
                            }
                            _lastBeatmapId = beatmapFile;
                        }
                    }
                    else
                    {
                        StatusMessage = "osu! is running, waiting for beatmap selection...";
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckOsuBeatmap failed: {ex.Message}");
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() => StatusMessage = "Failed to read osu! memory"));
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
                StatusMessage = $"Songs directory set: {Config.SongsPath}";
            }
        }

          internal void Cleanup()
          {
              _checkTimer?.Stop();
              _checkTimer?.Dispose();
          }
      }
  }
