
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using krrTools.Tools.DPtool;
using krrTools.Tools.FilesManager;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.KrrLV;
using krrTools.Tools.Listener;
using krrTools.Tools.N2NC;
using krrTools.Tools.Preview;
using krrTools.UI;
using krrTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Grid = System.Windows.Controls.Grid;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;

namespace krrTools;
public class MainWindow : FluentWindow
{
    private readonly Dictionary<object, ContentControl> _settingsHosts = new();
    private readonly Dictionary<string, DualPreviewControl> _previewControls = new();
    
    private ContentControl? _currentSettingsContainer;
    private Grid _mainGrid = null!;
    private TabView MainTabControl = null!;
    private ToggleButton GlobalOsuListenerButton = null!;
    private ToggleSwitch _realTimeToggle = null!;
    
    private ListenerViewModel? _listenerVM;
    private Window? _currentListenerWindow;
    
    // 跟踪选项卡拖动/分离
    private Point _dragStartPoint;
    private TabViewItem? _draggedTab;
    private DateTime _lastPreviewRefresh = DateTime.MinValue;
    
    private object? _currentTool;
    private string? _internalOsuPath;
    private bool _realTimePreview;

    public readonly FileDispatcher _fileDispatcher;
    public TabView TabControl => MainTabControl;
    // 文件调度器
    public FileDispatcher FileDispatcher => _fileDispatcher;
    
    // 工具调度器
    private ToolScheduler ToolScheduler { get; }

    private bool RealTimePreview
    {
        get => _realTimePreview;
        set
        {
            if (_realTimePreview != value)
            {
                _realTimePreview = value;
                SaveRealTimePreview();
                OnRealTimePreviewChanged();
            }
        }
    }
    
    private void SaveRealTimePreview() => BaseOptionsManager.SetRealTimePreview(_realTimePreview);
    private void LoadRealTimePreview() => _realTimePreview = BaseOptionsManager.GetRealTimePreview();

