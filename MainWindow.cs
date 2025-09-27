using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Shell;
using krrTools.Tools.Shared;
using krrTools.tools.Preview;
using krrTools.tools.DPtool;
using krrTools.tools.FilesManager;
using krrTools.tools.KRRLNTransformer;
using krrTools.tools.KrrLV;
using krrTools.tools.Listener;
using krrTools.tools.LNTransformer;
using krrTools.tools.N2NC;
using krrTools.tools.Shared;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;

namespace krrTools;

public class MainWindow : FluentWindow
{
    private readonly Dictionary<string, ContentControl> _settingsHosts = new();
    private readonly Dictionary<string, DualPreviewControl> _previewControls = new();



    private ContentControl? _currentSettingsContainer;
    private Grid _mainGrid = null!;
    private TabControl MainTabControl = null!;
    private Button GlobalOsuListenerButton = null!;
    private ToggleSwitch _realTimeToggle = null!;
    
    private Slider? _alphaSlider;
    private ListenerViewModel? _listenerVM;
    private Window? _currentListenerWindow;
    
    // 跟踪选项卡拖动/分离
    private Point _dragStartPoint;
    private TabViewItem? _draggedTab;

    private N2NCViewModel? _converterVM;
    private DPToolViewModel? _dpVM;
    private LNTransformerViewModel? _lnVM;
    private DateTime _lastPreviewRefresh = DateTime.MinValue;
    
    private string? _internalOsuPath;
    private byte _currentAlpha = 102;
    private readonly byte[] _alphaCycle = [0x22, 0x33, 0x44, 0x55, 0x66, 0x88, 0xAA, 0xCC, 0xEE];
    private int _alphaIndex;
    private bool _realTimePreview;

    public readonly FileDispatcher _fileDispatcher;
    public TabControl TabControl => MainTabControl;
    public FileDispatcher FileDispatcher => _fileDispatcher;
    
    // 工具调度器
    private ToolScheduler ToolScheduler { get; } = new();

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
    
    private void SaveRealTimePreview() => OptionsManager.SetRealTimePreview(_realTimePreview);
    private void LoadRealTimePreview() => _realTimePreview = OptionsManager.GetRealTimePreview();

