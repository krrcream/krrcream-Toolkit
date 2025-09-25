using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using krrTools.tools.DPtool;
using krrTools.tools.N2NC;
using krrTools.tools.Preview;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;

namespace krrTools.tools.Listener
{
    internal class ListenerControl : UserControl
    {
        private readonly ListenerViewModel _viewModel;
        private readonly object? _sourceWindow;
        private readonly int _sourceId;
        private GlobalHotkey? _globalHotkey;

        internal static bool IsOpen { get; private set; }

        internal ListenerControl(object? sourceWindow = null, int sourceId = 0)
        {
            // Initialize control UI
            BuildUI();
            _viewModel = new ListenerViewModel();
            DataContext = _viewModel;
            _sourceWindow = sourceWindow;
            _sourceId = sourceId;

            // Subscribe to language changes
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            // Unsubscribe when unloaded
            this.Unloaded += (_,_) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;

            // 监听热键变化
            _viewModel.HotkeyChanged += (_, _) => {
                UnregisterHotkey();
                this.Dispatcher.BeginInvoke(new Action(InitializeHotkey));
            };

            // 订阅 BeatmapSelected 事件以便在实时预览开启时把文件推送到预览控件
            _viewModel.BeatmapSelected += ViewModel_BeatmapSelected;
            // 订阅 Config 属性变化以响应 RealTimePreview 开关
            _viewModel.Config.PropertyChanged += ViewModel_ConfigPropertyChanged;
            
            //TODO: 监听 Tab页活动，切换对应处理器，不切换标签，需要检查功能
            _viewModel.WindowTitle = Strings.ListenerTitlePrefix;
        }

        private void BuildUI()
        {
            // Control styling: background and sizing handled by host
            this.Background = new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));

            // Root grid
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top border with buttons
            var topBorder = new Border { Background = Brushes.LightGray, Padding = new Thickness(10) };
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            titleText.SetBinding(TextBlock.TextProperty, new Binding("WindowTitle"));
            Grid.SetColumn(titleText, 0);
            topGrid.Children.Add(titleText);

            var createBtn = SharedUIComponents.CreateStandardButton(Strings.CreateMapLabel);
            createBtn.Background = Brushes.LightBlue;
            createBtn.Margin = new Thickness(5);
            createBtn.Click += ConvertButton_Click;
            Grid.SetColumn(createBtn, 1);
            topGrid.Children.Add(createBtn);

            var hotkeyText = new TextBlock { Margin = new Thickness(10,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            // bind to centralized Config.Hotkey
            hotkeyText.SetBinding(TextBlock.TextProperty, new Binding("Config.Hotkey"));
            Grid.SetColumn(hotkeyText, 2);
            topGrid.Children.Add(hotkeyText);

            var hotkeyBtn = SharedUIComponents.CreateStandardButton(Strings.SetHotkeyLabel);
            hotkeyBtn.Background = Brushes.LightYellow;
            hotkeyBtn.Margin = new Thickness(5);
            hotkeyBtn.Click += HotkeySetButton_Click;
            Grid.SetColumn(hotkeyBtn, 3);
            topGrid.Children.Add(hotkeyBtn);

            topBorder.Child = topGrid;
            Grid.SetRow(topBorder, 0);
            root.Children.Add(topBorder);

            // Content area
            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Songs path area
            var songsPanel = new StackPanel { Margin = new Thickness(5,0,5,10), Orientation = Orientation.Vertical };
            var songsLabel = SharedUIComponents.CreateHeaderLabel(Strings.SongsFolderPathHeader);
            songsLabel.Foreground = Brushes.White;
            songsPanel.Children.Add(songsLabel);

            var songsGrid = new Grid();
            songsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            songsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var songsPathText = new TextBlock { Background = Brushes.Transparent, FontSize = 18, Foreground = Brushes.White };
            songsPathText.SetBinding(TextBlock.TextProperty, new Binding("Config.SongsPath") { Mode = BindingMode.OneWay });
            Grid.SetColumn(songsPathText, 0);
            songsGrid.Children.Add(songsPathText);
            var browseBtn = SharedUIComponents.CreateStandardButton(Strings.BrowseLabel);
            browseBtn.Padding = new Thickness(10,2,10,2);
            browseBtn.Click += BrowseButton_Click;
            Grid.SetColumn(browseBtn, 1);
            songsGrid.Children.Add(browseBtn);
            songsPanel.Children.Add(songsGrid);

            var realTimeChk = SharedUIComponents.CreateStandardCheckBox(Strings.RealTimePreviewLabel);
            realTimeChk.Margin = new Thickness(5,8,5,0);
            realTimeChk.Foreground = Brushes.White;
            realTimeChk.Background = Brushes.Transparent;
            realTimeChk.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Config.RealTimePreview") { Mode = BindingMode.TwoWay });
            songsPanel.Children.Add(realTimeChk);

            Grid.SetRow(songsPanel, 0);
            contentGrid.Children.Add(songsPanel);

            // Monitoring group
            var group = new GroupBox { FontSize = 18, Margin = new Thickness(0,10,0,0) };
            var header = SharedUIComponents.CreateHeaderLabel(Strings.MonitoringInformationHeader);
            header.Foreground = Brushes.White;
            group.Header = header;

            var grpGrid = new Grid();
            grpGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grpGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var statusText = new TextBlock { FontSize = 18, Margin = new Thickness(0,5,0,0), Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
            statusText.SetBinding(TextBlock.TextProperty, new Binding("StatusMessage"));
            Grid.SetRow(statusText, 0);
            grpGrid.Children.Add(statusText);

            // DualPreviewControl intentionally omitted per previous XAML comment

            group.Content = grpGrid;
            Grid.SetRow(group, 1);
            contentGrid.Children.Add(group);

            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);

            // Set control content
            this.Content = root;

            // Wire lifecycle events
            this.Loaded += (_,_) => InitializeHotkey();
            this.Unloaded += Window_Closing;
        }

        private void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            _viewModel.SetSongsPath();
        }

