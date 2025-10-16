using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;
using krrTools.Utilities;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace krrTools.Tools.Listener
{
    public partial class ListenerControl
    {
        private ConversionHotkeyManager? _conversionHotkeyManager;
        private readonly BeatmapTransformationService _transformationService;

        public RelayCommand BrowseCommand { get; }

        private readonly ListenerViewModel _viewModel;
        public ListenerViewModel ViewModel => _viewModel;

        private readonly FileDropZoneViewModel _dropZoneViewModel;
        public FileDropZoneViewModel DropZoneViewModel => _dropZoneViewModel;

        private readonly IEventBus _eventBus;

        internal ListenerControl()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Constructor called");

            _eventBus = App.Services.GetService(typeof(IEventBus)) as IEventBus ?? throw new InvalidOperationException("EventBus not found");

            InitializeComponent();
            _viewModel = new ListenerViewModel();
            DataContext = _viewModel;

            // 初始化转换服务
            var moduleManager = App.Services.GetService(typeof(IModuleManager)) as IModuleManager;
            _transformationService = new BeatmapTransformationService(moduleManager!);

            // 初始化拖拽区 ViewModel
            var fileDispatcher = new FileDispatcher();
            _dropZoneViewModel = new FileDropZoneViewModel(fileDispatcher);
            
            BrowseCommand = new RelayCommand(() => _viewModel.SetSongsPathWindow());

            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;

            // 订阅监听状态变化
            _eventBus.Subscribe<MonitoringEnabledChangedEvent>(OnMonitoringEnabledChanged);

            _viewModel.WindowTitle = Strings.OSUListener.Localize();

            // 添加快捷键编辑事件
            N2NCHotkeyTextBox.KeyDown += OnHotkeyKeyDown;
            N2NCHotkeyTextBox.GotFocus += OnHotkeyTextBoxGotFocus;
            N2NCHotkeyTextBox.LostFocus += OnHotkeyTextBoxLostFocus;

            DPHotkeyTextBox.KeyDown += OnHotkeyKeyDown;
            DPHotkeyTextBox.GotFocus += OnHotkeyTextBoxGotFocus;
            DPHotkeyTextBox.LostFocus += OnHotkeyTextBoxLostFocus;

            KRRLNHotkeyTextBox.KeyDown += OnHotkeyKeyDown;
            KRRLNHotkeyTextBox.GotFocus += OnHotkeyTextBoxGotFocus;
            KRRLNHotkeyTextBox.LostFocus += OnHotkeyTextBoxLostFocus;

            Loaded += (_, _) =>
            {
                Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Loaded event fired");
                InitializeConversionHotkeys();
            };
            Unloaded += Window_Closing;
        }

        private void Window_Closing(object? sender, RoutedEventArgs e)
        {
            _conversionHotkeyManager?.Dispose();
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => { _viewModel.WindowTitle = Strings.OSUListener; }));
        }

        private void InitializeConversionHotkeys()
        {
            var mainWindow = Application.Current.MainWindow;
            Logger.WriteLine(LogLevel.Debug, $"[ListenerControl] InitializeConversionHotkeys called, MainWindow: {mainWindow}, Handle: {mainWindow?.GetType().Name}");

            if (mainWindow == null)
            {
                Logger.WriteLine(LogLevel.Error, "[ListenerControl] MainWindow is null, cannot initialize hotkeys");
                return;
            }

            Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Initializing conversion hotkeys");

            _conversionHotkeyManager = new ConversionHotkeyManager(
                ExecuteConvertWithModule,
                mainWindow
            );

            // 只在监听启用时注册快捷键
            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            if (globalSettings.MonitoringEnable.Value)
            {
                _conversionHotkeyManager.RegisterHotkeys(globalSettings);
                UpdateHotkeyConflicts();
                Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Hotkeys registered during initialization");
            }
            else
            {
                Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Hotkeys not registered during initialization (monitoring disabled)");
            }

            Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Conversion hotkeys initialized");
        }

        private void UpdateHotkeyConflicts()
        {
            if (_conversionHotkeyManager == null) return;

            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            var conflicts = _conversionHotkeyManager.CheckHotkeyConflicts(globalSettings);

            _viewModel.N2NCHotkeyConflict.Value = conflicts[ConverterEnum.N2NC];
            _viewModel.DPHotkeyConflict.Value = conflicts[ConverterEnum.DP];
            _viewModel.KRRLNHotkeyConflict.Value = conflicts[ConverterEnum.KRRLN];
        }

        private void ExecuteConvertWithModule(ConverterEnum converter)
        {
            Logger.WriteLine(LogLevel.Information, $"[ListenerControl] EXECUTE CONVERT: {converter} triggered by hotkey");

            // 获取当前谱面
            string beatmapPath = _viewModel.MonitorOsuFilePath;

            try
            {
                // 解码谱面
                var beatmap = OsuParsers.Decoders.BeatmapDecoder.Decode(beatmapPath);

                // 使用转换服务
                var transformedBeatmap = _transformationService.TransformBeatmap(beatmap, converter);

                // 保存转换后谱面
                var outputPath = transformedBeatmap!.GetOutputOsuFileName();
                var outputDir = Path.GetDirectoryName(beatmapPath);
                var fullOutputPath = Path.Combine(outputDir!, outputPath);
                transformedBeatmap!.Save(fullOutputPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Conversion failed: {ex.Message}", Strings.CannotConvert.Localize(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            // 阻止默认行为
            e.Handled = true;

            // 获取按键组合
            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            // 忽略单独的修饰键和输入法处理键
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.System || key == Key.ImeProcessed) return;

            // 构建快捷键字符串
            var hotkeyParts = new List<string>();

            if ((modifiers & ModifierKeys.Control) != 0) hotkeyParts.Add("Ctrl");
            if ((modifiers & ModifierKeys.Shift) != 0) hotkeyParts.Add("Shift");
            if ((modifiers & ModifierKeys.Alt) != 0) hotkeyParts.Add("Alt");

            // 转换键为字符串
            var keyString = KeyToString(key);
            if (!string.IsNullOrEmpty(keyString))
            {
                hotkeyParts.Add(keyString);
            }

            var hotkey = string.Join("+", hotkeyParts);

            // 根据TextBox设置对应的快捷键
            if (textBox == N2NCHotkeyTextBox)
            {
                _viewModel.SetN2NCHotkey(hotkey);
                textBox.Text = hotkey;
            }
            else if (textBox == DPHotkeyTextBox)
            {
                _viewModel.SetDPHotkey(hotkey);
                textBox.Text = hotkey;
            }
            else if (textBox == KRRLNHotkeyTextBox)
            {
                _viewModel.SetKRRLNHotkey(hotkey);
                textBox.Text = hotkey;
            }

            // 重新注册快捷键
            ReinitializeHotkeys();

            Logger.WriteLine(LogLevel.Debug, $"[ListenerControl] Set hotkey to: {hotkey}");
        }

        private string KeyToString(Key key)
        {
            // 处理特殊键
            switch (key)
            {
                case Key.OemPlus: return "+";
                case Key.OemMinus: return "-";
                case Key.OemQuestion: return "/";
                case Key.OemPeriod: return ".";
                case Key.OemComma: return ",";
                case Key.OemSemicolon: return ";";
                case Key.OemQuotes: return "'";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemBackslash: return "\\";
                case Key.OemTilde: return "`";
                default: return key.ToString();
            }
        }

        private void ReinitializeHotkeys()
        {
            // 注销现有快捷键
            _conversionHotkeyManager?.UnregisterAllHotkeys();

            // 重新注册
            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            _conversionHotkeyManager?.RegisterHotkeys(globalSettings);

            // 更新冲突状态
            UpdateHotkeyConflicts();
        }

        private void OnMonitoringEnabledChanged(MonitoringEnabledChangedEvent evt)
        {
            // 确保快捷键管理器已初始化
            if (_conversionHotkeyManager == null)
            {
                Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Initializing hotkey manager in OnMonitoringEnabledChanged");
                InitializeConversionHotkeys();
            }

            if (evt.NewValue)
            {
                // 监听启用，注册快捷键
                var globalSettings = BaseOptionsManager.GetGlobalSettings();
                _conversionHotkeyManager?.RegisterHotkeys(globalSettings);
                UpdateHotkeyConflicts();
                Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Hotkeys registered because monitoring enabled");
            }
            else
            {
                // 监听禁用，注销快捷键
                _conversionHotkeyManager?.UnregisterAllHotkeys();
                Logger.WriteLine(LogLevel.Debug, "[ListenerControl] Hotkeys unregistered because monitoring disabled");
            }
        }

        private void OnHotkeyTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 当TextBox获得焦点时，禁用输入法以避免ImeProcessed问题
                InputMethod.SetIsInputMethodEnabled(textBox, false);

                Logger.WriteLine(LogLevel.Debug, $"[ListenerControl] Hotkey TextBox got focus: {textBox.Name}, IME disabled");
            }
        }

        private void OnHotkeyTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 当TextBox失去焦点时，重新启用输入法
                InputMethod.SetIsInputMethodEnabled(textBox, true);

                Logger.WriteLine(LogLevel.Debug, $"[ListenerControl] Hotkey TextBox lost focus: {textBox.Name}, IME re-enabled");
            }
        }

        private void TestN2NCButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.WriteLine(LogLevel.Information, "[ListenerControl] TEST BUTTON: Manual trigger N2NC conversion");
            ExecuteConvertWithModule(ConverterEnum.N2NC);
        }
    }
}