    private void DebouncedRefresh(DualPreviewControl control, int ms = 150)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastPreviewRefresh).TotalMilliseconds < ms) return;
        _lastPreviewRefresh = now;
        control.Refresh();
    }

    private N2NCControl _convWindowInstance = null!;
    private LNTransformerControl _lnWindowInstance = null!;
    private KRRLNTransformerControl _krrLnTransformerInstance = null!;
    private DPToolControl _dpWindowInstance = null!;

    private ContentControl? N2NCSettingsHost => _settingsHosts.GetValueOrDefault(OptionsManager.N2NCToolName);
    private ContentControl? LNSettingsHost => _settingsHosts.GetValueOrDefault(OptionsManager.LNToolName);
    private ContentControl? DPSettingsHost => _settingsHosts.GetValueOrDefault(OptionsManager.DPToolName);
    private ContentControl? KRRLNSettingsHost => _settingsHosts.GetValueOrDefault(OptionsManager.KRRLNToolName);
    private ContentControl? LVCalSettingsHost => _settingsHosts.GetValueOrDefault(OptionsManager.LVCalToolName);
    private ContentControl? FilesManagerHost => _settingsHosts.GetValueOrDefault(OptionsManager.FilesManagerToolName);

    public DualPreviewControl? N2NCPreview => _previewControls.GetValueOrDefault(OptionsManager.N2NCToolName);
    public DualPreviewControl? LNPreview => _previewControls.GetValueOrDefault(OptionsManager.LNToolName);
    public DualPreviewControl? DPPreview => _previewControls.GetValueOrDefault(OptionsManager.DPToolName);
    public DualPreviewControl? KRRLNPreview => _previewControls.GetValueOrDefault(OptionsManager.KRRLNToolName);

    public MainWindow()
    {
        LoadRealTimePreview();

        Title = Strings.WindowTitle;
        Width = 1000;
        Height = 750;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        
        // 设置WindowChrome以实现自定义标题栏
        var windowChrome = new WindowChrome
        {
            CaptionHeight = 32,
            CornerRadius = new CornerRadius(0),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = true,
            ResizeBorderThickness = new Thickness(4)
        };
        WindowChrome.SetWindowChrome(this, windowChrome);

        BuildUI();
        SharedUIComponents.SetPanelBackgroundAlpha(102);

        PreviewKeyDown += MainWindow_PreviewKeyDown;
        LoadToolSettingsHosts();
        SetupPreviewProcessors();
        _fileDispatcher = new FileDispatcher(_previewControls, MainTabControl);
        if (_previewControls.TryGetValue(OptionsManager.N2NCToolName, out var cp))
            cp.StartConversionRequested += ConverterPreview_StartConversionRequested;
        if (_previewControls.TryGetValue(OptionsManager.LNToolName, out var lp))
            lp.StartConversionRequested += LNPreview_StartConversionRequested;
        if (_previewControls.TryGetValue(OptionsManager.DPToolName, out var dp))
            dp.StartConversionRequested += DPPreview_StartConversionRequested;

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
            }
        };

        // 创建自定义标题栏
        var titleBar = UIComponents.CreateTitleBar(this, Title);
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

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
        BuildNoPreveiwTabs();

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
            DataContext = new SharedUIComponents.LocalizedString(Strings.RealTimePreviewLabel)
        };
        _realTimeToggle.SetBinding(ContentProperty, new Binding("Value"));
        _realTimeToggle.Checked += (_,_) => RealTimePreview = true;
        _realTimeToggle.Unchecked += (_,_) => RealTimePreview = false;

        GlobalOsuListenerButton = SharedUIComponents.CreateStandardButton(Strings.OSUListenerButton);
        GlobalOsuListenerButton.Click += GlobalOsuListenerButton_Click;

        var footer = UIComponents.CreateStatusBar(this, _realTimeToggle, GlobalOsuListenerButton);
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        Content = root;

        AllowDrop = true;
        Drop += (_, e) => GlobalDropArea_Drop(e);

        // 初始化工具调度器
        InitializeToolScheduler();

        // 设置初始选项卡内容
        MainTabControl_SelectionChanged(null, null);
    }

    // 注册工具到调度器
    private void InitializeToolScheduler()
    {
        ToolScheduler.RegisterTool(new N2NCTool());
        ToolScheduler.RegisterTool(new LNTransformerTool());
        ToolScheduler.RegisterTool(new DPTool());
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
            Opacity = 0.3,
        };
    }

    #region 创建带预览器选项卡
    private void BuildPreviewTabs()
    {
        var previewConfigs = new[] { OptionsManager.N2NCToolName,OptionsManager.KRRLNToolName ,OptionsManager.LNToolName, OptionsManager.DPToolName };
        foreach (var cfg in previewConfigs)
        {
            var headerText = cfg switch
            {
                OptionsManager.N2NCToolName => Strings.TabN2NC,
                OptionsManager.KRRLNToolName => "KRR转面器",
                OptionsManager.LNToolName => Strings.TabLNTransformer,
                OptionsManager.DPToolName => Strings.TabDPTool,
                _ => cfg
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
    private void BuildNoPreveiwTabs()
    {
        var simpleConfigs = new[]
        {
            new { ToolKey = OptionsManager.LVCalToolName },
            new { ToolKey = OptionsManager.FilesManagerToolName }
        };
        foreach (var cfg in simpleConfigs)
        {
            var headerText = cfg.ToolKey == OptionsManager.LVCalToolName ? Strings.TabKrrLV : Strings.TabFilesManager;
            var headerLabel = SharedUIComponents.CreateHeaderLabel(headerText);
            headerLabel.FontSize = 14;
            var tab = new TabViewItem
            {
                Header = headerLabel,
                Tag = cfg.ToolKey,
                Width = double.NaN,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var settingsHost = new ContentControl();
            _settingsHosts[cfg.ToolKey] = settingsHost;
            // 文件管理器和LV计算器不需要滚动条
            settingsHost.Content = null;
            var grid = new Grid { Margin = new Thickness(8) };
            var settingsBorder = SharedUIComponents.CreateStandardPanel(settingsHost, padding: new Thickness(0));
            grid.Children.Add(settingsBorder);
            MainTabControl.Items.Add(tab);
        }
    }
    #endregion
    
    private void SetupPreviewProcessors()
    {
        _internalOsuPath = ResolveInternalSample();
        
        // 统一分配预览处理器
        if (_previewControls.TryGetValue(OptionsManager.N2NCToolName, out var converterPreview) &&
            _settingsHosts.TryGetValue(OptionsManager.N2NCToolName, out var convHost))
            converterPreview.Processor = new ConverterPreviewProcessor(null,
                () => (convHost.DataContext as N2NCViewModel)?.GetConversionOptions());

        if (_previewControls.TryGetValue(OptionsManager.LNToolName, out var lnPreview) &&
            _settingsHosts.TryGetValue(OptionsManager.LNToolName, out var lnHost))
            lnPreview.Processor = new LNPreviewProcessor(null,
                () => (lnHost.DataContext as LNTransformerViewModel)?.GetLNPreviewParameters() ?? default(LNTransformerCore.LNPreviewParameters));

        if (_previewControls.TryGetValue(OptionsManager.DPToolName, out var dpPreview) &&
            _settingsHosts.TryGetValue(OptionsManager.DPToolName, out var dpHost))
            dpPreview.Processor = new DPPreviewProcessor(null,
                () => (dpHost.DataContext as DPToolViewModel)?.Options ?? new DPToolOptions());

        _converterVM = N2NCSettingsHost?.DataContext as N2NCViewModel;
        if (_converterVM != null && N2NCPreview?.Processor is ConverterPreviewProcessor cpp)
        {
            cpp.ConverterOptionsProvider = () => _converterVM.GetConversionOptions();
            _converterVM.PropertyChanged += (_, _) => DebouncedRefresh(N2NCPreview!);
        }

        _dpVM = DPSettingsHost?.DataContext as DPToolViewModel;
        if (_dpVM != null && DPPreview?.Processor is DPPreviewProcessor dpp)
        {
            dpp.DPOptionsProvider = () => _dpVM.Options;
            _dpVM.PropertyChanged += (_, _) => DebouncedRefresh(DPPreview!);
            _dpVM.Options.PropertyChanged += (_, _) => DebouncedRefresh(DPPreview!);
        }

        _lnVM = LNSettingsHost?.DataContext as LNTransformerViewModel;
        if (_lnVM != null && LNPreview?.Processor is LNPreviewProcessor lnp)
        {
            lnp.LNParamsProvider = () => _lnVM.GetLNPreviewParameters();
            _lnVM.PropertyChanged += (_, _) => DebouncedRefresh(LNPreview!);
            _lnVM.Options.PropertyChanged += (_, _) => DebouncedRefresh(LNPreview!);
        }
        
        if (!string.IsNullOrEmpty(_internalOsuPath) && File.Exists(_internalOsuPath))
        {
            var arr = new[] { _internalOsuPath };
            if (_previewControls.TryGetValue(OptionsManager.N2NCToolName, out var convControl))
                convControl.LoadPreview(arr, true);
            if (_previewControls.TryGetValue(OptionsManager.LNToolName, out var lnControl))
                lnControl.LoadPreview(arr, true);
            if (_previewControls.TryGetValue(OptionsManager.DPToolName, out var dpControl))
                dpControl.LoadPreview(arr, true);
        }
    }

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

    private void LoadToolSettingsHosts()
    {
        // Converter 嵌入
        _convWindowInstance = new N2NCControl();
        var conv = _convWindowInstance;
        if (conv.Content is UIElement convContent && N2NCSettingsHost != null)
        {
            // 复制资源，以便 StaticResource/Style 查找仍然有效
            var keys = conv.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                // Skip implicit Window styles (they would affect the host/main window)
                var res = conv.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!N2NCSettingsHost.Resources.Contains(k))
                    N2NCSettingsHost.Resources.Add(k, conv.Resources[k]);
            }

            // 清除嵌入内容中的固定尺寸以便自适应
            ClearFixedSizes(convContent);

            // 将实际内容移入宿主，保留绑定和事件处理器
            N2NCSettingsHost.DataContext = conv.DataContext;
            N2NCSettingsHost.Content = convContent;
            // 清空原窗口内容，使元素只有一个父级
            conv.Content = null;
        }

        // KRRLNTransformer 嵌入
        _krrLnTransformerInstance = new KRRLNTransformerControl();
        var krrLn = _krrLnTransformerInstance;
        if (krrLn.Content is UIElement krrLnContent && KRRLNSettingsHost != null)
        {
            // 复制资源，以便 StaticResource/Style 查找仍然有效
            var keys = krrLn.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                // Skip implicit Window styles (they would affect the host/main window)
                var res = krrLn.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!KRRLNSettingsHost.Resources.Contains(k))
                    KRRLNSettingsHost.Resources.Add(k, krrLn.Resources[k]);
            }

            // 清除嵌入内容中的固定尺寸以便自适应
            ClearFixedSizes(krrLnContent);

            // 将实际内容移入宿主，保留绑定和事件处理器
            KRRLNSettingsHost.DataContext = krrLn.DataContext;
            KRRLNSettingsHost.Content = krrLnContent;
            // 清空原窗口内容，使元素只有一个父级
            krrLn.Content = null;
        }
        
        
        // LNTransformer 嵌入
        _lnWindowInstance = new LNTransformerControl();
        var ln = _lnWindowInstance;
        if (ln.Content is UIElement lnContent && LNSettingsHost != null)
        {
            // 复制资源
            var keys = ln.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                // Skip implicit Window styles
                var res = ln.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!LNSettingsHost.Resources.Contains(k))
                    LNSettingsHost.Resources.Add(k, ln.Resources[k]);
            }

            ClearFixedSizes(lnContent);

            LNSettingsHost.DataContext = ln.DataContext;
            LNSettingsHost.Content = lnContent;
            ln.Content = null;
        }

        // DP Tool 嵌入
        _dpWindowInstance = new DPToolControl();
        var dp = _dpWindowInstance;
        if (dp.Content is UIElement dpContent && DPSettingsHost != null)
        {
            // 复制资源，以便 StaticResource/Style 查找仍然有效
            var keys = dp.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                // Skip implicit Window styles
                var res = dp.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!DPSettingsHost.Resources.Contains(k))
                    DPSettingsHost.Resources.Add(k, dp.Resources[k]);
            }

            ClearFixedSizes(dpContent);

            DPSettingsHost.DataContext = dp.DataContext;
            DPSettingsHost.Content = dpContent;
            dp.Content = null;
        }

        // LV Calculator 嵌入
        var lvWin = new KrrLVControl();
        if (lvWin.Content is UIElement lvContent && LVCalSettingsHost != null)
        {
            var keys = lvWin.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                var res = lvWin.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!LVCalSettingsHost.Resources.Contains(k))
                    LVCalSettingsHost.Resources.Add(k, lvWin.Resources[k]);
            }

            ClearFixedSizes(lvContent);

            LVCalSettingsHost.DataContext = lvWin.DataContext;
            LVCalSettingsHost.Content = lvContent;
            lvWin.Content = null;
        }

        // osu! file manager 嵌入
        var getFilesWin = new FilesManagerControl();
        if (getFilesWin.Content is UIElement gfContent && FilesManagerHost != null)
        {
            var keys = getFilesWin.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                var res = getFilesWin.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!FilesManagerHost.Resources.Contains(k))
                    FilesManagerHost.Resources.Add(k, getFilesWin.Resources[k]);
            }

            ClearFixedSizes(gfContent);

            FilesManagerHost.DataContext = getFilesWin.DataContext;
            FilesManagerHost.Content = gfContent;
            getFilesWin.Content = null;
        }
    }

    // 窗口级别拖放：转发到全局处理器
    private void GlobalDropArea_Drop(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = e.Data.GetData(DataFormats.FileDrop) as string[] ?? [];
        var allOsu = paths.SelectMany(path =>
        {
            if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".osu", StringComparison.OrdinalIgnoreCase))
                return [path];
            if (Directory.Exists(path))
                try
                {
                    return Directory.EnumerateFiles(path, "*.osu", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed enumerating {path}: {ex.Message}");
                }

            return [];
        }).ToList();

        if (!allOsu.Any())
        {
            MessageBox.Show(Strings.NoOsuFilesFound.Localize());
            return;
        }

        _fileDispatcher.LoadFiles(allOsu.ToArray());
    }

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

    private void DetachTab(TabViewItem tab)
    {
        if (!MainTabControl.Items.Contains(tab)) return;

        var toolKey = tab.Tag.ToString();
        // Only allow LV and GetFiles tools to detach
        if (toolKey != OptionsManager.LVCalToolName && toolKey != OptionsManager.FilesManagerToolName) return;

        var header = tab.Header?.ToString() ?? "Detached";

        Func<ContentControl> createFreshWindow = toolKey switch
        {
            OptionsManager.LVCalToolName => () => new KrrLVControl(),
            OptionsManager.FilesManagerToolName => () => new FilesManagerControl(),
            _ => throw new ArgumentOutOfRangeException()
        };

        var control = createFreshWindow();
        var win = new Window
        {
            Title = header,
            Content = control,
            Width = 800,
            Height = 600
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

    private string? ResolveInternalSample()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            var direct = Path.Combine(baseDir, "mania-last-object-not-latest.osu");
            if (File.Exists(direct)) return direct;
            var dir = new DirectoryInfo(baseDir);
            for (var i = 0; i < 6; i++)
            {
                if (dir == null) break;
                var candidate = Path.Combine(dir.FullName, "tools", "Preview",
                    "mania-last-object-not-latest.osu");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("未找到内置.osu预览文件" + ex.Message);
        }

        return null;
    }

    private void ConverterPreview_StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;

        // Filter out internal sample (preview default) so it is never converted
        var toProcess = paths.Where(p => !string.Equals(p, _internalOsuPath, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (toProcess.Length == 0) return;

        _fileDispatcher.ConvertFiles(toProcess, OptionsManager.N2NCToolName);
    }

    private void LNPreview_StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;

        // Filter out internal sample and invalid files
        var toProcess = paths.Where(p => !string.Equals(p, _internalOsuPath, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (toProcess.Length == 0) return;

        _fileDispatcher.ConvertFiles(toProcess, OptionsManager.LNToolName);
    }

    private void DPPreview_StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;

        // Filter out internal sample and invalid files
        var toProcess = paths.Where(p => !string.Equals(p, _internalOsuPath, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (toProcess.Length == 0) return;

        _fileDispatcher.ConvertFiles(toProcess, OptionsManager.DPToolName);
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
        var sourceId = 0;
        switch (selectedTab?.Tag as string)
        {
            case OptionsManager.N2NCToolName:
                source = _convWindowInstance;
                sourceId = 1;
                break;
            case OptionsManager.LNToolName:
                source = _lnWindowInstance;
                sourceId = 2;
                break;
            case OptionsManager.DPToolName:
                source = _dpWindowInstance;
                sourceId = 3;
                break;
        }

        var listenerControl = new ListenerControl(source, sourceId)
        {
            RealTimePreview = RealTimePreview
        };
        _currentListenerWindow = new Window
        {
            Title = Strings.ListenerTitlePrefix,
            Content = listenerControl,
            Width = 800,
            Height = 600
        };
        _currentListenerWindow.Closed += (_, _) => _currentListenerWindow = null;
        _currentListenerWindow.Show();
    }

    private void MainWindow_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.B)
        {
            var target = _currentAlpha > 0x55 ? (byte)0x44 : (byte)0xCC;
            _currentAlpha = target;
            SharedUIComponents.SetPanelBackgroundAlpha(_currentAlpha);
            if (_alphaSlider != null) _alphaSlider.Value = _currentAlpha;
            e.Handled = true;
        }
        {
            _alphaIndex = (_alphaIndex + 1) % _alphaCycle.Length;
            var next = _alphaCycle[_alphaIndex];
            _currentAlpha = next;
            SharedUIComponents.SetPanelBackgroundAlpha(next);
            if (_alphaSlider != null) _alphaSlider.Value = next;
            e.Handled = true;
        }
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
                if (N2NCPreview != null)
                {
                    N2NCPreview.LoadPreview(arr, suppressBroadcast: true);
                    N2NCPreview.ApplyDropZoneStagedUI(arr);
                }
                if (LNPreview != null)
                {
                    LNPreview.LoadPreview(arr, suppressBroadcast: true);
                    LNPreview.ApplyDropZoneStagedUI(arr);
                }
                if (DPPreview != null)
                {
                    DPPreview.LoadPreview(arr, suppressBroadcast: true);
                    DPPreview.ApplyDropZoneStagedUI(arr);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Listener preview broadcast failed: {ex.Message}");
            }
        }));
    }

    private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        var selectedTag = (MainTabControl.SelectedItem as TabItem)?.Tag as string;
        var isConverter = selectedTag is OptionsManager.N2NCToolName or OptionsManager.LNToolName or OptionsManager.DPToolName;
        if (_previewControls.TryGetValue("Global", out var preview))
        {
            preview.Visibility = isConverter ? Visibility.Visible : Visibility.Collapsed;
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
    }
}