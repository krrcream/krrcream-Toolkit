using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using krrTools.Tools.DPtool;
using krrTools.Tools.FilesManager;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.KRRLVAnalysis;
using krrTools.Tools.Listener;
using krrTools.Tools.N2NC;
using krrTools.Tools.Preview;
using krrTools.UI;
using krrTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuParsers.Decoders;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Grid = Wpf.Ui.Controls.Grid;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;

namespace krrTools;

public class MainWindow : FluentWindow
{
    private readonly Dictionary<object, ContentControl> _settingsHosts = new();
    private readonly Grid root;
    private readonly TabView MainTabControl;
    private readonly Grid _mainGrid;

    private ToggleButton GlobalOsuListenerButton = null!;
    private ToggleSwitch _realTimeToggle = null!;

    private PreviewViewDual? _previewDual;
    private ContentControl? _currentSettingsContainer;
    private ListenerViewModel? _listenerVM;
    private Window? _currentListenerWindow;

    // 跟踪选项卡拖动/分离
    private Point _dragStartPoint;
    private TabViewItem? _draggedTab;
    private DateTime _lastPreviewRefresh = DateTime.MinValue;

    private PropertyChangedEventHandler? _currentOptionsChangedHandler;

    private object? _currentTool;
    private string? _internalOsuPath;
    private bool _realTimePreview;
    public TabView TabControl => MainTabControl;
    // 文件调度器
    public readonly FileDispatcher _fileDispatcher;
    public FileDispatcher FileDispatcher => _fileDispatcher;
    // 工具调度器
    private IModuleManager ModuleManager { get; }

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

