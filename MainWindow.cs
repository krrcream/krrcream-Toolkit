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
using krrTools.Bindable;
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
using Microsoft.Extensions.Logging;
using Grid = Wpf.Ui.Controls.Grid;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Wpf.Ui.Controls;

namespace krrTools
{
    public class MainWindow : FluentWindow
    {
        private readonly IEventBus _eventBus;
        private readonly Dictionary<object, ContentControl> _settingsHosts = new();
        private readonly Grid root;
        private readonly TabView MainTabControl;
        private readonly Grid _mainGrid;

        private PreviewViewDual? _previewDual;
        private ContentControl? _currentSettingsContainer;
        private ListenerViewModel? _listenerVM;

        // 跟踪选项卡拖动/分离
        private Point _dragStartPoint;
        private DateTime _dragStartTime;
        private TabViewItem? _draggedTab;
        public TabView TabControl => MainTabControl;

        public PreviewViewDual? PreviewDualControl => _previewDual;

        public FileDropZone? FileDropZone => _fileDropZone;

        private FileDropZone? _fileDropZone;

        // 工具调度器
        private IModuleManager ModuleManager { get; }

        // private object? _currentTool;
        // private string? _internalOsuPath;
        // private string? _lastProcessedBackgroundPath;
        private bool _realTimePreview;
        public bool RealTimePreviewEnabled => _realTimePreview;

        private bool RealTimePreview
        {
            get => _realTimePreview;
            set
            {
                if (_realTimePreview != value)
                {
                    _realTimePreview = value;
                    BaseOptionsManager.SetRealTimePreview(_realTimePreview);
                    OnRealTimePreviewChanged();
                }
            }
        }

        private void LoadRealTimePreview()
        {
            _realTimePreview = BaseOptionsManager.GetRealTimePreview();
        }

        private N2NCView _convWindowInstance = null!;
        private DPToolView _dpToolWindowInstance = null!;
        private KRRLNTransformerView _krrLnTransformerInstance = null!;
        private ListenerControl _listenerControlInstance = null!;

