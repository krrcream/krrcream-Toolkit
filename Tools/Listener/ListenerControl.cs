using System;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using TextBox = System.Windows.Controls.TextBox;

namespace krrTools.Tools.Listener
{
    public partial class ListenerControl
    {
        private readonly ListenerViewModel _viewModel;
        private ConversionHotkeyManager? _conversionHotkeyManager;

        private TextBox? _n2ncHotkeyTextBox;
        private TextBox? _dpHotkeyTextBox;
        private TextBox? _krrlnHotkeyTextBox;
        private readonly Border? _fileInfoContainer;

        // 用于避免重复加载同一个预览文件的静态变量
        private static string? _lastPreviewFilePath;
        private static DateTime _lastPreviewLoadTime = DateTime.MinValue;

        public RelayCommand ConvertCommand { get; }
        public RelayCommand BrowseCommand { get; }

        public DynamicLocalizedString SongsFolderPathHeader { get; } = new(Strings.SongsFolderPathHeader);
        public DynamicLocalizedString BrowseLabel { get; } = new(Strings.BrowseLabel);
        public DynamicLocalizedString MonitoringInformationHeader { get; } = new(Strings.MonitoringInformationHeader);
        public DynamicLocalizedString CreateMapLabel { get; } = new(Strings.CreateMapLabel);

        public ListenerViewModel ViewModel => _viewModel;

        private bool IsRealTimePreviewEnabled =>
            (Application.Current.MainWindow as MainWindow)?.RealTimePreviewEnabled ?? false;

        internal ListenerControl(IEventBus eventBus)
        {
            InitializeComponent();
            _viewModel = new ListenerViewModel(eventBus);
            DataContext = _viewModel;

            ConvertCommand = new RelayCommand(ExecuteConvert);
            BrowseCommand = new RelayCommand(() => _viewModel.SetSongsPath());

            // Get reference to FileInfoContainer from XAML
            _fileInfoContainer = FindName("FileInfoContainer") as Border;

            // Create DataGrid for file info display
            CreateFileInfoDataGrid();

            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;

            // 订阅 BeatmapSelected 事件以便在实时预览开启时把文件推送到预览控件
            _viewModel.BeatmapSelected += ViewModel_BeatmapSelected;
            // 订阅 Config 属性变化以响应 RealTimePreview 开关
            _viewModel.Config.PropertyChanged += ViewModel_ConfigPropertyChanged;
            _viewModel.WindowTitle = Strings.OSUListener.Localize();

            Loaded += (_, _) =>
            {
                InitializeConversionHotkeys();
                if (FindName("N2NCHotkeyTextBox") is TextBox n2ncTb)
                {
                    n2ncTb.PreviewKeyDown += N2NCHotkeyTextBox_PreviewKeyDown;
                    _n2ncHotkeyTextBox = n2ncTb;
                }

                if (FindName("DPHotkeyTextBox") is TextBox dpTb)
                {
                    dpTb.PreviewKeyDown += DPHotkeyTextBox_PreviewKeyDown;
                    _dpHotkeyTextBox = dpTb;
                }

                if (FindName("KRRLNHotkeyTextBox") is TextBox krrlnTb)
                {
                    krrlnTb.PreviewKeyDown += KRRLNHotkeyTextBox_PreviewKeyDown;
                    _krrlnHotkeyTextBox = krrlnTb;
                }
            };
            Unloaded += Window_Closing;
        }

        private void ExecuteConvert()
        {
            // 检查是否设置了Songs目录且当前有选中的谱面
            if (string.IsNullOrEmpty(BaseOptionsManager.GetGlobalSettings().SongsPath) &&
                string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
            {
                var message = string.IsNullOrEmpty(BaseOptionsManager.GetGlobalSettings().SongsPath)
                    ? "Please set the Songs directory first."
                    : "No beatmap is currently selected.";
                MessageBox.Show(message, Strings.CannotConvert.Localize(), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var mainWindow = Application.Current.MainWindow as MainWindow;

            if (mainWindow == null)
            {
                MessageBox.Show(Strings.NoActiveTabSelected.Localize(), Strings.CannotConvert.Localize(),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // If RealTimePreview is on, convert the current file
            try
            {
                if (IsRealTimePreviewEnabled && !string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
                {
                    mainWindow.FileDropZoneViewModel?.SetFiles([_viewModel.CurrentOsuFilePath],
                        source: FileDropZoneViewModel.FileSource.Listened);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error,
                    "[ListenerControl] Error while attempting to process current file: " + ex.Message);
                // fall through to single-file behavior
            }

            // Fallback/single-file behavior: process the current selected osu file
            if (!string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
                mainWindow.FileDropZoneViewModel?.SetFiles([_viewModel.CurrentOsuFilePath],
                    source: FileDropZoneViewModel.FileSource.Listened);
        }

        private void N2NCHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            var ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) ==
                       System.Windows.Input.ModifierKeys.Control;
            var shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) ==
                        System.Windows.Input.ModifierKeys.Shift;
            var alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) ==
                      System.Windows.Input.ModifierKeys.Alt;

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

            if (key == System.Windows.Input.Key.Escape)
                return;

            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt)
                return;

            var hotkey = string.Empty;
            if (ctrl) hotkey += "Ctrl+";
            if (shift) hotkey += "Shift+";
            if (alt) hotkey += "Alt+";

            hotkey += key.ToString();

            _viewModel.SetN2NCHotkey(hotkey);
            if (_n2ncHotkeyTextBox != null) _n2ncHotkeyTextBox.Text = hotkey;
            Console.WriteLine($"N2NC Hotkey set to: {hotkey}");
        }

        private void DPHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            var ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) ==
                       System.Windows.Input.ModifierKeys.Control;
            var shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) ==
                        System.Windows.Input.ModifierKeys.Shift;
            var alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) ==
                      System.Windows.Input.ModifierKeys.Alt;

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