        private void Window_Closing(object? sender, RoutedEventArgs e)
        {
            _viewModel.SaveConfig();
            _viewModel.Cleanup();
            UnregisterHotkey();
            // mark closed
            IsOpen = false;
        }
        
        private void ConvertButton_Click(object? sender, RoutedEventArgs? e)
        {
            // 检查是否设置了Songs目录且当前有选中的谱面
            if (string.IsNullOrEmpty(_viewModel.Config.SongsPath) && string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
            {
                string message = string.IsNullOrEmpty(_viewModel.Config.SongsPath) ?
                    "Please set the Songs directory first." :
                    "No beatmap is currently selected.";
                MessageBox.Show(message, "Cannot Convert", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // If RealTimePreview is on and there are staged files, convert those instead of the single current file
            try
            {
                if (_viewModel.Config.RealTimePreview)
                {
                    var staged = DualPreviewControl.GetSharedStagedPaths();
                    if (staged is { Length: > 0 })
                    {
                        // Prefer an existing ConverterWindow or create one
                        N2NCControl? conv;
                        if (_sourceWindow is N2NCControl swConv) conv = swConv;
                        else
                        {
                            conv = Application.Current?.Windows.OfType<N2NCControl>().FirstOrDefault();
                        }

                        if (conv == null)
                        {
                            try {
                                conv = new N2NCControl();
                                var convWindow = new Window { Title = Strings.TabConverter, Content = conv, Width = 800, Height = 600 };
                                convWindow.Show();
                            } catch (Exception ex) { Debug.WriteLine($"Failed to create/show N2NCControl: {ex.Message}"); }
                        }

                        if (conv == null)
                        {
                            MessageBox.Show("No converter available to process staged files.", "No Converter", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var created = new List<string>();
                        var failed = new List<string>();

                        foreach (var p in staged.Where(p => !string.IsNullOrEmpty(p)))
                        {
                            try
                            {
                                var res = conv.ProcessSingleFile(p);
                                if (!string.IsNullOrEmpty(res)) created.Add(res);
                                else failed.Add(p);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Error converting staged file: " + ex.Message);
                                failed.Add(p);
                            }
                        }

                        if (created.Count > 0)
                        {
                            try { DualPreviewControl.BroadcastStagedPaths(null); } catch (Exception ex) { Debug.WriteLine($"BroadcastStagedPaths clear failed: {ex.Message}"); }

                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine("Conversion finished. Created files:");
                            foreach (var c in created) sb.AppendLine(c);
                            if (failed.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine("The following source files failed to convert:");
                                foreach (var f in failed) sb.AppendLine(f);
                            }
                            MessageBox.Show(sb.ToString(), "Conversion Result", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            var msg = failed.Count > 0
                                ? "Conversion failed for the staged files. The staged files remain so you can retry."
                                : "Conversion did not produce any output.";
                            MessageBox.Show(msg, "Conversion Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }

                        return; // staged handling done
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error while attempting to process staged files: " + ex.Message);
                // fall through to single-file behavior
            }

            // Fallback/single-file behavior: process the current selected osu file
            if (!string.IsNullOrEmpty(_viewModel.CurrentOsuFilePath))
            {
                switch (_sourceId)
                {
                    case 1: // Converter
                        try
                        {
                            N2NCControl? conv;
                            if (_sourceWindow is N2NCControl swConv) conv = swConv;
                            else
                            {
                                conv = Application.Current?.Windows.OfType<N2NCControl>().FirstOrDefault();
                            }

                            if (conv == null)
                            {
                                try {
                                    conv = new N2NCControl();
                                    var convWindow = new Window { Title = Strings.TabConverter, Content = conv, Width = 800, Height = 600 };
                                    convWindow.Show();
                                } catch (Exception ex) { Debug.WriteLine($"Failed to create/show N2NCControl: {ex.Message}"); }
                            }

                            if (conv != null)
                            {
                                conv.ProcessSingleFile(_viewModel.CurrentOsuFilePath, openOsz: true);
                            }
                            else
                            {
                                MessageBox.Show("No converter available to process the file.", "No Converter", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Converter invocation failed: " + ex.Message);
                            MessageBox.Show("Conversion failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;
                    case 2: // LN Transformer
                        {
                            LNTransformer.LNTransformerControl? LNTransformerControl;
                            if (_sourceWindow is LNTransformer.LNTransformerControl swLn) LNTransformerControl = swLn;
                            else
                            {
                                LNTransformerControl = Application.Current?.Windows.OfType<LNTransformer.LNTransformerControl>().FirstOrDefault();
                            }

                            if (LNTransformerControl == null)
                            {
                                try {
                                    LNTransformerControl = new LNTransformer.LNTransformerControl();
                                    var lnWindow = new Window { Title = Strings.TabLNTransformer, Content = LNTransformerControl, Width = 800, Height = 600 };
                                    lnWindow.Show();
                                } catch (Exception ex) { Debug.WriteLine($"Failed to create/show LNTransformerControl: {ex.Message}"); }
                            }

                            if (LNTransformerControl != null)
                            {
                                LNTransformerControl.ProcessSingleFile(_viewModel.CurrentOsuFilePath);
                            }
                        }
                        break;
                    case 3: // DP Tool
                        if (_sourceWindow is DPToolControl dpToolWindow)
                        {
                            dpToolWindow.ProcessSingleFile(_viewModel.CurrentOsuFilePath);
                        }
                        break;
                    default:
                        MessageBox.Show($"Selected file: {_viewModel.CurrentOsuFilePath}", "File Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                }
            }
            else
            {
                MessageBox.Show("No beatmap is currently selected.", "Cannot Convert", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void InitializeHotkey()
        {
            try
            {
                var hostWindow = Window.GetWindow(this) ?? Application.Current?.MainWindow;
                if (hostWindow == null) throw new InvalidOperationException("ListenerControl must be hosted in a Window to register hotkeys.");
                _globalHotkey = new GlobalHotkey(_viewModel.Config.Hotkey ?? string.Empty, () => {
                    // 在UI线程执行转换操作
                    Dispatcher.Invoke(() => ConvertButton_Click(null, null));
                }, hostWindow);
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
            var hotkeyWindow = new HotkeyWindow(_viewModel.Config.Hotkey ?? string.Empty);
            if (hotkeyWindow.ShowDialog() == true)
            {
                _viewModel.SetHotkey(hotkeyWindow.Hotkey);
            }
        }
        
        // ViewModel 属性变化处理（关注 RealTimePreview）
        private void ViewModel_ConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "RealTimePreview")
            {
                // 在 UI 线程更新预览控件的 Drop 可用性
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 当实时预览开启时，禁用所有预览的 DropZone；关闭时恢复
                        DualPreviewControl.SetGlobalDropEnabled(!_viewModel.Config.RealTimePreview);
                        if (!_viewModel.Config.RealTimePreview)
                        {
                            // 清理之前通过实时预览暂存的文件（全局广播清除）
                            DualPreviewControl.BroadcastStagedPaths(null);
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"ListenerView.ViewModel_PropertyChanged: {ex.Message}"); }
                }));
            }
        }

        // Beatmap 被选中时的处理：当实时预览开启则把文件广播到预览並暂存以便转换
        private void ViewModel_BeatmapSelected(object? sender, string osuPath)
        {
            if (!_viewModel.Config.RealTimePreview) return; // 只有在实时预览开启时才路由
              if (string.IsNullOrEmpty(osuPath)) return;

             // 确保在 UI 线程访问控件
             Dispatcher.BeginInvoke(new Action(() =>
             {
                 try
                 {
                     if (!System.IO.File.Exists(osuPath)) return;

                     var arr = new[] { osuPath };

                     // 广播到所有预览并尝试把文件加载进主窗口的预览控件（尽量一次性处理，减少多层 try/catch）
                     DualPreviewControl.BroadcastStagedPaths(arr);

                     if (Application.Current?.MainWindow is MainWindow main)
                     {
                         // Use null-guards and log any exceptions at top level
                         if (main.ConverterPreview != null)
                         {
                             main.ConverterPreview.LoadFiles(arr, suppressBroadcast: true);
                             main.ConverterPreview.ApplyStagedUI(arr);
                         }

                         if (main.LNPreview != null)
                         {
                             main.LNPreview.LoadFiles(arr, suppressBroadcast: true);
                             main.LNPreview.ApplyStagedUI(arr);
                         }

                         if (main.DPPreview != null)
                         {
                             main.DPPreview.LoadFiles(arr, suppressBroadcast: true);
                             main.DPPreview.ApplyStagedUI(arr);
                         }
                     }
                     DualPreviewControl.SetGlobalDropEnabled(false);
                 }
                 catch (Exception ex)
                 {
                     Debug.WriteLine($"Listener preview broadcast failed: {ex.Message}");
                 }
             }));
         }

        private void OnLanguageChanged()
        {
            try
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _viewModel.WindowTitle = Strings.ListenerTitlePrefix;
                }));
            }
            catch (Exception ex) { Debug.WriteLine($"ListenerView OnLanguageChanged invoke failed: {ex.Message}"); }
        }
    }
}