        private ContentControl? N2NCSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.N2NC);
        private ContentControl? DPSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.DP);
        private ContentControl? KRRLNSettingsHost => _settingsHosts.GetValueOrDefault(ConverterEnum.KRRLN);
        private ContentControl? LVCalSettingsHost => _settingsHosts.GetValueOrDefault(ModuleEnum.LVCalculator);
        private ContentControl? FilesManagerHost => _settingsHosts.GetValueOrDefault(ModuleEnum.FilesManager);
        private ContentControl? ListenerSettingsHost => _settingsHosts.GetValueOrDefault(ModuleEnum.Listener);

        public FileDropZoneViewModel? FileDropZoneViewModel => null;

        public MainWindow(IModuleManager moduleManager, IEventBus eventBus)
        {
            _eventBus = eventBus;
            ModuleManager = moduleManager;

            Title = Strings.WindowTitle; // 初始化
            Width = 1000;
            Height = 750;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Content = root = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto }, // 标题栏
                    new RowDefinition { Height = GridLength.Auto }, // 选项卡行
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // 主内容区域（设置 + 预览）
                    new RowDefinition { Height = GridLength.Auto } // Footer状态栏行
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
            BuildUI();
            OnRealTimePreviewChanged();

            LoadToolSettingsHosts();

            Loaded += ApplyToThemeLoaded;
            MainTabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
            MainTabControl.PreviewMouseMove += TabControl_PreviewMouseMove;
            MainTabControl.PreviewMouseLeftButtonUp += TabControl_PreviewMouseLeftButtonUp;
            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

            // 添加窗口关闭时的资源清理
            Closed += MainWindow_Closed;
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
            var previewViewModel = new PreviewViewModel();
            _previewDual = new PreviewViewDual(previewViewModel);

            // 创建 FileDropZoneViewModel
            var fileDispatcher = new FileDispatcher();
            var fileDropZoneViewModel = new FileDropZoneViewModel(_previewDual, fileDispatcher, GetActiveTabTag);
            var fileDropZone = new FileDropZone(fileDropZoneViewModel);
            _fileDropZone = fileDropZone;
            // fileDropZoneViewModel.FilesDropped += OnFilesDropped;
            // fileDropZoneViewModel.StartConversionRequested += StartConversionRequested;

            var previewGrid = new Grid();
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var previewPanel = SharedUIComponents.CreateStandardPanel(_previewDual, new Thickness(8));
            previewGrid.Children.Add(previewPanel);
            previewGrid.Children.Add(fileDropZone);

            // Footer
            var statusBarControl = new StatusBarControl
            {
                RealTimeToggle =
                {
                    IsChecked = RealTimePreview
                }
            };

            statusBarControl.RealTimeToggle.Checked += (_, _) => RealTimePreview = true;
            statusBarControl.RealTimeToggle.Unchecked += (_, _) => RealTimePreview = false;

            var localizedRealTimeText = new DynamicLocalizedString(Strings.RealTimePreviewLabel);
            statusBarControl.RealTimeToggle.SetBinding(ContentProperty,
                new Binding("Value") { Source = localizedRealTimeText });

            statusBarControl.TopmostToggle.Checked += (_, _) =>
            {
                Topmost = true;
                statusBarControl.TopmostToggle.Content = new SymbolIcon { Symbol = SymbolRegular.Pin20 };
                statusBarControl.TopmostToggle.ToolTip = "取消置顶";
            };
            statusBarControl.TopmostToggle.Unchecked += (_, _) =>
            {
                Topmost = false;
                statusBarControl.TopmostToggle.Content = new SymbolIcon { Symbol = SymbolRegular.PinOff20 };
                statusBarControl.TopmostToggle.ToolTip = "置顶窗口";
            };

            var footer = statusBarControl;

            // 设置Grid行
            System.Windows.Controls.Grid.SetRow(MainTabControl, 1);
            System.Windows.Controls.Grid.SetColumnSpan(MainTabControl, 2); // 跨越两列
            System.Windows.Controls.Grid.SetRow(_mainGrid, 2);
            System.Windows.Controls.Grid.SetRow(footer, 3);

            System.Windows.Controls.Grid.SetColumn(settingsContainer, 0);
            System.Windows.Controls.Grid.SetRow(previewPanel, 0);
            System.Windows.Controls.Grid.SetRow(fileDropZone, 1);
            System.Windows.Controls.Grid.SetColumn(previewGrid, 1);

            _mainGrid.Children.Add(settingsContainer);
            _mainGrid.Children.Add(previewGrid);
            root.Children.Add(new TitleBar { Title = Strings.WindowTitle });
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
                StatusBarControl.ApplyThemeSettings();
                InvalidateVisual(); // 强制重新绘制以应用主题
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
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
                    ModuleEnum.Listener => Strings.OSUListener,
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
                    HostGetter = () => LVCalSettingsHost
                },
                new ToolEmbeddingConfig
                {
                    ControlFactory = () => new FilesManagerView(),
                    HostGetter = () => FilesManagerHost
                },
                new ToolEmbeddingConfig
                {
                    ControlFactory = () => new ListenerControl(_eventBus),
                    HostGetter = () => ListenerSettingsHost,
                    InstanceSetter = control =>
                    {
                        _listenerControlInstance = (ListenerControl)control;
                        _listenerControlInstance.ViewModel.PropertyChanged += ListenerViewModel_PropertyChanged;
                    }
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
            }
        }

        #endregion

        // 选项卡拖动/分离处理 - 克隆内容用于分离窗口，保持原选项卡不变
        private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _dragStartTime = DateTime.Now;
            // 查找鼠标下的 Tab
            var source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is TabViewItem)) source = VisualTreeHelper.GetParent(source);

            _draggedTab = source as TabViewItem;
        }

        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedTab == null) return;
            
            // 检查按下时间是否超过阈值，避免误触
            if ((DateTime.Now - _dragStartTime).TotalMilliseconds < 300) return;
            
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

        private void TabControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _draggedTab = null;
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
                { ModuleEnum.Listener, typeof(ListenerControl) }
                // 如有其他 ModuleEnum 项，继续添加
            };

            if (!Enum.TryParse(toolKey, out ModuleEnum moduleEnum) ||
                !moduleControlMap.TryGetValue(moduleEnum, out var controlType))
                throw new ArgumentOutOfRangeException();

            UserControl CreateFreshWindow()
            {
                return (UserControl)Activator.CreateInstance(controlType)!;
            }

            var control = CreateFreshWindow(); // 创建新控件实例
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

        private ConverterEnum GetActiveTabTag()
        {
            return (MainTabControl.SelectedItem as TabViewItem)?.Tag is ConverterEnum converter
                ? converter
                : ConverterEnum.N2NC;
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

                // 立即触发一次检查，以加载当前选中的谱面
                _ = _listenerVM.TriggerImmediateCheck();
                // 移除直接调用SetSongsPath的逻辑，让ListenerViewModel自动检测osu!进程并获取Songs路径
                // if (string.IsNullOrEmpty(_listenerVM.Config.SongsPath))
                // {
                //     _listenerVM.SetSongsPath();
                // }
            }
            else
            {
                if (_listenerVM != null)
                {
                    _listenerVM.Cleanup();
                    _listenerVM = null;
                }

                // 当关闭实时预览时，重置预览到默认内置样本
                _previewDual?.ResetPreview();
                // 清除拖入区的监听文件
                _fileDropZone?.SetManualMode(null);
            }
        }

        private void OnBeatmapSelected(object? sender, ListenerViewModel.BeatmapInfo e)
        {
            if (string.IsNullOrEmpty(e.FilePath) || !File.Exists(e.FilePath)) return;

            try
            {
                _fileDropZone?.SetManualMode([e.FilePath], FileDropZoneViewModel.FileSource.Listened);
            }
            catch (Exception ex)
            {
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("MainWindow")
                .LogError($"Error loading beatmap in real-time preview: {ex.Source}{ex.Message}{ex.StackTrace}");
            }
        }

        private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            var selectedTag = (MainTabControl.SelectedItem as TabViewItem)?.Tag;
            var isConverter = selectedTag is ConverterEnum;

            if (_previewDual != null && selectedTag != null)
            {
                _previewDual.Visibility = isConverter ? Visibility.Visible : Visibility.Collapsed;
                if (isConverter)
                {
                    var converterEnum = (ConverterEnum)selectedTag;

                    // 直接创建统一的处理器
                    var processor = new ConverterProcessor(ModuleManager, GetOptionsProviderForConverter(converterEnum))
                    {
                        ModuleTool = converterEnum
                    };

                    // 设置选项变化监听
                    var viewModel = GetViewModelForConverter(converterEnum);

                    if (viewModel != null)
                    {
                        // 只设置DataContext，不触发刷新
                        _previewDual.DataContext = viewModel;

                        // 通过ViewModel设置处理器，它会处理刷新
                        _previewDual.ViewModel!.SetProcessor(processor);
                        _previewDual.SetCurrentTool(converterEnum);
                        _previewDual.SetCurrentViewModel(viewModel);
                    }
                }
                else
                {
                    _previewDual.ViewModel!.SetProcessor(null);
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
            UpdateSettingsContainer(selectedTag, isConverter);
        }

        private void UpdateSettingsContainer(object? selectedTag, bool isConverter)
        {
            if (_currentSettingsContainer == null) return;

            _currentSettingsContainer.Content = null;

            if (selectedTag != null && _settingsHosts.TryGetValue(selectedTag, out var settingsHost))
            {
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

        private void ListenerViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentOsuFilePath")
            {
                if (sender is ListenerViewModel listenerVM && !string.IsNullOrEmpty(listenerVM.CurrentOsuFilePath))
                    _fileDropZone?.SetManualMode([listenerVM.CurrentOsuFilePath]);
                else
                    _fileDropZone?.SetManualMode(null);
            }
        }

        #region 辅助方法

        private Func<IToolOptions> GetOptionsProviderForConverter(ConverterEnum converter)
        {
            return converter switch
            {
                ConverterEnum.N2NC => () =>
                    (_convWindowInstance.DataContext as N2NCViewModel)?.Options ?? new N2NCOptions(),
                ConverterEnum.DP => () =>
                    (_dpToolWindowInstance.DataContext as DPToolViewModel)?.Options ?? new DPToolOptions(),
                ConverterEnum.KRRLN => () =>
                    (_krrLnTransformerInstance.DataContext as KRRLNTransformerViewModel)?.Options ??
                    new KRRLNTransformerOptions(),
                _ => () => new N2NCOptions() // 默认返回N2NC选项，避免null
            };
        }

        private object? GetViewModelForConverter(ConverterEnum converter)
        {
            return converter switch
            {
                ConverterEnum.N2NC => _convWindowInstance.DataContext,
                ConverterEnum.DP => _dpToolWindowInstance.DataContext,
                ConverterEnum.KRRLN => _krrLnTransformerInstance.DataContext,
                _ => null
            };
        }

        /// <summary>
        /// 窗口关闭时的资源清理
        /// </summary>
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // 清理预览ViewModel的资源
            if (_previewDual?.ViewModel is IDisposable disposableViewModel) disposableViewModel.Dispose();

            // 清理ListenerViewModel的资源
            if (_listenerVM is IDisposable listenerDisposable) listenerDisposable.Dispose();

            // 清理工具ViewModel的资源
            if (_convWindowInstance.DataContext is IDisposable n2ncDisposable)
                n2ncDisposable.Dispose();

            if (_dpToolWindowInstance.DataContext is IDisposable dpDisposable)
                dpDisposable.Dispose();

            if (_krrLnTransformerInstance.DataContext is IDisposable krrlnDisposable)
                krrlnDisposable.Dispose();

            // 清理其他可能需要释放的资源
        }

        #endregion
    }
}