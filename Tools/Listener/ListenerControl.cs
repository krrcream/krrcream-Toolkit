using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;
using OsuParsers.Decoders;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using CommunityToolkit.Mvvm.Input;
using TextBox = System.Windows.Controls.TextBox;

namespace krrTools.Tools.Listener;

public partial class ListenerControl
{
    private readonly ListenerViewModel _viewModel;
    private readonly object? _sourceWindow;
    private readonly int _sourceId;
    private GlobalHotkey? _globalHotkey;

    private TextBox? _hotkeyTextBox;

    internal static bool IsOpen { get; private set; }

    public RelayCommand ConvertCommand { get; }
    public RelayCommand BrowseCommand { get; }

    public DynamicLocalizedString SongsFolderPathHeader { get; } = new(Strings.SongsFolderPathHeader);
    public DynamicLocalizedString BrowseLabel { get; } = new(Strings.BrowseLabel);
    public DynamicLocalizedString MonitoringInformationHeader { get; } = new(Strings.MonitoringInformationHeader);
    public DynamicLocalizedString CreateMapLabel { get; } = new(Strings.CreateMapLabel);

    public ListenerViewModel ViewModel => _viewModel;

    private bool IsRealTimePreviewEnabled => (Application.Current.MainWindow as MainWindow)?.RealTimePreviewEnabled ?? false;

    internal ListenerControl(object? sourceWindow = null, int sourceId = 0)
    {
        InitializeComponent();
        _viewModel = new ListenerViewModel();
        DataContext = _viewModel;
        _sourceWindow = sourceWindow;
        _sourceId = sourceId;

        ConvertCommand = new RelayCommand(ExecuteConvert);
        BrowseCommand = new RelayCommand(() => _viewModel.SetSongsPath());

        SharedUIComponents.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;

        // 监听热键变化
        _viewModel.HotkeyChanged += (_, _) =>
        {
            UnregisterHotkey();
            Dispatcher.BeginInvoke(new Action(InitializeHotkey));
        };

        // 订阅 BeatmapSelected 事件以便在实时预览开启时把文件推送到预览控件
        _viewModel.BeatmapSelected += ViewModel_BeatmapSelected;
        // 订阅 Config 属性变化以响应 RealTimePreview 开关
        _viewModel.Config.PropertyChanged += ViewModel_ConfigPropertyChanged;
        _viewModel.WindowTitle = Strings.OSUListener.Localize();

        Loaded += (_, _) => 
        {
            InitializeHotkey();
            if (FindName("HotkeyTextBox") is TextBox tb) 
            {
                tb.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
                _hotkeyTextBox = tb;
            }
        };
        Unloaded += Window_Closing;
    }

    private void ExecuteConvert()
    {
        // 检查是否设置了Songs目录且当前有选中的谱面
        if (string.IsNullOrEmpty(_viewModel.Config.SongsPath) && string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
        {
            var message = string.IsNullOrEmpty(_viewModel.Config.SongsPath)
                ? "Please set the Songs directory first."
                : "No beatmap is currently selected.";
            MessageBox.Show(message, Strings.CannotConvert.Localize(), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Get current active tab from MainWindow
        var mainWindow = Application.Current.MainWindow as MainWindow;
        var activeTab = mainWindow?.TabControl.SelectedItem as TabViewItem;
        var activeTag = activeTab?.Tag as string;

        if (string.IsNullOrEmpty(activeTag))
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
                mainWindow?._fileDispatcher.ConvertFiles([_viewModel.CurrentOsuFilePath], activeTag);
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
            mainWindow?._fileDispatcher.ConvertFiles([_viewModel.CurrentOsuFilePath], activeTag);
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control;
        bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift;
        bool alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt;

        System.Windows.Input.Key key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

        if (key == System.Windows.Input.Key.Escape)
            return;

        if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
            key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
            key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt)
            return;

        string hotkey = string.Empty;
        if (ctrl) hotkey += "Ctrl+";
        if (shift) hotkey += "Shift+";
        if (alt) hotkey += "Alt+";

        hotkey += key.ToString();

        _viewModel.SetHotkey(hotkey);
        if (_hotkeyTextBox != null) _hotkeyTextBox.Text = hotkey;
        Console.WriteLine($"Hotkey set to: {hotkey}");
    }

    private void InitializeHotkey()
    {
        try
        {
            var hostWindow = GetWindow(this) ?? Application.Current?.MainWindow;
            if (hostWindow == null)
                throw new InvalidOperationException("ListenerControl must be hosted in a Window to register hotkeys.");
            _globalHotkey = new GlobalHotkey(_viewModel.Config.Hotkey ?? string.Empty, () =>
            {
                // 在UI线程执行转换操作
                Dispatcher.Invoke(ExecuteConvert);
            }, hostWindow);
        }
        catch (Exception ex)
        {
            Logger.WriteLine(LogLevel.Error, "[ListenerControl] Failed to register hotkey: {0}", ex.Message);
            MessageBox.Show(
                Strings.FailedToRegisterHotkey.Localize() + ": " + ex.Message + "\n\nPlease bind a new hotkey.",
                Strings.HotkeyError.Localize(),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UnregisterHotkey()
    {
        _globalHotkey?.Unregister();
        _globalHotkey = null;
    }

    private void Window_Closing(object? sender, RoutedEventArgs e)
    {
        _viewModel.SaveConfig();
        _viewModel.Cleanup();
        UnregisterHotkey();

        IsOpen = false;
    }

    // ViewModel 属性变化处理（关注 RealTimePreview）
    private void ViewModel_ConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Hotkey")
        {
            UnregisterHotkey();
            Dispatcher.BeginInvoke(new Action(InitializeHotkey));
        }
    }

    // Beatmap 被选中时的处理：当实时预览开启则把文件广播到预览並暂存以便转换
    private void ViewModel_BeatmapSelected(object? sender, ListenerViewModel.BeatmapInfo info)
    {
        if (!IsRealTimePreviewEnabled) return; // 只有在实时预览开启时才路由
        if (string.IsNullOrEmpty(info.FilePath)) return;

        try
        {
            if (Application.Current?.MainWindow is MainWindow main)
            {
                // Load to global preview if current tool is a converter
                var selectedTag = (main.TabControl.SelectedItem as TabViewItem)?.Tag;
                if (selectedTag is ConverterEnum)
                {
                    var globalPreview = main.PreviewDualControl;
                    if (globalPreview != null)
                    {
                        var beatmaps = BeatmapDecoder.Decode(info.FilePath);
                        globalPreview.LoadPreview(beatmaps);
                        Console.WriteLine($"尝试加载预览文件: {info.FilePath}");
                    }
                    
                    Console.WriteLine($"{globalPreview}为空");
                }
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
}