    private void DebouncedRefresh(DualPreviewControl control, int ms = 150)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastPreviewRefresh).TotalMilliseconds < ms) return;
        _lastPreviewRefresh = now;
        control.Refresh();
    }
    
    private N2NCControl _convWindowInstance = null!;
    private DPToolControl _dpWindowInstance = null!;
    private KRRLNTransformerControl _krrLnTransformerInstance = null!;
    
    public N2NCControl ConvWindowInstance => _convWindowInstance;
    public DPToolControl DPWindowInstance => _dpWindowInstance;
    public KRRLNTransformerControl KRRLNTransformerInstance => _krrLnTransformerInstance;
    
    private ContentControl? N2NCSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.N2NC);
    private ContentControl? DPSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.DP);
    private ContentControl? KRRLNSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.KRRLN);
    private ContentControl? LVCalSettingsHost => _settingsHosts.GetValueOrDefault(ModuleEnum.LVCalculator);
    private ContentControl? FilesManagerHost => _settingsHosts.GetValueOrDefault(ModuleEnum.FilesManager);

    public DualPreviewControl? PreviewControl => _previewControls.GetValueOrDefault("Global");

    public MainWindow()
    {
        ToolScheduler = App.Services.GetRequiredService<ToolScheduler>();

        LoadRealTimePreview();

        Title = Strings.WindowTitle;
        Width = 1000;
        Height = 750;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        BuildUI();
        SharedUIComponents.SetPanelBackgroundAlpha(102);

        LoadToolSettingsHosts();
        SetupPreviewProcessors();
        _fileDispatcher = new FileDispatcher(_previewControls, MainTabControl);
        // Connect conversion events to the global preview control
        if (_previewControls.TryGetValue("Global", out var globalPreview))
        {
            globalPreview.StartConversionRequested += GlobalPreview_StartConversionRequested;
        }

        Loaded += MainWindow_Loaded;
    }

    private void BuildUI()
    {
        var root = new Grid()
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }, // 标题栏
                new RowDefinition { Height = GridLength.Auto }, // 选项卡行
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // 主内容区域（设置 + 预览）
                new RowDefinition { Height = GridLength.Auto }, // Footer状态栏行
            },
            Children =
            {
                new TitleBar()
                {
                    Title = Strings.WindowTitle,
                },
            }
        };
        
        // 选项卡TabControl - 只显示选项卡头
        MainTabControl = new TabView
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(CustomTabPanel))),
            Height = 30 // 只显示选项卡头
        };
        MainTabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
        MainTabControl.PreviewMouseMove += TabControl_PreviewMouseMove;
        MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

        Grid.SetRow(MainTabControl, 1);
        Grid.SetColumnSpan(MainTabControl, 2); // 跨越两列
        root.Children.Add(MainTabControl);

        // 主内容Grid - 设置和预览
        _mainGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(320) }, // 设置 - 320宽
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } // 预览器 - 动态适应剩余宽度
            }
        };
        Grid.SetRow(_mainGrid, 2);
        root.Children.Add(_mainGrid);

        // 设置内容容器
        var settingsContainer = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _currentSettingsContainer = settingsContainer;
        Grid.SetColumn(settingsContainer, 0);
        _mainGrid.Children.Add(settingsContainer);

        // 预览器
        BuildPreviewTabs();
        BuildNoPreViewTabs();

        // 全局预览器
        var globalPreview = new DualPreviewControl { Margin = new Thickness(8), Visibility = Visibility.Collapsed };
        _previewControls["Global"] = globalPreview;
        var previewBorder = SharedUIComponents.CreateStandardPanel(globalPreview, new Thickness(8));
        Grid.SetColumn(previewBorder, 1);
        _mainGrid.Children.Add(previewBorder);

        // Footer
        _realTimeToggle = new ToggleSwitch
        {
            IsChecked = RealTimePreview,
            DataContext = new LocalizedStringHelper.LocalizedString(Strings.RealTimePreviewLabel)
        };
        _realTimeToggle.SetBinding(ContentProperty, new Binding("Value"));
        _realTimeToggle.Checked += (_,_) => RealTimePreview = true;
        _realTimeToggle.Unchecked += (_,_) => RealTimePreview = false;

        // GlobalOsuListenerButton = SharedUIComponents.CreateStandardButton(Strings.OSUListener);
        var localizedListenerText = new LocalizedStringHelper.LocalizedString(Strings.OSUListener);
        GlobalOsuListenerButton = new ToggleButton
        {
            Width = 120,
            Height = 24,
            Margin = new Thickness(4, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = false
        };
        GlobalOsuListenerButton.SetBinding(ContentProperty, new Binding("Value") { Source = localizedListenerText });
        GlobalOsuListenerButton.Click += GlobalOsuListenerButton_Click;

        var footer = SharedUIComponents.CreateStatusBar(this, _realTimeToggle, GlobalOsuListenerButton);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;

        AllowDrop = true;

        // 设置初始选项卡内容
        MainTabControl_SelectionChanged(null, null);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var savedTheme = SharedUIComponents.GetSavedApplicationTheme() != null 
                         && Enum.TryParse<ApplicationTheme>(SharedUIComponents.GetSavedApplicationTheme(), out var theme) ? 
            theme : ApplicationTheme.Light;
        
        var savedBackdrop = SharedUIComponents.GetSavedWindowBackdropType() != null 
                            && Enum.TryParse<WindowBackdropType>(SharedUIComponents.GetSavedWindowBackdropType(), out var backdrop) ? 
            backdrop : WindowBackdropType.Acrylic;
        
        var savedAccent = SharedUIComponents.GetSavedUpdateAccent() ?? true;
        ApplicationThemeManager.Apply(savedTheme, savedBackdrop, savedAccent);

        // 设置窗口背景为虚化的osu背景图片
        Background = new ImageBrush
        {
            Stretch = Stretch.UniformToFill,
            Opacity = 0.25,
        };
    }

    #region 创建带预览器选项卡
    private void BuildPreviewTabs()
    {
        var previewConfigs = new[] { ConverterEnum.N2NC, ConverterEnum.KRRLN, ConverterEnum.DP };
        foreach (var cfg in previewConfigs)
        {
            var headerText = cfg switch
            {
                ConverterEnum.N2NC => Strings.TabN2NC,
                ConverterEnum.KRRLN => Strings.TabKRRsLN,
                ConverterEnum.DP => Strings.TabDPTool,
                _ => cfg.ToString()
            };
            var headerLabel = SharedUIComponents.CreateHeaderLabel(headerText);
            headerLabel.FontSize = 14;
            var tab = new TabViewItem
            {
                Header = headerLabel,
                Tag = cfg,
                Width = double.NaN,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var settingsHost = new ContentControl();
            _settingsHosts[cfg] = settingsHost;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            settingsHost.Content = null;
            scroll.Content = settingsHost;
            SharedUIComponents.CreateStandardPanel(scroll, new Thickness(8));
            MainTabControl.Items.Add(tab);
        }
    }
    #endregion
    
    #region 创建简单选项卡（无预览器）
    private void BuildNoPreViewTabs()
    {
        var simpleConfigs = new[] { ModuleEnum.LVCalculator, ModuleEnum.FilesManager };
        foreach (var cfg in simpleConfigs)
        {
            var headerText = cfg switch
            {
                ModuleEnum.LVCalculator => Strings.TabKrrLV,
                ModuleEnum.FilesManager => Strings.TabFilesManager,
                _ => cfg.ToString()
            };
            var headerLabel = SharedUIComponents.CreateHeaderLabel(headerText);
            headerLabel.FontSize = 14;
            var tab = new TabViewItem
            {
                Header = headerLabel,
                Tag = cfg,
                Width = double.NaN,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var settingsHost = new ContentControl();
            _settingsHosts[cfg] = settingsHost;
            // 文件管理器和LV计算器不需要滚动条
            settingsHost.Content = null;
            var grid = new Grid { Margin = new Thickness(8) };
            var settingsBorder = SharedUIComponents.CreateStandardPanel(settingsHost, padding: new Thickness(0));
            grid.Children.Add(settingsBorder);
            MainTabControl.Items.Add(tab);
        }
    }
    #endregion
    
    #region 统一分配预览处理器
    private void SetupPreviewProcessors()
    {
        _internalOsuPath = ResolveInternalSample();
    }

    private void RefreshGlobalPreviewIfCurrentTool(string toolName)
    {
        if (_previewControls.TryGetValue("Global", out var globalPreview) && 
            globalPreview.CurrentTool == toolName && 
            globalPreview.Visibility == Visibility.Visible)
        {
            DebouncedRefresh(globalPreview);
        }
    }
    #endregion

    // 清除固定尺寸属性，以便嵌入时自适应布局
    private void ClearFixedSizes(DependencyObject? element)
    {
        if (element is FrameworkElement fe)
        {
            fe.Width = double.NaN;
            fe.Height = double.NaN;
            fe.MinWidth = 0;
            fe.MinHeight = 0;
            fe.MaxWidth = double.PositiveInfinity;
            fe.MaxHeight = double.PositiveInfinity;
            fe.HorizontalAlignment = HorizontalAlignment.Stretch;
            fe.VerticalAlignment = VerticalAlignment.Stretch;
        }

        // 递归处理逻辑子项
        if (element != null)
        {
            var children = LogicalTreeHelper.GetChildren(element).OfType<object>().ToList();
            foreach (var child in children)
                // 仅对 DependencyObject 子项递归
                if (child is DependencyObject dob)
                    ClearFixedSizes(dob);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }

        return null;
    }

#region 嵌入各工具设置窗口

    /// <summary>
    /// 工具嵌入配置
    /// </summary>
    private class ToolEmbeddingConfig
    {
        public required Func<UserControl> ControlFactory { get; init; }
        public required Func<ContentControl?> HostGetter { get; init; }
        public Action<UserControl>? InstanceSetter { get; init; }
    }

    /// <summary>
    /// 通用方法：将工具控件的内容嵌入到指定的宿主容器中
    /// </summary>
    /// <param name="toolControl">工具控件实例</param>
    /// <param name="host">宿主容器</param>
    private void EmbedTool(UserControl toolControl, ContentControl host)
    {
        if (toolControl.Content is UIElement content)
        {
            // 复制资源，以便 StaticResource/Style 查找仍然有效
            var keys = toolControl.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                // Skip implicit Window styles (they would affect the host/main window)
                var res = toolControl.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!host.Resources.Contains(k))
                    host.Resources.Add(k, toolControl.Resources[k]);
            }

            // 清除嵌入内容中的固定尺寸以便自适应
            ClearFixedSizes(content);

            // 将实际内容移入宿主，保留绑定和事件处理器
            host.DataContext = toolControl.DataContext;
            host.Content = content;
            // 清空原窗口内容，使元素只有一个父级
            toolControl.Content = null;
        }
    }

    private void LoadToolSettingsHosts()
    {
        var toolConfigs = new[]
        {
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new N2NCControl(),
                HostGetter = () => N2NCSettingsHost,
                InstanceSetter = control => _convWindowInstance = (N2NCControl)control
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new KRRLNTransformerControl(),
                HostGetter = () => KRRLNSettingsHost,
                InstanceSetter = control => _krrLnTransformerInstance = (KRRLNTransformerControl)control
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new DPToolControl(),
                HostGetter = () => DPSettingsHost,
                InstanceSetter = control => _dpWindowInstance = (DPToolControl)control
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new KrrLVControl(),
                HostGetter = () => LVCalSettingsHost
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new FilesManagerControl(),
                HostGetter = () => FilesManagerHost
            }
        };

        foreach (var config in toolConfigs)
        {
            var control = config.ControlFactory();
            var host = config.HostGetter();
            
            if (host != null)
            {
                EmbedTool(control, host);
                config.InstanceSetter?.Invoke(control);
            }

            // 特殊处理：设置变化监听，用于实时预览更新
            if (control is KRRLNTransformerControl krrControl)
            {
                krrControl.SettingsChanged += (_, _) => RefreshGlobalPreviewIfCurrentTool(ConverterEnum.KRRLN);
            }
            else if (control is N2NCControl n2ncControl)
            {
                n2ncControl.SettingsChanged += (_, _) => RefreshGlobalPreviewIfCurrentTool(nameof(ConverterEnum.N2NC));
            }
            else if (control is DPToolControl dpControl)
            {
                dpControl.SettingsChanged += (_, _) => RefreshGlobalPreviewIfCurrentTool(nameof(ConverterEnum.DP));
            }
        }
    }