            if (key == System.Windows.Input.Key.Escape)
                return;

            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt)
                return;

            var hotkey = string.Empty;
            if (ctrl) hotkey += "Ctrl+";
            if (shift) hotkey += "Shift+";
            if (alt) hotkey += "Alt+";

            hotkey += key.ToString();

            _viewModel.SetDPHotkey(hotkey);
            if (_dpHotkeyTextBox != null) _dpHotkeyTextBox.Text = hotkey;
            Console.WriteLine($"DP Hotkey set to: {hotkey}");
        }

        private void KRRLNHotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;

            var ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) ==
                       System.Windows.Input.ModifierKeys.Control;
            var shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) ==
                        System.Windows.Input.ModifierKeys.Shift;
            var alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) ==
                      System.Windows.Input.ModifierKeys.Alt;

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

            if (key == System.Windows.Input.Key.Escape)
                return;

            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt)
                return;

            var hotkey = string.Empty;
            if (ctrl) hotkey += "Ctrl+";
            if (shift) hotkey += "Shift+";
            if (alt) hotkey += "Alt+";

            hotkey += key.ToString();

            _viewModel.SetKRRLNHotkey(hotkey);
            if (_krrlnHotkeyTextBox != null) _krrlnHotkeyTextBox.Text = hotkey;
            Console.WriteLine($"KRRLN Hotkey set to: {hotkey}");
        }

        private void Window_Closing(object? sender, RoutedEventArgs e)
        {
            _viewModel.Cleanup();
            _conversionHotkeyManager?.Dispose();
        }

        // ViewModel 属性变化处理（关注 RealTimePreview）
        private void ViewModel_ConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 不再需要处理主快捷键变化
        }

        // Beatmap 被选中时的处理：当实时预览开启则把文件广播到预览並暂存以便转换
        private void ViewModel_BeatmapSelected(object? sender, ListenerViewModel.BeatmapInfo info)
        {
            if (!IsRealTimePreviewEnabled) return; // 只有在实时预览开启时才路由
            if (string.IsNullOrEmpty(info.FilePath)) return;

            // 避免重复加载同一个文件或短时间内频繁加载
            var now = DateTime.Now;
            if (_lastPreviewFilePath == info.FilePath && (now - _lastPreviewLoadTime).TotalSeconds < 0.5) return;
            _lastPreviewFilePath = info.FilePath;
            _lastPreviewLoadTime = now;

            try
            {
                if (Application.Current?.MainWindow is MainWindow main)
                {
                    // Load to global preview if current tool is a converter
                    main.FileDropZoneViewModel?.SetFiles([info.FilePath],
                        source: FileDropZoneViewModel.FileSource.Listened);
                    Console.WriteLine($"尝试加载预览文件: {info.FilePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerControl] Listener preview broadcast failed: {0}", ex.Message);
            }
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => { _viewModel.WindowTitle = Strings.OSUListener; }));
        }

        private void InitializeConversionHotkeys()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _conversionHotkeyManager = new ConversionHotkeyManager(
                ExecuteConvertWithModule,
                mainWindow
            );

            // 注册快捷键
            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            _conversionHotkeyManager.RegisterHotkeys(globalSettings);
        }

        private void ExecuteConvertWithModule(ConverterEnum converter)
        {
            // 检查是否设置了Songs目录且当前有选中的谱面
            if (string.IsNullOrEmpty(BaseOptionsManager.GetGlobalSettings().SongsPath) &&
                string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
            {
                var message = string.IsNullOrEmpty(BaseOptionsManager.GetGlobalSettings().SongsPath)
                    ? "Please set the Songs directory first."
                    : "No beatmap is currently selected.";
                MessageBox.Show(message, Strings.CannotConvert.Localize(), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 使用指定的转换模块
            try
            {
                if (Application.Current.MainWindow is MainWindow mainWindow && IsRealTimePreviewEnabled && !string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
                {
                    mainWindow.FileDropZoneViewModel?.SetFiles([_viewModel.CurrentOsuFilePath],
                        source: FileDropZoneViewModel.FileSource.Listened);
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error,
                    "[ListenerControl] Error while attempting to process current file: " + ex.Message);
            }

            // Fallback: process the current selected osu file
            if (!string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                    mainWindow.FileDropZoneViewModel?.SetFiles([_viewModel.CurrentOsuFilePath],
                        source: FileDropZoneViewModel.FileSource.Listened);
            }
        }

        private void CreateFileInfoDataGrid()
        {
            if (_fileInfoContainer == null) return;

            // 创建一个简单的StackPanel来显示当前文件信息
            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Margin = new Thickness(10)
            };

            // 先创建静态内容测试UI是否工作
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "=== DEBUG: UI Container Working ===",
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = System.Windows.Media.Brushes.Red
            });

            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Title: Static Test Title",
                FontSize = 14
            });

            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Artist: Static Test Artist",
                FontSize = 14
            });

            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Keys: 7",
                FontSize = 14
            });

            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "XXY SR: 12.345",
                FontSize = 14
            });

            stackPanel.Children.Add(new System.Windows.Controls.TextBlock 
            { 
                Text = "Status: UI Test Working",
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.Green
            });

            // Add to container
            _fileInfoContainer.Child = stackPanel;
        }
    }
}