using System;
using System.Diagnostics;
using System.Windows;
using krrTools.tools.DPtool;

namespace krrTools.tools.Listener
{
    public partial class ListenerView
    {
        private readonly ListenerViewModel _viewModel;
        private object? _sourceWindow; // 保存打开此窗口的源窗口实例
        private int _sourceId; // 源窗口标识符
        private GlobalHotkey? _globalHotkey;
        
        public ListenerView(object? sourceWindow = null, int sourceId = 0)
        {
            InitializeComponent();
            _viewModel = new ListenerViewModel();
            DataContext = _viewModel;
            _sourceWindow = sourceWindow;
            _sourceId = sourceId;
    
            // 监听热键变化
            _viewModel.HotkeyChanged += (s, e) => {
                UnregisterHotkey();
                Dispatcher.BeginInvoke(InitializeHotkey);
            };
            
            
            // 根据源窗口ID设置标题
            switch (_sourceId)
            {
                case 1:
                    _viewModel.WindowTitle = "Any Keys Converter";
                    Title = "osu!Listener - Any Keys Converter";
                    break;
                case 2:
                    _viewModel.WindowTitle = "LN Transformer";
                    Title = "osu!Listener - LN Transformer";
                    break;
                case 3:
                    _viewModel.WindowTitle = "DP Tool";
                    Title = "osu!Listener - DP Tool";
                    break;
                default:
                    _viewModel.WindowTitle = "osu!Listener";
                    Title = "osu!Listener";
                    break;
            }
        }


        private void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            _viewModel.SetSongsPath();
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _viewModel.SaveConfig();
            _viewModel.Cleanup();
            UnregisterHotkey();
        }
        
        private void ConvertButton_Click(object? sender, RoutedEventArgs? e)
        {
            // 检查是否设置了Songs目录且当前有选中的谱面
            if (!string.IsNullOrEmpty(_viewModel.SongsPath) && !string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
            {
                // 根据源窗口类型调用相应的处理方法
                switch (_sourceId)
                {
                    case 1: // Converter
                        if (_sourceWindow is Tools.Converter.ConverterWindow converterWindow)
                        {
                            converterWindow.ProcessSingleFile(_viewModel.CurrentOsuFilePath);
                        }
                        break;
                    case 2: // LN Transformer
                        if (_sourceWindow is krrTools.tools.LNTransformer.LNTransformer lnTransformerWindow)
                        {
                            // LN Transformer的处理逻辑
                            lnTransformerWindow.ProcessSingleFile(_viewModel.CurrentOsuFilePath);
                        }
                        break;
                    case 3: // DP Tool
                        if (_sourceWindow is DPToolWindow dpToolWindow)
                        {
                            dpToolWindow.ProcessSingleFile(_viewModel.CurrentOsuFilePath);
                        }
                        break;
                    default:
                        // 备用方案：显示文件路径
                        MessageBox.Show($"Selected file: {_viewModel.CurrentOsuFilePath}", "File Selected", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                }
            }
            else
            {
                string message = string.IsNullOrEmpty(_viewModel.SongsPath) ? 
                    "Please set the Songs directory first." : 
                    "No beatmap is currently selected.";
                MessageBox.Show(message, "Cannot Convert", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void InitializeHotkey()
        {
            try
            {
                _globalHotkey = new GlobalHotkey(_viewModel.HotkeyText, () => {
                    // 在UI线程执行转换操作
                    Dispatcher.Invoke(() => ConvertButton_Click(null, null));
                }, this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register hotkey: {ex.Message}");
                MessageBox.Show($"Failed to register hotkey: {ex.Message}\n\nPlease bind a new hotkey.", "Hotkey Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UnregisterHotkey()
        {
            _globalHotkey?.Unregister();
            _globalHotkey = null;
        }

        // 添加热键设置按钮点击事件
        private void HotkeySetButton_Click(object sender, RoutedEventArgs e)
        {
            var hotkeyWindow = new HotkeyWindow(_viewModel.HotkeyText);
            if (hotkeyWindow.ShowDialog() == true)
            {
                _viewModel.SetHotkey(hotkeyWindow.Hotkey);
            }
        }
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeHotkey();
        }
        
        
        
    }
}