#endregion

    // 选项卡拖动/分离处理 - 克隆内容用于分离窗口，保持原选项卡不变
    private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        // 查找鼠标下的 Tab
        var source = e.OriginalSource as DependencyObject;
        while (source != null && !(source is TabViewItem)) source = VisualTreeHelper.GetParent(source);

        _draggedTab = source as TabViewItem;
    }

    private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedTab == null) return;
        var pos = e.GetPosition(this);
        var dx = Math.Abs(pos.X - _dragStartPoint.X);
        var dy = Math.Abs(pos.Y - _dragStartPoint.Y);
        const double dragThreshold = 20.0; // 最小拖动距离以触发分离
        if (dx > dragThreshold || dy > dragThreshold)
        {
            DetachTab(_draggedTab);
            _draggedTab = null;
        }
    }

#region 构建独立选项卡，切换时更新设置和预览
    private void DetachTab(TabViewItem tab)
    {
        if (!MainTabControl.Items.Contains(tab)) return;

        var toolKey = tab.Tag.ToString();
        // Only allow LV and GetFiles tools to detach
        if (toolKey != "LVCalculator" && toolKey != "FilesManager") return;

        var header = tab.Header?.ToString() ?? "Detached";

        Func<ContentControl> createFreshWindow = toolKey switch
        {
            "LVCalculator" => () => new KrrLVControl(),
            "FilesManager" => () => new FilesManagerControl(),
            _ => throw new ArgumentOutOfRangeException()
        };

        var control = createFreshWindow();
        var win = new Window
        {
            Title = header,
            Content = control,
            Width = 800,
            Height = 600,
            Owner = this
        };

        var cursor = System.Windows.Forms.Cursor.Position;
        win.Left = cursor.X - 40;
        win.Top = cursor.Y - 10;

        var insertIndex = MainTabControl.Items.IndexOf(tab);
        MainTabControl.Items.Remove(tab);

        var followTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        followTimer.Tick += (_, _) =>
        {
            var p = System.Windows.Forms.Cursor.Position;
            win.Left = p.X - 40;
            win.Top = p.Y - 10;
            if ((System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Left) !=
                System.Windows.Forms.MouseButtons.Left)
            {
                var tabPanel = FindVisualChild<TabPanel>(MainTabControl);
                Rect headerRect;
                if (tabPanel != null)
                {
                    var panelTopLeft = tabPanel.PointToScreen(new Point(0, 0));
                    headerRect = new Rect(panelTopLeft.X, panelTopLeft.Y, tabPanel.ActualWidth,
                        tabPanel.ActualHeight);
                }
                else
                {
                    var topLeft = MainTabControl.PointToScreen(new Point(0, 0));
                    headerRect = new Rect(topLeft.X, topLeft.Y, MainTabControl.ActualWidth,
                        Math.Min(80, MainTabControl.ActualHeight));
                }

                var winRect = new Rect(win.Left, win.Top, win.ActualWidth > 0 ? win.ActualWidth : win.Width,
                    win.ActualHeight > 0 ? win.ActualHeight : win.Height);
                if (headerRect.IntersectsWith(winRect)) win.Close();

                followTimer.Stop();
            }
        };
        followTimer.Start();

        win.Closed += (_, _) =>
        {
            if (followTimer.IsEnabled) followTimer.Stop();
        };

        win.Closing += (_, _) =>
        {
            if (followTimer.IsEnabled) followTimer.Stop();

            if (!MainTabControl.Items.Contains(tab))
            {
                if (insertIndex >= 0 && insertIndex <= MainTabControl.Items.Count)
                    MainTabControl.Items.Insert(insertIndex, tab);
                else MainTabControl.Items.Add(tab);
            }

            MainTabControl.SelectedItem = tab;
        };

        win.Show();
    }