    private void DebouncedRefresh(PreviewViewDual control, int ms = 100)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastPreviewRefresh).TotalMilliseconds < ms) return;
        _lastPreviewRefresh = now;
        control.Refresh();
    }

    private N2NCView _convWindowInstance = null!;
    private DPToolView _dpToolWindowInstance = null!;
    private KRRLNTransformerView _krrLnTransformerInstance = null!;

    private ContentControl? N2NCSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.N2NC);
    private ContentControl? DPSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.DP);
    private ContentControl? KRRLNSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.KRRLN);
    private ContentControl? LVCalSettingsHost => _settingsHosts.GetValueOrDefault(ModuleEnum.LVCalculator);
    private ContentControl? FilesManagerHost => _settingsHosts.GetValueOrDefault(ModuleEnum.FilesManager);

    public PreviewViewDual? PreviewDualControl => _previewDual;

    public MainWindow()
    {
        Title = Strings.WindowTitle; // 初始化
        Width = 1000;
        Height = 750;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = root = new Grid()
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }, // 标题栏
                new RowDefinition { Height = GridLength.Auto }, // 选项卡行
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // 主内容区域（设置 + 预览）
                new RowDefinition { Height = GridLength.Auto }, // Footer状态栏行
            }        
        };
        MainTabControl = new TabView
        {
            Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(CustomTabPanel))),
            Height = 30 // 只显示选项卡头
        };
        _mainGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(320) }, // 设置 - 320宽
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } // 预览器 - 动态适应剩余宽度
            }
        };
        LoadRealTimePreview();
        OnRealTimePreviewChanged();
        BuildUI();

        LoadToolSettingsHosts();
        SetupPreviewProcessors();
        
        _fileDispatcher = new FileDispatcher(MainTabControl);
        ModuleManager = App.Services.GetRequiredService<IModuleManager>();
        
        Loaded += ApplyToThemeLoaded;
        MainTabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
        MainTabControl.PreviewMouseMove += TabControl_PreviewMouseMove;
        MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
    }

    private void BuildUI()
    {
        // 设置内容容器
        var settingsContainer = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _currentSettingsContainer = settingsContainer;

        // 预览器
        BuildPreviewTabs();
        BuildNoPreViewTabs();

        // 全局预览器
        _previewDual = new PreviewViewDual();

        var fileDropZone = new FileDropZone();
        fileDropZone.StartConversionRequested += StartConversionRequested;

        var previewGrid = new Grid();
        previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var previewPanel = SharedUIComponents.CreateStandardPanel(_previewDual, new Thickness(8));
        previewGrid.Children.Add(previewPanel);
        previewGrid.Children.Add(fileDropZone);
        
        // Footer
        _realTimeToggle = new ToggleSwitch
        {
            IsChecked = RealTimePreview,
            DataContext = new DynamicLocalizedString(Strings.RealTimePreviewLabel)
        };
        _realTimeToggle.SetBinding(ContentProperty, new Binding("Value"));
        _realTimeToggle.Checked += (_, _) => RealTimePreview = true;
        _realTimeToggle.Unchecked += (_, _) => RealTimePreview = false;

        var localizedListenerText = new DynamicLocalizedString(Strings.OSUListener);
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

        // 设置Grid行
        Grid.SetRow(MainTabControl, 1);
        Grid.SetColumnSpan(MainTabControl, 2); // 跨越两列
        Grid.SetRow(_mainGrid, 2);
        Grid.SetRow(footer, 3);
        
        Grid.SetColumn(settingsContainer, 0);
        Grid.SetRow(previewPanel, 0);
        Grid.SetRow(fileDropZone, 1);
        Grid.SetColumn(previewGrid, 1);
        
        _mainGrid.Children.Add(settingsContainer);
        _mainGrid.Children.Add(previewGrid);
        root.Children.Add(new TitleBar() { Title = Strings.WindowTitle });
        root.Children.Add(MainTabControl);
        root.Children.Add(_mainGrid);
        root.Children.Add(footer);
        
        AllowDrop = true;
        // 设置初始选项卡内容
        MainTabControl_SelectionChanged(null, null);
    }

    private void ApplyToThemeLoaded(object sender, RoutedEventArgs e)
    {
        // 使用Dispatcher延迟应用主题，确保窗口完全初始化
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var savedTheme = SharedUIComponents.GetSavedApplicationTheme() != null
                             && Enum.TryParse<ApplicationTheme>(SharedUIComponents.GetSavedApplicationTheme(), out var theme) ?
                theme : ApplicationTheme.Light;

            var savedBackdrop = SharedUIComponents.GetSavedWindowBackdropType() != null
                                && Enum.TryParse<WindowBackdropType>(SharedUIComponents.GetSavedWindowBackdropType(), out var backdrop) ?
                backdrop : WindowBackdropType.Acrylic;

            var savedAccent = SharedUIComponents.GetSavedUpdateAccent() ?? true;
            ApplicationThemeManager.Apply(savedTheme, savedBackdrop, savedAccent);
            InvalidateVisual(); // 强制重新绘制以应用主题
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    #region 创建带预览器选项卡
    private void BuildPreviewTabs()
    {
        foreach (var cfg in Enum.GetValuesAsUnderlyingType(
                     typeof(ConverterEnum)).Cast<ConverterEnum>())
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
        foreach (var cfg in Enum.GetValuesAsUnderlyingType(
                     typeof(ModuleEnum)).Cast<ModuleEnum>())
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

            settingsHost.AllowDrop = true;
            MainTabControl.Items.Add(tab);
        }
    }
    #endregion

    private void SetupPreviewProcessors()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var direct = Path.Combine(baseDir, "mania-PreView.osu");

        if (!File.Exists(direct))
        {
            var dir = new DirectoryInfo(baseDir);
            for (var i = 0; i < 6; i++)
            {
                if (dir == null) break;

                direct = Path.Combine(dir.FullName, "tools", "Preview",
                    "mania-PreView.osu");
                if (!File.Exists(direct))
                    dir = dir.Parent;
            }
        }

        _internalOsuPath = direct;
    }

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
    private class ToolEmbeddingConfig
    {
        public required Func<UserControl> ControlFactory { get; init; }
        public required Func<ContentControl?> HostGetter { get; init; }
        public Action<UserControl>? InstanceSetter { get; init; }
    }

    // 通用方法：将工具控件的内容嵌入到指定的宿主容器中
    private void EmbedTool(UserControl toolControl, ContentControl host)
    {
        if (toolControl.Content is UIElement content)
        {
            // 复制资源，以便 StaticResource/Style 查找仍然有效
            var keys = toolControl.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
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
            host.AllowDrop = true; // 允许拖拽事件传递到内容
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
                ControlFactory = () => new N2NCView(),
                HostGetter = () => N2NCSettingsHost,
                InstanceSetter = control => _convWindowInstance = (N2NCView)control
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new KRRLNTransformerView(),
                HostGetter = () => KRRLNSettingsHost,
                InstanceSetter = control => _krrLnTransformerInstance = (KRRLNTransformerView)control
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new DPToolView(),
                HostGetter = () => DPSettingsHost,
                InstanceSetter = control => _dpToolWindowInstance = (DPToolView)control
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new KRRLVAnalysisView(),
                HostGetter = () => LVCalSettingsHost,
            },
            new ToolEmbeddingConfig
            {
                ControlFactory = () => new FilesManagerView(),
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
            if (control is KRRLNTransformerView krrControl)
            {
                krrControl.SettingsChanged += (_, _) => RefreshGlobalPreviewIfCurrentTool(ConverterEnum.KRRLN);
            }
            else if (control is N2NCView n2ncControl)
            {
                n2ncControl.SettingsChanged += (_, _) => RefreshGlobalPreviewIfCurrentTool(ConverterEnum.N2NC);
            }
            else if (control is DPToolView dpControl)
            {
                dpControl.SettingsChanged += (_, _) => RefreshGlobalPreviewIfCurrentTool(ConverterEnum.DP);
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
        const double dragThreshold = 15.0; // 最小拖动距离以触发分离
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
        // 只用于ModuleEnum枚举模块
        if (!Enum.TryParse(typeof(ModuleEnum), toolKey, out _)) return;

        var header = tab.Header?.ToString() ?? "Detached";

        // 枚举到控件类型的映射
        var moduleControlMap = new Dictionary<ModuleEnum, Type>
        {
            { ModuleEnum.LVCalculator, typeof(KRRLVAnalysisView) },
            { ModuleEnum.FilesManager, typeof(FilesManagerView) },
            // 如有其他 ModuleEnum 项，继续添加
        };

        if (!Enum.TryParse(toolKey, out ModuleEnum moduleEnum) || !moduleControlMap.TryGetValue(moduleEnum, out var controlType))
            throw new ArgumentOutOfRangeException();

        ContentControl CreateFreshWindow() => (ContentControl)Activator.CreateInstance(controlType)!;

        var control = CreateFreshWindow(); // 创建新的控件实例
        var win = new Window // 独立窗口
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

    private void StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;

        // Get the currently active tool from the selected tab
        var selectedTag = (MainTabControl.SelectedItem as TabViewItem)?.Tag?.ToString();
        if (string.IsNullOrEmpty(selectedTag))
        {
            return;
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
        var sourceId = toolEnum is ConverterEnum converter ? BaseOptionsManager.GetSourceId(converter) : 0;

        // 使用自动映射获取控件实例
        switch (toolEnum)
        {
            case ConverterEnum.N2NC:
                source = _convWindowInstance;
                break;
            case ConverterEnum.DP:
                source = _dpToolWindowInstance;
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
            // 当关闭实时预览时，重置预览到默认内置样本
            _previewDual?.ResetToDefaultPreview();
        }
    }

    private void OnBeatmapSelected(object? sender, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (!File.Exists(filePath)) return;

                // 加载预览
                if (_currentTool is ConverterEnum && _previewDual != null)
                {
                    var beatmaps = BeatmapDecoder.Decode(filePath).GetManiaBeatmap();
                    beatmaps.FilePath = filePath;
                    _previewDual.LoadPreview(beatmaps);
                }
            }
            catch (Exception ex)
            {
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("MainWindow")
                    .LogError($"Error loading beatmap in real-time preview: {ex.Message}");
            }
        }));
    }

    private void RefreshGlobalPreviewIfCurrentTool(ConverterEnum toolEnum)
    {
        if (_previewDual != null)
        {
            _previewDual!.Refresh();
        }
    }

    private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        var selectedTag = (MainTabControl.SelectedItem as TabViewItem)?.Tag;
        var isConverter = selectedTag is ConverterEnum;
        
        if (_previewDual != null)
        {
            _previewDual.Visibility = isConverter ? Visibility.Visible : Visibility.Collapsed;
            if (isConverter)
            {
                // 直接创建统一的处理器
                var processor = new ConverterProcessor
                {
                    ToolScheduler = ModuleManager,
                    ConverterOptionsProvider = selectedTag switch
                    {
                        ConverterEnum.N2NC => () => (_convWindowInstance.DataContext as N2NCViewModel)?.Options ?? new N2NCOptions(),
                        ConverterEnum.DP => () => (_dpToolWindowInstance.DataContext as DPToolViewModel)?.Options ?? new DPToolOptions(),
                        ConverterEnum.KRRLN => () => (_krrLnTransformerInstance.DataContext as KRRLNTransformerViewModel)?.Options ?? new KRRLNTransformerOptions(),
                        _ => null
                    },
                    // CurrentTool = selectedTag.ToString()
                };
                _previewDual.Processor = processor;

                // 设置选项变化监听
                object? viewModel = selectedTag switch
                {
                    ConverterEnum.N2NC => _convWindowInstance.DataContext,
                    ConverterEnum.DP => _dpToolWindowInstance.DataContext,
                    ConverterEnum.KRRLN => _krrLnTransformerInstance.DataContext,
                    _ => null
                };

                // 设置 DataContext 以便监听属性变化
                _previewDual.DataContext = viewModel;

                var optionsProperty = viewModel?.GetType().GetProperty("Options");
                if (optionsProperty != null && optionsProperty.GetValue(viewModel) is INotifyPropertyChanged optionsNpc)
                {
                    _currentOptionsChangedHandler = (_, _) => DebouncedRefresh(_previewDual);
                    optionsNpc.PropertyChanged += _currentOptionsChangedHandler;
                }
                _previewDual.Refresh();
            }
            else
            {
                _previewDual.Processor = null;
            } // 非转换工具时清除处理器
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
        
        // 切换前先清空内容，避免重复父级
        if (_currentSettingsContainer != null)
        {
            _currentSettingsContainer.Content = null;

            if (selectedTag != null && _settingsHosts.TryGetValue(selectedTag, out var settingsHost))
            {
                // 只在未被当前容器持有时赋值
                if (isConverter)
                {
                    if (_currentSettingsContainer != settingsHost.Content)
                        _currentSettingsContainer.Content = settingsHost.Content;
                }
                else
                {
                    if (_currentSettingsContainer != settingsHost)
                        _currentSettingsContainer.Content = settingsHost;
                }
            }
        }

        _currentTool = selectedTag;
    }
}
