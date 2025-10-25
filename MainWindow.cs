using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using krrTools.Beatmaps;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using Wpf.Ui.Controls;
using Binding = System.Windows.Data.Binding;
using Border = Wpf.Ui.Controls.Border;
using Control = System.Windows.Forms.Control;
using Grid = Wpf.Ui.Controls.Grid;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace krrTools
{
    public class MainWindow : FluentWindow
    {
        private readonly Dictionary<object, ContentControl> _settingsHosts = new Dictionary<object, ContentControl>();
        private readonly Grid root;
        private readonly TabView MainTabControl;
        private readonly Grid _mainGrid;

        private PreviewViewDual _previewDual = null!;
        private ContentControl _currentSettingsContainer = null!;
        private readonly StateBarManager _StateBarManager;

        // 全局快捷键管理器
        private ConversionHotkeyManager? _conversionHotkeyManager;

        // Snackbar服务
        private SnackbarPresenter _snackbarPresenter = null!;

        // 跟踪选项卡拖动/分离
        private Point _dragStartPoint;
        private DateTime _dragStartTime;
        private TabViewItem? _draggedTab;

        // 工具调度器
        private IModuleManager ModuleManager { get; }

        private N2NCView _convWindowInstance = null!;
        private DPToolView _dpToolWindowInstance = null!;
        private KRRLNTransformerView _krrLnTransformerInstance = null!;
        private ListenerControl _listenerControlInstance = null!;

        private readonly Dictionary<ConverterEnum, Func<IToolOptions>> _optionsProviders = new Dictionary<ConverterEnum, Func<IToolOptions>>();
        private readonly Dictionary<ConverterEnum, Func<object?>> _viewModelGetters = new Dictionary<ConverterEnum, Func<object?>>();

        private ContentControl? N2NCSettingsHost
        {
            get => _settingsHosts.GetValueOrDefault(ConverterEnum.N2NC);
        }
        private ContentControl? DPSettingsHost
        {
            get => _settingsHosts.GetValueOrDefault(ConverterEnum.DP);
        }
        private ContentControl? KRRLNSettingsHost
        {
            get => _settingsHosts.GetValueOrDefault(ConverterEnum.KRRLN);
        }
        private ContentControl? LVCalSettingsHost
        {
            get => _settingsHosts.GetValueOrDefault(ModuleEnum.LVCalculator);
        }
        private ContentControl? FilesManagerHost
        {
            get => _settingsHosts.GetValueOrDefault(ModuleEnum.FilesManager);
        }
        private ContentControl? ListenerSettingsHost
        {
            get => _settingsHosts.GetValueOrDefault(ModuleEnum.Listener);
        }

        public MainWindow(IModuleManager moduleManager)
        {
            ModuleManager = moduleManager;

            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            // 从服务容器获取状态管理器
            _StateBarManager = App.Services.GetRequiredService<StateBarManager>();

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

            BuildUI();
            LoadToolSettingsHosts();
            InitializeProviders();

            Loaded += ApplyToThemeLoaded;
            MainTabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
            MainTabControl.PreviewMouseMove += TabControl_PreviewMouseMove;
            MainTabControl.PreviewMouseLeftButtonUp += TabControl_PreviewMouseLeftButtonUp;
            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

            // 添加窗口关闭时的资源清理
            Closed += MainWindow_Closed;
        }

        private void InitializeGlobalHotkeys()
        {
            _conversionHotkeyManager = new ConversionHotkeyManager(ExecuteConvertWithModule, this);

            // 订阅监听状态变化
            var eventBus = App.Services.GetService(typeof(IEventBus)) as IEventBus;
            eventBus?.Subscribe<MonitoringEnabledChangedEvent>(OnMonitoringEnabledChanged);

            // 如果监听已启用，注册快捷键
            GlobalSettings globalSettings = BaseOptionsManager.GetGlobalSettings();
            if (globalSettings.MonitoringEnable.Value) _conversionHotkeyManager.RegisterHotkeys(globalSettings);
        }

        private void ExecuteConvertWithModule(ConverterEnum converter)
        {
            // 获取监听ViewModel中的当前谱面路径, 变量是持久化的，所以这里一定有值
            string beatmapPath = _listenerControlInstance.ViewModel.MonitorOsuFilePath;

            try
            {
                // 解码谱面，并确保是有效的 Mania 谱面
                Beatmap? beatmap = BeatmapDecoder.Decode(beatmapPath).GetManiaBeatmap();

                if (beatmap == null)
                {
                    Logger.WriteLine(LogLevel.Error, $"{beatmapPath} is not a supported beatmap. And Skipped.");
                    return;
                }

                // 初始化转换服务
                var moduleManager = App.Services.GetService(typeof(IModuleManager)) as IModuleManager;
                var transformationService = new BeatmapTransformationService(moduleManager!);

                // 使用转换服务
                Beatmap transformedBeatmap = transformationService.TransformBeatmap(beatmap, converter);

                // 获取工具检查是否有变化
                IToolModule? tool = moduleManager?.GetToolByName(converter.ToString());
                bool hasChanges = true; // 默认假设有变化

                if (tool != null)
                {
                    dynamic dynamicTool = tool;
                    hasChanges = dynamicTool.HasChanges;
                }

                if (!hasChanges)
                {
                    // 没有实际转换，跳过保存和打包
                    Logger.WriteLine(LogLevel.Information, $"谱面无需转换，已跳过: {beatmapPath}");
                    return;
                }

                // 保存转换后谱面
                string outputPath = transformedBeatmap.GetOutputOsuFileName();
                string? outputDir = Path.GetDirectoryName(beatmapPath);
                string fullOutputPath = Path.Combine(outputDir!, outputPath);

                // 限制完整路径长度不超过255个字符，注意getdirectoryname已经标准化非法字符，不需要重新判断
                if (fullOutputPath.Length > 255)
                {
                    // 直接处理路径超长情况
                    if (fullOutputPath.Length > 255)
                    {
                        // 去掉最后的".osu"扩展名
                        string pathWithoutExtension = fullOutputPath.Substring(0, fullOutputPath.Length - 4);

                        // 截取到247个字符，然后加上".osu"
                        fullOutputPath = pathWithoutExtension.Substring(0, 247) + "....osu";
                    }
                }

                transformedBeatmap.Save(fullOutputPath);

                // 打包成.osz并打开
                string? directoryPath = Path.GetDirectoryName(fullOutputPath);
                string? directoryName = Path.GetFileName(directoryPath);
                if (directoryPath == null || directoryName == null)
                    throw new ArgumentException("Invalid path structure");

                string zipFilePath = Path.Combine(directoryPath, $"{directoryName}.osz");

                if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

                using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create)) archive.CreateEntryFromFile(fullOutputPath, Path.GetFileName(fullOutputPath));
                File.Delete(fullOutputPath);

                Console.WriteLine($"已创建 {zipFilePath}");

                string songsPath = BaseOptionsManager.GetGlobalSettings().SongsPath.Value;
                string? osuDir = Path.GetDirectoryName(songsPath);
                if (osuDir == null)
                    throw new InvalidOperationException("Invalid songs path");

                string osuExe = Path.Combine(osuDir, "osu!.exe");
                if (!File.Exists(osuExe))
                    throw new InvalidOperationException("osu!.exe not found");

                Process.Start(osuExe, zipFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Conversion failed: {ex.Message}", "Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnMonitoringEnabledChanged(MonitoringEnabledChangedEvent evt)
        {
            if (_conversionHotkeyManager == null) return;

            if (evt.NewValue)
            {
                // 监听启用，注册快捷键
                GlobalSettings globalSettings = BaseOptionsManager.GetGlobalSettings();
                _conversionHotkeyManager.RegisterHotkeys(globalSettings);
            }
            else
            {
                // 监听禁用，注销快捷键
                _conversionHotkeyManager.UnregisterAllHotkeys();
            }
        }

        private Func<IToolOptions> GetOptionsProviderForConverter(ConverterEnum converter)
        {
            return _optionsProviders.GetValueOrDefault(converter, () => new N2NCOptions());
        }

        private object? GetViewModelForConverter(ConverterEnum converter)
        {
            return _viewModelGetters.GetValueOrDefault(converter, () => null).Invoke();
        }

        private void InitializeProviders()
        {
            _optionsProviders[ConverterEnum.N2NC] = () => (_convWindowInstance.DataContext as N2NCViewModel)?.Options ?? new N2NCOptions();
            _optionsProviders[ConverterEnum.DP] = () => (_dpToolWindowInstance.DataContext as DPToolViewModel)?.Options ?? new DPToolOptions();
            _optionsProviders[ConverterEnum.KRRLN] = () => (_krrLnTransformerInstance.DataContext as KRRLNTransformerViewModel)?.Options ?? new KRRLNTransformerOptions();

            _viewModelGetters[ConverterEnum.N2NC] = () => _convWindowInstance.DataContext;
            _viewModelGetters[ConverterEnum.DP] = () => _dpToolWindowInstance.DataContext;
            _viewModelGetters[ConverterEnum.KRRLN] = () => _krrLnTransformerInstance.DataContext;
        }

        private void BuildUI()
        {
            // 设置内容容器
            _currentSettingsContainer = new ContentControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            BuildTabs(Enum.GetValues<ConverterEnum>(), converter => converter switch
                                                                    {
                                                                        ConverterEnum.N2NC => Strings.TabN2NC,
                                                                        ConverterEnum.KRRLN => Strings.TabKRRsLN,
                                                                        ConverterEnum.DP => Strings.TabDPTool,
                                                                        _ => converter.ToString()
                                                                    }, true);

            BuildTabs(Enum.GetValues<ModuleEnum>(), module => module switch
                                                              {
                                                                  ModuleEnum.LVCalculator => Strings.TabKrrLV,
                                                                  ModuleEnum.FilesManager => Strings.TabFilesManager,
                                                                  ModuleEnum.Listener => Strings.OSUListener,
                                                                  _ => module.ToString()
                                                              }, false);

            Grid previewGrid = BuildPreview();
            StatusBarControl footer = BuildFooter();

            // 设置Grid行
            System.Windows.Controls.Grid.SetRow(MainTabControl, 1);
            System.Windows.Controls.Grid.SetColumnSpan(MainTabControl, 2); // 跨越两列
            System.Windows.Controls.Grid.SetRow(_mainGrid, 2);
            System.Windows.Controls.Grid.SetRow(footer, 3);

            System.Windows.Controls.Grid.SetColumn(_currentSettingsContainer, 0);
            System.Windows.Controls.Grid.SetRow(_previewDual, 0);
            System.Windows.Controls.Grid.SetRow(previewGrid.Children[1], 1); // fileDropZone
            System.Windows.Controls.Grid.SetColumn(previewGrid, 1);

            _mainGrid.Children.Add(_currentSettingsContainer);
            _mainGrid.Children.Add(previewGrid);
            root.Children.Add(new TitleBar { Title = Strings.WindowTitle });
            root.Children.Add(MainTabControl);
            root.Children.Add(_mainGrid);
            root.Children.Add(footer);

            // 添加SnackbarPresenter
            _snackbarPresenter = new SnackbarPresenter();
            root.Children.Add(_snackbarPresenter);

            AllowDrop = true;
        }

        private void BuildTabs<T>(IEnumerable<T> enums, Func<T, string> getHeader, bool hasPreview) where T : Enum
        {
            foreach (T cfg in enums)
            {
                TextBlock headerLabel = SharedUIComponents.CreateHeaderLabel(getHeader(cfg));
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

                if (hasPreview)
                {
                    var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                    settingsHost.Content = null;
                    scroll.Content = settingsHost;
                    SharedUIComponents.CreateStandardPanel(scroll, new Thickness(8));
                }
                else
                    settingsHost.AllowDrop = true;

                MainTabControl.Items.Add(tab);
            }
        }

        private Grid BuildPreview()
        {
            // 全局预览器
            var previewViewModel = new PreviewViewModel();
            _previewDual = new PreviewViewDual(previewViewModel);

            var fileDispatcher = new FileDispatcher
            {
                // 设置消息显示委托
                ShowMessage = (title, message) =>
                {
                    var snackbar = new Snackbar(_snackbarPresenter)
                    {
                        Title = title,
                        Content = message,
                        Appearance = ControlAppearance.Success,
                        Timeout = TimeSpan.FromSeconds(5)
                    };
                    snackbar.Show();
                }
            };
            var unused = new FileDropZoneViewModel(fileDispatcher)
            {
                PreviewDual = _previewDual,
                GetActiveTabTag = GetActiveTabTag
            };
            unused.InjectServices(); // 注入依赖项
            var fileDropZone = new FileDropZone(fileDispatcher)
            {
                ViewModel =
                {
                    GetActiveTabTag = GetActiveTabTag,
                    PreviewDual = _previewDual
                }
            };

            var previewGrid = new Grid();
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            previewGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border previewPanel = SharedUIComponents.CreateStandardPanel(_previewDual, new Thickness(8));
            previewGrid.Children.Add(previewPanel);
            previewGrid.Children.Add(fileDropZone);

            return previewGrid;
        }

        private StatusBarControl BuildFooter()
        {
            // Footer
            var statusBarControl = new StatusBarControl(_StateBarManager);

            var localizedRealTimeText = new DynamicLocalizedString(Strings.RealTimePreviewLabel);
            statusBarControl.RealTimeToggle.SetBinding(ContentProperty,
                                                       new Binding("Value") { Source = localizedRealTimeText });

            statusBarControl.TopmostToggle.Checked += (_, _) =>
            {
                SetTopmost(true);
                statusBarControl.TopmostToggle.Content = new SymbolIcon { Symbol = SymbolRegular.Pin20 };
            };
            statusBarControl.TopmostToggle.Unchecked += (_, _) =>
            {
                SetTopmost(false);
                statusBarControl.TopmostToggle.Content = new SymbolIcon { Symbol = SymbolRegular.PinOff20 };
            };

            // 监听冻结状态
            _StateBarManager.IsFrozen.OnValueChanged(frozen =>
            {
                Dispatcher.Invoke(() =>
                {
                    // TODO: 考虑是否要暂停预览刷新、监听等，也要评估复杂程度
                    if (frozen)
                    {
                        statusBarControl.RealTimeToggle.IsEnabled = false;
                        statusBarControl.TopmostToggle.IsEnabled = false;
                    }
                    else
                    {
                        statusBarControl.RealTimeToggle.IsEnabled = true;
                        statusBarControl.TopmostToggle.IsEnabled = true;
                    }
                });
            });

            // 监听隐藏状态
            _StateBarManager.IsHidden.OnValueChanged(hidden =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (hidden)
                        WindowState = WindowState.Minimized;
                    else
                    {
                        WindowState = WindowState.Normal;
                        Activate();
                    }
                });
            });

            // 添加SnackbarPresenter
            _snackbarPresenter = new SnackbarPresenter();

            return statusBarControl;
        }

        private void SetTopmost(bool value)
        {
            Topmost = value;
            _StateBarManager.SetTopmost(value);
        }

        private void ApplyToThemeLoaded(object sender, RoutedEventArgs e)
        {
            // 使用Dispatcher延迟应用主题，确保窗口完全初始化
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusBarControl.ApplyThemeSettings();
                InvalidateVisual(); // 强制重新绘制以应用主题

                // 初始化全局快捷键
                InitializeGlobalHotkeys();
            }), DispatcherPriority.ApplicationIdle);
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
                List<object> children = LogicalTreeHelper.GetChildren(element).OfType<object>().ToList();

                foreach (object child in children)
                    // 仅对 DependencyObject 子项递归
                {
                    if (child is DependencyObject dob)
                        ClearFixedSizes(dob);
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
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
            // 清除嵌入内容中的固定尺寸以便自适应
            ClearFixedSizes(toolControl);

            // 将整个工具控件嵌入到宿主，保留绑定和事件处理器
            host.DataContext = toolControl.DataContext;
            host.Content = toolControl;
            host.AllowDrop = true; // 允许拖拽事件传递到内容
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
                    ControlFactory = () => new ListenerControl(),
                    HostGetter = () => ListenerSettingsHost,
                    InstanceSetter = control => { _listenerControlInstance = (ListenerControl)control; }
                }
            };

            foreach (ToolEmbeddingConfig config in toolConfigs)
            {
                UserControl control = config.ControlFactory();
                ContentControl? host = config.HostGetter();

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

            Point pos = e.GetPosition(this);
            double dx = Math.Abs(pos.X - _dragStartPoint.X);
            double dy = Math.Abs(pos.Y - _dragStartPoint.Y);
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

            string? toolKey = tab.Tag.ToString();
            // 只用于ModuleEnum枚举模块
            if (!Enum.TryParse(typeof(ModuleEnum), toolKey, out _)) return;

            string header = tab.Header?.ToString() ?? "Detached";

            // 枚举到控件类型的映射
            var moduleControlMap = new Dictionary<ModuleEnum, Type>
            {
                { ModuleEnum.LVCalculator, typeof(KRRLVAnalysisView) },
                { ModuleEnum.FilesManager, typeof(FilesManagerView) },
                { ModuleEnum.Listener, typeof(ListenerControl) }
                // 如有其他 ModuleEnum 项，继续添加
            };

            if (!Enum.TryParse(toolKey, out ModuleEnum moduleEnum) ||
                !moduleControlMap.TryGetValue(moduleEnum, out Type? controlType))
                throw new ArgumentOutOfRangeException();

            UserControl CreateFreshWindow() => (UserControl)Activator.CreateInstance(controlType)!;

            UserControl control = CreateFreshWindow(); // 创建新控件实例
            var win = new Window // 独立窗口
            {
                Title = header,
                Content = control,
                Width = 800,
                Height = 600,
                Owner = this
            };

            System.Drawing.Point cursor = System.Windows.Forms.Cursor.Position;
            win.Left = cursor.X - 40;
            win.Top = cursor.Y - 10;

            int insertIndex = MainTabControl.Items.IndexOf(tab);
            MainTabControl.Items.Remove(tab);

            var followTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            followTimer.Tick += (_, _) =>
            {
                System.Drawing.Point p = System.Windows.Forms.Cursor.Position;
                win.Left = p.X - 40;
                win.Top = p.Y - 10;

                if ((Control.MouseButtons & MouseButtons.Left) !=
                    MouseButtons.Left)
                {
                    var tabPanel = FindVisualChild<TabPanel>(MainTabControl);
                    Rect headerRect;

                    if (tabPanel != null)
                    {
                        Point panelTopLeft = tabPanel.PointToScreen(new Point(0, 0));
                        headerRect = new Rect(panelTopLeft.X, panelTopLeft.Y, tabPanel.ActualWidth,
                                              tabPanel.ActualHeight);
                    }
                    else
                    {
                        Point topLeft = MainTabControl.PointToScreen(new Point(0, 0));
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

        private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
        {
            object? selectedTag = (MainTabControl.SelectedItem as TabViewItem)?.Tag;
            bool isConverter = selectedTag is ConverterEnum;

            if (selectedTag != null)
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
                    object? viewModel = GetViewModelForConverter(converterEnum);

                    if (viewModel != null)
                    {
                        // 只设置DataContext，不触发刷新
                        _previewDual.DataContext = viewModel;

                        // 通过ViewModel设置处理器，它会处理刷新
                        _previewDual.ViewModel!.SetProcessor(processor);
                        _previewDual.SetCurrentTool(converterEnum);

                        // 选项卡切换时立即刷新预览
                        _previewDual.ViewModel!.TriggerRefresh();
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
            _currentSettingsContainer.Content = null;

            if (selectedTag != null && _settingsHosts.TryGetValue(selectedTag, out ContentControl? settingsHost))
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

        // 处理窗口关闭时的资源清理
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // 清理预览ViewModel的资源
            if (_previewDual.ViewModel is IDisposable disposableViewModel) disposableViewModel.Dispose();

            // 清理ListenerViewModel的资源
            if (_listenerControlInstance.ViewModel is IDisposable listenerDisposable) listenerDisposable.Dispose();

            // 清理工具ViewModel的资源
            if (_convWindowInstance.DataContext is IDisposable n2ncDisposable)
                n2ncDisposable.Dispose();

            if (_dpToolWindowInstance.DataContext is IDisposable dpDisposable)
                dpDisposable.Dispose();

            if (_krrLnTransformerInstance.DataContext is IDisposable krrlnDisposable)
                krrlnDisposable.Dispose();

            // // 清理KRRLVAnalysisViewModel的资源
            // if (LVCalSettingsHost?.DataContext is IDisposable lvAnalysisDisposable)
            //     lvAnalysisDisposable.Dispose();

            // 清理其他可能需要释放的资源
        }
    }
}