#endregion

    private string? ResolveInternalSample()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var direct = Path.Combine(baseDir, "mania-PreView.osu");
            if (File.Exists(direct)) return direct;
            var dir = new DirectoryInfo(baseDir);
            for (var i = 0; i < 6; i++)
            {
                if (dir == null) break;
                var candidate = Path.Combine(dir.FullName, "tools", "Preview",
                    "mania-PreView.osu");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        catch (Exception)
        {
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("MainWindow")
                .LogWarning("无法定位内置示例谱面");
        }

        return null;
    }

    private void GlobalPreview_StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;

        // Get the currently active tool from the selected tab
        var selectedTag = (MainTabControl.SelectedItem as TabViewItem)?.Tag?.ToString();
        if (string.IsNullOrEmpty(selectedTag))
        {
            // Fallback to Global preview's current tool
            if (sender is DualPreviewControl globalPreview && !string.IsNullOrEmpty(globalPreview.CurrentTool))
            {
                selectedTag = globalPreview.CurrentTool;
            }
            if (string.IsNullOrEmpty(selectedTag)) return;
        }

        // Filter out internal sample and invalid files
        var toProcess = paths.Where(p => !string.Equals(p, _internalOsuPath, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (toProcess.Length == 0) return;

        _fileDispatcher.ConvertFiles(toProcess, selectedTag);
    }

    private void GlobalOsuListenerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentListenerWindow is { IsVisible: true })
        {
            _currentListenerWindow.Close();
            _currentListenerWindow = null;
            return;
        }

        var selectedTab = MainTabControl.SelectedItem as TabViewItem;

        object? source = null;
        var toolEnum = selectedTab?.Tag;
        var sourceId = toolEnum != null ? BaseOptionsManager.GetSourceId(toolEnum) : 0;

        // 使用自动映射获取控件实例
        switch (toolEnum)
        {
            case ConverterEnum.N2NC:
                source = _convWindowInstance;
                break;
            case ConverterEnum.DP:
                source = _dpWindowInstance;
                break;
            case ConverterEnum.KRRLN:
                source = _krrLnTransformerInstance;
                break;
        }

        var listenerControl = new ListenerControl(source, sourceId)
        {
            RealTimePreview = RealTimePreview
        };
        _currentListenerWindow = listenerControl;
        _currentListenerWindow.Closed += (_, _) => _currentListenerWindow = null;
        _currentListenerWindow.Show();
    }
    

    // Custom TabPanel that allows dynamic widths
    private class CustomTabPanel : TabPanel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            double totalWidth = 0;
            double maxHeight = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                totalWidth += child.DesiredSize.Width;
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }
            return new Size(totalWidth, maxHeight);
        }
    }

    private void OnRealTimePreviewChanged()
    {
        if (_realTimePreview)
        {
            if (_listenerVM == null)
            {
                _listenerVM = new ListenerViewModel();
                _listenerVM.BeatmapSelected += OnBeatmapSelected;
            }
            if (string.IsNullOrEmpty(_listenerVM.Config.SongsPath))
            {
                _listenerVM.SetSongsPath();
            }
        }
        else
        {
            if (_listenerVM != null)
            {
                _listenerVM.Cleanup();
                _listenerVM = null;
            }
        }
    }

    private void OnBeatmapSelected(object? sender, string osuPath)
    {
        if (string.IsNullOrEmpty(osuPath)) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (!File.Exists(osuPath)) return;

                // 更新窗口背景
                string? bgPath = PreviewTransformation.GetBackgroundImagePath(osuPath);
                if (!string.IsNullOrEmpty(bgPath) && File.Exists(bgPath))
                {
                    var bgBitmap = new BitmapImage();
                    bgBitmap.BeginInit();
                    bgBitmap.UriSource = new Uri(bgPath);
                    bgBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bgBitmap.EndInit();
                    if (Background is ImageBrush ib)
                    {
                        ib.ImageSource = bgBitmap;
                    }
                }

                var arr = new[] { osuPath };
                DualPreviewControl.BroadcastStagedPaths(arr);
                // Load to global preview if current tool is a converter
                if (_currentTool is ConverterEnum && _previewControls.TryGetValue("Global", out var globalPreview))
                {
                    globalPreview.LoadPreview(arr, suppressBroadcast: true);
                    globalPreview.ApplyDropZoneStagedUI(arr);
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("MainWindow")
                    .LogError($"Error loading beatmap in real-time preview: {ex.Message}");
            }
        }));
    }

    private void RefreshGlobalPreviewIfCurrentTool(object toolEnum)
    {
        if (_currentTool?.Equals(toolEnum) == true && _previewControls.TryGetValue("Global", out var preview))
        {
            preview.Refresh();
        }
    }

    private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        var selectedTag = (MainTabControl.SelectedItem as TabViewItem)?.Tag;
        // 判断是否为转换工具，配套增加预览器
        var isConverter = selectedTag is ConverterEnum;
        if (_previewControls.TryGetValue("Global", out var preview))
        {
            preview.Visibility = isConverter ? Visibility.Visible : Visibility.Collapsed;
            preview.CurrentTool = selectedTag?.ToString(); // Set the current tool for the global preview
            if (isConverter && selectedTag != null)
            {
                // 直接创建统一的处理器
                var processor = new PreviewProcessor
                {
                    ToolScheduler = ToolScheduler,
                    // ConverterOptionsProvider = () => _converterVM?.Options ?? new N2NCOptions(),
                    // KRRLNOptionsProvider = () => _krrLnTransformerInstance.Options,
                    // // LNOptionsProvider = () => _lnVM?.Options ?? new YLsLNTransformerOptions(),
                    // DPOptionsProvider = () => _dpVM?.Options ?? new DPToolOptions(),
                    CurrentTool = selectedTag.ToString()
                };
                preview.Processor = processor;
            }
            else if (!isConverter)
            {
                preview.Processor = null; // 非转换工具时清除处理器
            }
        }
        // 动态调整列宽度
        if (isConverter)
        {
            _mainGrid.ColumnDefinitions[0].Width = new GridLength(320);
            _mainGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            _mainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _mainGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }
        if (_currentSettingsContainer != null && selectedTag != null && _settingsHosts.TryGetValue(selectedTag, out var settingsHost))
        {
            _currentSettingsContainer.Content = settingsHost.Content;
        }
        _currentTool = selectedTag;
    }
}
