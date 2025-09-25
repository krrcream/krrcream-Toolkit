using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Shell;
using krrTools.Tools.Shared;
using krrTools.tools.Preview;
using krrTools.tools.DPtool;
using krrTools.tools.Get_files;
using krrTools.tools.KRR_LV;
using krrTools.tools.LNTransformer;
using krrTools.tools.N2NC;
using krrTools.tools.Shared;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace krrTools;

public class MainWindow : FluentWindow
{
    private TabControl MainTabControl = null!;
    private readonly Dictionary<string, ContentControl> _settingsHosts = new();
    private readonly Dictionary<string, DualPreviewControl> _previewControls = new();
    private Button? GlobalOsuListenerButton;

    // 标题栏控件
    private Button _minimizeButton = null!;
    private Button _maximizeButton = null!;
    private Button _closeButton = null!;
    private Border _titleBar = null!;
    private TextBlock? _titleTextBlock;

    // 跟踪选项卡拖动/分离
    private Point _dragStartPoint;
    private TabItem? _draggedTab;

    private N2NCViewModel? _converterVM;
    private DPToolViewModel? _dpVM;
    private DateTime _lastPreviewRefresh = DateTime.MinValue;
    private string? _internalOsuPath;


    private byte _currentAlpha = 102;
    private readonly byte[] _alphaCycle = [0x22, 0x33, 0x44, 0x55, 0x66, 0x88, 0xAA, 0xCC, 0xEE];
    private int _alphaIndex;
    private Slider? _alphaSlider;

    private void DebouncedRefresh(DualPreviewControl control, int ms = 150)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastPreviewRefresh).TotalMilliseconds < ms) return;
        _lastPreviewRefresh = now;
        control.Refresh();
    }

    private N2NCControl? _convWindowInstance;
    private LNTransformerControl? _lnWindowInstance;
    private DPToolControl? _dpWindowInstance;

    private ContentControl? ConverterSettingsHost =>
        _settingsHosts.GetValueOrDefault(OptionsConstants.ConverterToolName);

    private ContentControl? LNSettingsHost => _settingsHosts.GetValueOrDefault(OptionsConstants.LNToolName);
    private ContentControl? DPSettingsHost => _settingsHosts.GetValueOrDefault(OptionsConstants.DPToolName);
    private ContentControl? LVSettingsHost => _settingsHosts.GetValueOrDefault(OptionsConstants.LVToolName);
    private ContentControl? GetFilesHost => _settingsHosts.GetValueOrDefault(OptionsConstants.GetFilesToolName);

    public DualPreviewControl? ConverterPreview =>
        _previewControls.GetValueOrDefault(OptionsConstants.ConverterToolName);

    public DualPreviewControl? LNPreview => _previewControls.GetValueOrDefault(OptionsConstants.LNToolName);
    public DualPreviewControl? DPPreview => _previewControls.GetValueOrDefault(OptionsConstants.DPToolName);

    public MainWindow()
    {
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
            UseAeroCaptionButtons = false,
            NonClientFrameEdges = NonClientFrameEdges.None
        };
        WindowChrome.SetWindowChrome(this, windowChrome);

        BuildUI();
        ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Acrylic, updateAccent: true);
        SharedUIComponents.SetPanelBackgroundAlpha(102);

        PreviewKeyDown += MainWindow_PreviewKeyDown;
        LoadToolSettingsHosts();
        SetupPreviewProcessors();
        if (_previewControls.TryGetValue(OptionsConstants.ConverterToolName, out var cp))
            cp.StartConversionRequested += ConverterPreview_StartConversionRequested;
        if (_previewControls.TryGetValue(OptionsConstants.LNToolName, out var lp))
            lp.StartConversionRequested += LNPreview_StartConversionRequested;
        if (_previewControls.TryGetValue(OptionsConstants.DPToolName, out var dp))
            dp.StartConversionRequested += DPPreview_StartConversionRequested;
    }

    private void BuildUI()
    {
        // 根 Grid
        var root = new Grid();
        var row0 = new RowDefinition { Height = GridLength.Auto }; // 标题栏行
        var row1 = new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }; // 主内容区域
        var row2 = new RowDefinition { Height = GridLength.Auto }; // Footer行
        root.RowDefinitions.Add(row0);
        root.RowDefinitions.Add(row1);
        root.RowDefinitions.Add(row2);

        // 创建自定义标题栏
        CreateTitleBar();
        Grid.SetRow(_titleBar, 0);
        root.Children.Add(_titleBar);

        // TabControl
        MainTabControl = new TabControl
        {
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        MainTabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
        MainTabControl.PreviewMouseMove += TabControl_PreviewMouseMove;

        // 尝试从全局主题应用现代 TabItem 样式（浏览器标签页风格），若不可用则忽略
        var appRes = Application.Current?.Resources;
        var modernKey = nameof(ResourceKeys.ModernTabItemStyle);
        if (appRes != null && appRes.Contains(modernKey) && appRes[modernKey] is Style tabStyle)
            MainTabControl.ItemContainerStyle = tabStyle;

        if (appRes != null) SharedUIComponents.ApplyDefaultControlStyles(appRes);

        BuildPreviewTabs();
        BuildSimpleTabs();

        Grid.SetRow(MainTabControl, 1);
        root.Children.Add(MainTabControl);

        // Global OSU Listener button (右上)
        GlobalOsuListenerButton = SharedUIComponents.CreateStandardButton(Strings.OSUListenerButton);
        GlobalOsuListenerButton.HorizontalAlignment = HorizontalAlignment.Right;
        GlobalOsuListenerButton.VerticalAlignment = VerticalAlignment.Top;
        GlobalOsuListenerButton.Margin = new Thickness(0, 8, 12, 0);
        GlobalOsuListenerButton.Width = 110;
        GlobalOsuListenerButton.Click += GlobalOsuListenerButton_Click;
        // 放在同一格(1)，通过 Canvas.ZIndex/对齐实现覆盖
        Grid.SetRow(GlobalOsuListenerButton, 1);
        root.Children.Add(GlobalOsuListenerButton);

        // Footer
        BuildFooter(root);

        Content = root;

        AllowDrop = true;
        Drop += (_, e) => GlobalDropArea_Drop(e);
    }

    private void CreateTitleBar()
    {
        // 创建标题栏容器
        _titleBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            Height = 32,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            Margin = new Thickness(0, 0, 0, 0)
        };
        // 确保标题栏在左侧和右侧留出系统按钮的空间
        _titleBar.SetValue(WindowChrome.IsHitTestVisibleInChromeProperty, true);

        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 标题文本
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 按钮区域

        // 标题文本
        _titleTextBlock = new TextBlock
        {
            Text = Title,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0),
            FontWeight = FontWeights.Medium
        };
        Grid.SetColumn(_titleTextBlock, 0);
        titleGrid.Children.Add(_titleTextBlock);

        // 按钮容器
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(buttonPanel, 1);

        // 最小化按钮
        _minimizeButton = new Button
        {
            Content = "—",
            Width = 46,
            Height = 32,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.Black,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        // 确保按钮在标题栏区域内可点击
        _minimizeButton.SetValue(WindowChrome.IsHitTestVisibleInChromeProperty, true);
        _minimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        buttonPanel.Children.Add(_minimizeButton);

        // 最大化/还原按钮
        _maximizeButton = new Button
        {
            Content = "□",
            Width = 46,
            Height = 32,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.Black,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        // 确保按钮在标题栏区域内可点击
        _maximizeButton.SetValue(WindowChrome.IsHitTestVisibleInChromeProperty, true);
        _maximizeButton.Click += (_, _) => 
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            _maximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        };
        buttonPanel.Children.Add(_maximizeButton);

        // 关闭按钮
        _closeButton = new Button
        {
            Content = "×",
            Width = 46,
            Height = 32,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.Black,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        // 确保按钮在标题栏区域内可点击
        _closeButton.SetValue(WindowChrome.IsHitTestVisibleInChromeProperty, true);
        
        // 为关闭按钮添加悬停效果
        _closeButton.MouseEnter += (s, e) => _closeButton.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35));
        _closeButton.MouseLeave += (s, e) => _closeButton.Background = Brushes.Transparent;
        _closeButton.Foreground = new SolidColorBrush(Colors.Black);
        
        // 为最小化和最大化按钮添加悬停效果
        _minimizeButton.MouseEnter += (s, e) => _minimizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        _minimizeButton.MouseLeave += (s, e) => _minimizeButton.Background = Brushes.Transparent;
        
        _maximizeButton.MouseEnter += (s, e) => _maximizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        _maximizeButton.MouseLeave += (s, e) => _maximizeButton.Background = Brushes.Transparent;
        _closeButton.Click += (s, e) => Close();
        buttonPanel.Children.Add(_closeButton);
        
        // 添加按钮的按下效果
        _minimizeButton.PreviewMouseDown += (s, e) => _minimizeButton.Background = new SolidColorBrush(Color.FromRgb(180, 180, 180));
        _minimizeButton.PreviewMouseUp += (s, e) => _minimizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        
        _maximizeButton.PreviewMouseDown += (s, e) => _maximizeButton.Background = new SolidColorBrush(Color.FromRgb(180, 180, 180));
        _maximizeButton.PreviewMouseUp += (s, e) => _maximizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220));
        
        _closeButton.PreviewMouseDown += (s, e) => _closeButton.Background = new SolidColorBrush(Color.FromRgb(200, 15, 30));
        _closeButton.PreviewMouseUp += (s, e) => _closeButton.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35));

        titleGrid.Children.Add(buttonPanel);

        // 添加鼠标事件以支持拖拽窗口
        titleGrid.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // 双击标题栏切换最大化状态
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    _maximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
                }
                else
                {
                    // 拖拽窗口
                    try
                    {
                        DragMove();
                    }
                    catch (InvalidOperationException)
                    {
                        // 忽略拖拽异常
                    }
                }
            }
        };
        
        // 为标题文本也添加拖拽支持
        _titleTextBlock.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // 忽略拖拽异常
                }
            }
        };

        // Ensure the entire title bar supports dragging
        _titleBar.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // Ignore drag exceptions
                }
            }
        };

        _titleBar.Child = titleGrid;
        
        // 当窗口状态改变时更新按钮内容
        StateChanged += (s, e) =>
        {
            _maximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        };
        
        // 初始化按钮内容
        _maximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private void BuildFooter(Grid root)
    {
        var footer = new Border
        {
            Background = SharedUIComponents.PanelBackgroundBrush,
            BorderBrush = SharedUIComponents.PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Height = double.NaN, // 动态高度
            MinHeight = 24,
            Padding = new Thickness(0, 4, 0, 4),
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = SharedUIComponents.PanelCornerRadius
        };

        var footerGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 左侧：版权信息
        var copyrightText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        
        void UpdateCopyrightText()
        {
            copyrightText.Text = SharedUIComponents.IsChineseLanguage() ? Strings.FooterCopyrightCN : Strings.FooterCopyright;
        }
        
        UpdateCopyrightText(); // 初始化文本
        SharedUIComponents.LanguageChanged += UpdateCopyrightText;
        copyrightText.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateCopyrightText;
        
        Grid.SetColumn(copyrightText, 0);
        footerGrid.Children.Add(copyrightText);

        // 中间：GitHub链接
        var githubLink = new Hyperlink(new Run(Strings.GitHubLinkText))
        {
            NavigateUri = new Uri(Strings.GitHubLinkUrl)
        };
        githubLink.RequestNavigate += Hyperlink_RequestNavigate;
        var githubTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 150, 0)
        };
        githubTextBlock.Inlines.Add(githubLink);
        Grid.SetColumn(githubTextBlock, 1);
        footerGrid.Children.Add(githubTextBlock);

        var themeComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(ApplicationTheme)),
            SelectedItem = ApplicationTheme.Light,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var backdropComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(WindowBackdropType)),
            SelectedItem = WindowBackdropType.Mica,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var accentSwitch = new ToggleSwitch
        {
            IsChecked = false,
            Content = Strings.Localize(Strings.UpdateAccent),
            Margin = new Thickness(4, 0, 4, 0)
        };
        
        // 统一应用主题和背景效果的方法
        void ApplyThemeSettings(bool updateAccent = false)
        {
            if (themeComboBox.SelectedItem is ApplicationTheme selectedTheme &&
                backdropComboBox.SelectedItem is WindowBackdropType selectedBackdrop)
            {
                ApplicationThemeManager.Apply(selectedTheme, selectedBackdrop, updateAccent);
            }
        }

        themeComboBox.SelectionChanged += (_, _) => { ApplyThemeSettings(); };
        backdropComboBox.SelectionChanged += (_, _) => { ApplyThemeSettings(); };
        accentSwitch.Checked += (_, _) => { ApplyThemeSettings(updateAccent: true); };
        accentSwitch.Unchecked += (_, _) => { ApplyThemeSettings(updateAccent: false); };

        var langSwitch = new ToggleSwitch
        {
            IsChecked = SharedUIComponents.IsChineseLanguage(),
            Margin = new Thickness(4, 0, 12, 0)
        };
        
        void UpdateLanguageSwitchText()
        {
            langSwitch.Content = SharedUIComponents.IsChineseLanguage() ? "EN" : "中文";
        }
        
        UpdateLanguageSwitchText();
        SharedUIComponents.LanguageChanged += UpdateLanguageSwitchText;
        langSwitch.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateLanguageSwitchText;
        
        langSwitch.Checked += (_, _) => SharedUIComponents.ToggleLanguage();
        langSwitch.Unchecked += (_, _) => SharedUIComponents.ToggleLanguage();

        var settingsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        settingsPanel.Children.Add(themeComboBox);
        settingsPanel.Children.Add(backdropComboBox);
        settingsPanel.Children.Add(accentSwitch);
        settingsPanel.Children.Add(langSwitch);

        Grid.SetColumn(settingsPanel, 2);
        footerGrid.Children.Add(settingsPanel);

        footer.Child = footerGrid;
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
    }

    private void BuildPreviewTabs()
    {
        var previewConfigs = new[]
        {
            new { ToolKey = OptionsConstants.ConverterToolName },
            new { ToolKey = OptionsConstants.LNToolName },
            new { ToolKey = OptionsConstants.DPToolName }
        };
        foreach (var cfg in previewConfigs)
        {
            // Create localized header label
            var headerText = cfg.ToolKey switch
            {
                "Converter" => Strings.TabConverter,
                "LNTransformer" => Strings.TabLNTransformer,
                "DPTool" => Strings.TabDPTool,
                _ => cfg.ToolKey
            };
            var headerLabel = SharedUIComponents.CreateHeaderLabel(headerText);
            var tab = new TabItem
            {
                Header = headerLabel,
                Tag = cfg.ToolKey,
                MinWidth = 50,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var settingsHost = new ContentControl();
            _settingsHosts[cfg.ToolKey] = settingsHost;
            
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            settingsHost.Content = null;
            scroll.Content = settingsHost;
            
            var settingsBorder = SharedUIComponents.CreateStandardPanel(scroll, new Thickness(8));
            Grid.SetColumn(settingsBorder, 0);
            grid.Children.Add(settingsBorder);
            
            var preview = new DualPreviewControl { Margin = new Thickness(8), MinWidth = 200 };
            _previewControls[cfg.ToolKey] = preview;
            Grid.SetColumn(preview, 1);
            grid.Children.Add(preview);
            tab.Content = grid;
            Debug.Assert(MainTabControl != null, nameof(MainTabControl) + " != null");
            MainTabControl.Items.Add(tab);
        }
    }

    // Create simple tool tabs with only settings host
    private void BuildSimpleTabs()
    {
        var simpleConfigs = new[]
        {
            new { ToolKey = OptionsConstants.LVToolName },
            new { ToolKey = OptionsConstants.GetFilesToolName }
        };
        foreach (var cfg in simpleConfigs)
        {
            var headerText = cfg.ToolKey == Strings.TabLV ? Strings.TabLV : Strings.TabGetFiles;
            var headerLabel = SharedUIComponents.CreateHeaderLabel(headerText);
            var tab = new TabItem
            {
                Header = headerLabel,
                Tag = cfg.ToolKey,
                MinWidth = 50,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var settingsHost = new ContentControl();
            _settingsHosts[cfg.ToolKey] = settingsHost;
            // Placeholder for simple tool settings; actual content is loaded later in LoadToolSettingsHosts
            settingsHost.Content = null;
            var grid = new Grid { Margin = new Thickness(8) };
            var settingsBorder = SharedUIComponents.CreateStandardPanel(settingsHost, padding: new Thickness(0));
            grid.Children.Add(settingsBorder);
            tab.Content = grid;
            MainTabControl.Items.Add(tab);
        }
    }

    private void SetupPreviewProcessors()
    {
        _internalOsuPath = ResolveInternalSample();
        // Providers read current host DataContext / controls at invocation time, making ordering safe
        if (_previewControls.TryGetValue(OptionsConstants.ConverterToolName, out var converterPreview) &&
            _settingsHosts.TryGetValue(OptionsConstants.ConverterToolName, out var convHost))
            converterPreview.Processor = new ConverterPreviewProcessor(null,
                () => (convHost.DataContext as N2NCViewModel)?.GetConversionOptions());

        if (_previewControls.TryGetValue(OptionsConstants.LNToolName, out var lnPreview))
            lnPreview.Processor = new LNPreviewProcessor(null,
                () => new PreviewTransformation.LNPreviewParameters
                {
                    LevelValue = GetSliderValue("LevelValue"),
                    PercentageValue = GetSliderValue("PercentageValue"),
                    DivideValue = GetSliderValue("DivideValue"),
                    ColumnValue = GetSliderValue("ColumnValue"),
                    GapValue = GetSliderValue("GapValue"),
                    OriginalLN = GetCheckBoxValue("OriginalLN"),
                    FixError = GetCheckBoxValue("FixError"),
                    OverallDifficulty = GetTextBoxDouble("OverallDifficulty")
                });

        if (_previewControls.TryGetValue(OptionsConstants.DPToolName, out var dpPreview) &&
            _settingsHosts.TryGetValue(OptionsConstants.DPToolName, out var dpHost))
            dpPreview.Processor = new DPPreviewProcessor(null,
                () => (dpHost.DataContext as DPToolViewModel)?.Options ?? new DPToolOptions());

        _converterVM = ConverterSettingsHost?.DataContext as N2NCViewModel;
        if (_converterVM != null && ConverterPreview?.Processor is ConverterPreviewProcessor cpp)
        {
            cpp.ConverterOptionsProvider = () => _converterVM.GetConversionOptions();
            _converterVM.PropertyChanged += (_, _) => DebouncedRefresh(ConverterPreview!);
        }

        _dpVM = DPSettingsHost?.DataContext as DPToolViewModel;
        if (_dpVM != null && DPPreview?.Processor is DPPreviewProcessor dpp)
        {
            dpp.DPOptionsProvider = () => _dpVM.Options;
            _dpVM.PropertyChanged += (_, _) => DebouncedRefresh(DPPreview!);
            _dpVM.Options.PropertyChanged += (_, _) => DebouncedRefresh(DPPreview!);
        }

        WireLNControlEvents();

        // Preload internal sample into all preview controls
        if (!string.IsNullOrEmpty(_internalOsuPath) && File.Exists(_internalOsuPath))
        {
            var arr = new[] { _internalOsuPath };
            if (_previewControls.TryGetValue(OptionsConstants.ConverterToolName, out var convControl))
                convControl.LoadFiles(arr, true);
            if (_previewControls.TryGetValue(OptionsConstants.LNToolName, out var lnControl))
                lnControl.LoadFiles(arr, true);
            if (_previewControls.TryGetValue(OptionsConstants.DPToolName, out var dpControl))
                dpControl.LoadFiles(arr, true);
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
        if (conv.Content is UIElement convContent && ConverterSettingsHost != null)
        {
            // 复制资源，以便 StaticResource/Style 查找仍然有效
            var keys = conv.Resources.Keys.Cast<object>().ToList();
            foreach (var k in keys)
            {
                // Skip implicit Window styles (they would affect the host/main window)
                var res = conv.Resources[k];
                if (res is Style s && s.TargetType == typeof(Window))
                    continue;
                if (!ConverterSettingsHost.Resources.Contains(k))
                    ConverterSettingsHost.Resources.Add(k, conv.Resources[k]);
            }

            // 清除嵌入内容中的固定尺寸以便自适应
            ClearFixedSizes(convContent);

            // 将实际内容移入宿主，保留绑定和事件处理器
            ConverterSettingsHost.DataContext = conv.DataContext;
            ConverterSettingsHost.Content = convContent;
            // 清空原窗口内容，使元素只有一个父级
            conv.Content = null;
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
        try
        {
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
            else if (DPSettingsHost != null)
            {
                // fallback placeholder
                DPSettingsHost.DataContext = _dpWindowInstance?.DataContext;
                var placeholder = new TextBlock
                {
                    Text =
                        "DP settings failed to load here — showing fallback.\nIf this persists, try reopening the DP tool.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                DPSettingsHost.Content = placeholder;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load DP tool settings: {ex.Message}");
            if (DPSettingsHost != null)
            {
                DPSettingsHost.DataContext = _dpWindowInstance?.DataContext;
                var placeholder = new TextBlock
                {
                    Text =
                        "DP settings failed to load here — showing fallback.\nIf this persists, try reopening the DP tool.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                DPSettingsHost.Content = placeholder;
            }
        }

        // LV Calculator 嵌入
        try
        {
            var lvWin = new KRRLVControl();
            if (lvWin.Content is UIElement lvContent && LVSettingsHost != null)
            {
                var keys = lvWin.Resources.Keys.Cast<object>().ToList();
                foreach (var k in keys)
                {
                    var res = lvWin.Resources[k];
                    if (res is Style s && s.TargetType == typeof(Window))
                        continue;
                    if (!LVSettingsHost.Resources.Contains(k))
                        LVSettingsHost.Resources.Add(k, lvWin.Resources[k]);
                }

                ClearFixedSizes(lvContent);

                LVSettingsHost.DataContext = lvWin.DataContext;
                LVSettingsHost.Content = lvContent;
                lvWin.Content = null;
            }
            else if (LVSettingsHost != null)
            {
                LVSettingsHost.DataContext = lvWin.DataContext;
                var placeholder = new TextBlock
                {
                    Text = "LV Calculator failed to load here — showing fallback.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                LVSettingsHost.Content = placeholder;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load LV Calculator settings: {ex.Message}");
        }

        // osu! file manager 嵌入
        try
        {
            var getFilesWin = new GetFilesControl();
            if (getFilesWin.Content is UIElement gfContent && GetFilesHost != null)
            {
                var keys = getFilesWin.Resources.Keys.Cast<object>().ToList();
                foreach (var k in keys)
                {
                    var res = getFilesWin.Resources[k];
                    if (res is Style s && s.TargetType == typeof(Window))
                        continue;
                    if (!GetFilesHost.Resources.Contains(k))
                        GetFilesHost.Resources.Add(k, getFilesWin.Resources[k]);
                }

                ClearFixedSizes(gfContent);

                GetFilesHost.DataContext = getFilesWin.DataContext;
                GetFilesHost.Content = gfContent;
                getFilesWin.Content = null;
            }
            else if (GetFilesHost != null)
            {
                GetFilesHost.DataContext = getFilesWin.DataContext;
                var placeholder = new TextBlock
                {
                    Text =
                        "osu! file manager failed to load here — showing fallback.\nTry opening it in a separate window if the issue persists.",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                GetFilesHost.Content = placeholder;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load GetFiles settings: {ex.Message}");
        }
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
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

        // Determine tool key from tab Tag
        if ((MainTabControl.SelectedItem as TabItem)?.Tag is string toolKey && _previewControls.TryGetValue(toolKey, out var control))
        {
            control.LoadFiles(allOsu.ToArray());
            try
            {
                control.StageFiles(allOsu.ToArray());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"StageFiles error ({toolKey}): {ex.Message}");
            }
        }
        else
        {
            var getFilesControl = new GetFilesControl();
            var getFilesWindow = new Window
            {
                Title = Strings.TabGetFiles,
                Content = getFilesControl,
                Width = 800,
                Height = 600
            };
            getFilesWindow.Show();
        }
    }

    // 选项卡拖动/分离处理 - 改进：克隆内容用于分离窗口，保持原选项卡不变
    private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        // 查找鼠标下的 TabItem
        var source = e.OriginalSource as DependencyObject;
        while (source != null && !(source is TabItem)) source = VisualTreeHelper.GetParent(source);

        _draggedTab = source as TabItem;
    }

    private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedTab == null) return;
        var pos = e.GetPosition(this);
        var dx = Math.Abs(pos.X - _dragStartPoint.X);
        var dy = Math.Abs(pos.Y - _dragStartPoint.Y);
        const double dragThreshold = 10.0;
        if (dx > dragThreshold || dy > dragThreshold)
        {
            DetachTab(_draggedTab);
            _draggedTab = null;
        }
    }

    private void DetachTab(TabItem? tab)
    {
        if (tab == null) return;
        // Prevent crash if tab is no longer in the control
        if (!MainTabControl.Items.Contains(tab)) return;

        var header = tab.Header?.ToString() ?? "Detached";
        var isPreviewTool = header == "Converter" || header == "LN Transformer" || header == "DP tool";

        Func<ContentControl>? createFreshWindow = header switch
        {
            "Converter" => () => new N2NCControl(),
            "LN Transformer" => () => new LNTransformerControl(),
            "DP tool" => () => new DPToolControl(),
            "LV Calculator" => () => new KRRLVControl(),
            "osu! file manager" => () => new GetFilesControl(),
            _ => null
        };

        Window win;
        ContentControl? host = null;
        UIElement? settingsContent = null;
        ResourceDictionary? settingsResources = null;
        if (isPreviewTool && createFreshWindow != null)
        {
            try
            {
                // Try to reuse the existing settings host content from the main window so the detached preview
                // uses the same settings instance (DataContext) instead of creating a fresh copy.
                host = header switch
                {
                    "Converter" => _settingsHosts.GetValueOrDefault("Converter"),
                    "LN Transformer" => _settingsHosts.GetValueOrDefault("LNTransformer"),
                    "DP tool" => _settingsHosts.GetValueOrDefault("DPTool"),
                    _ => null
                };

                if (host != null)
                {
                    settingsContent = host.Content as UIElement;
                    if (settingsContent != null)
                    {
                        // Detach the content from the host and preserve its DataContext for the detached window
                        host.Content = null;
                        ClearFixedSizes(settingsContent);
                        if (settingsContent is FrameworkElement fe)
                            fe.DataContext = host.DataContext; // preserve the VM on the content itself

                        settingsResources = host.Resources;
                    }
                }

                // Fallback: if host had no content (shouldn't normally happen), create fresh window as before
                if (settingsContent == null)
                {
                    var fresh = createFreshWindow();
                    settingsContent = fresh.Content as UIElement;
                    if (settingsContent != null)
                    {
                        ClearFixedSizes(settingsContent);
                        fresh.Content = null;
                    }

                    settingsResources = fresh.Resources;
                }

                IPreviewProcessor proc = header switch
                {
                    "Converter" => new ConverterPreviewProcessor(),
                    "LN Transformer" => new LNPreviewProcessor(),
                    _ => new DPPreviewProcessor()
                };

                var det = new DetachedToolWindow(header, settingsContent, settingsResources, proc);

                // When the detached window requests merge, restore the content back to its original host and close
                det.MergeRequested += (_, _) =>
                {
                    try
                    {
                        if (host != null && settingsContent != null && host.Content == null)
                            host.Content = settingsContent;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to restore host content on merge: {ex.Message}");
                    }

                    det.Close();
                };

                win = det;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed detached preview window: {ex.Message}");
                win = new Window
                {
                    Title = header + " (Detached)", Width = 900, Height = 650,
                    Content = new TextBlock { Text = header }
                };
            }
        }
        else
        {
            if (createFreshWindow != null)
                try
                {
                    var control = createFreshWindow();
                    win = new Window
                    {
                        Title = header,
                        Content = control,
                        Width = 800,
                        Height = 600
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to create fresh window: {ex.Message}");
                    win = new Window
                        { Title = header, Width = 800, Height = 600, Content = new TextBlock { Text = header } };
                }
            else
                win = new Window
                    { Title = header, Width = 800, Height = 600, Content = new TextBlock { Text = header } };

            try
            {
                var existingContent = win.Content as UIElement;
                var dock = new DockPanel();
                var mergeBtn = new Button
                {
                    Content = "Merge back", Margin = new Thickness(6), Padding = new Thickness(8),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                mergeBtn.Click += (_, _) => win.Close();
                DockPanel.SetDock(mergeBtn, Dock.Top);
                dock.Children.Add(mergeBtn);
                if (existingContent != null)
                {
                    win.Content = null;
                    ClearFixedSizes(existingContent);
                    dock.Children.Add(existingContent);
                }

                win.Content = dock;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to inject merge UI: {ex.Message}");
            }
        }

        try
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            win.Left = cursor.X - 40;
            win.Top = cursor.Y - 10;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to position detached window: {ex.Message}");
        }

        var insertIndex = MainTabControl.Items.IndexOf(tab);
        MainTabControl.Items.Remove(tab);
        var followTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        followTimer.Tick += (_, _) =>
        {
            try
            {
                var p = System.Windows.Forms.Cursor.Position;
                win.Left = p.X - 40;
                win.Top = p.Y - 10;
                if ((System.Windows.Forms.Control.MouseButtons & System.Windows.Forms.MouseButtons.Left) !=
                    System.Windows.Forms.MouseButtons.Left)
                {
                    try
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
                    }
                    catch (Exception ex2)
                    {
                        Debug.WriteLine($"Merge check error: {ex2.Message}");
                    }

                    followTimer.Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Follow timer tick error: {ex.Message}");
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
            try
            {
                if (followTimer.IsEnabled) followTimer.Stop();

                // If this was a preview tool detachment, try to restore the original settings content
                // back into its host so the main window doesn't end up with an empty settings area.
                try
                {
                    if (host != null && settingsContent != null && host.Content == null)
                    {
                        // Unparent the settingsContent from any current parent in the detached window
                        UnparentElement(settingsContent);
                        host.Content = settingsContent;
                    }
                }
                catch (Exception exInner)
                {
                    Debug.WriteLine($"Error while restoring detached settings content: {exInner.Message}");
                }

                if (!MainTabControl.Items.Contains(tab))
                {
                    if (insertIndex >= 0 && insertIndex <= MainTabControl.Items.Count)
                        MainTabControl.Items.Insert(insertIndex, tab);
                    else MainTabControl.Items.Add(tab);
                }

                MainTabControl.SelectedItem = tab;
            }
            catch (Exception ex3)
            {
                Debug.WriteLine($"Error reinserting original tab: {ex3.Message}");
            }
        };

        win.Show();
    }

    private string? ResolveInternalSample()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (string.IsNullOrEmpty(baseDir)) return null;
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
            Debug.WriteLine("ResolveInternalSample error: " + ex.Message);
        }

        return null;
    }

    private void WireLNControlEvents()
    {
        if (LNPreview?.Processor is not LNPreviewProcessor lnp) return;
        lnp.LNParamsProvider = () => new PreviewTransformation.LNPreviewParameters
        {
            LevelValue = GetSliderValue("LevelValue"),
            PercentageValue = GetSliderValue("PercentageValue"),
            DivideValue = GetSliderValue("DivideValue"),
            ColumnValue = GetSliderValue("ColumnValue"),
            GapValue = GetSliderValue("GapValue"),
            OriginalLN = GetCheckBoxValue("OriginalLN"),
            FixError = GetCheckBoxValue("FixError"),
            OverallDifficulty = GetTextBoxDouble("OverallDifficulty")
        };

        // Update labels initially and whenever control values change
        void updateAndRefresh()
        {
            UpdateLNLabels();
            if (LNPreview != null)
                DebouncedRefresh(LNPreview);
        }

        UpdateLNLabels();

        // Attempt to attach handlers immediately; if controls aren't yet available (e.g. not loaded
        // or visual tree not realized after reparenting), defer wiring until Loaded/Dispatcher idle.
        bool AttachHandlersOnce()
        {
            try
            {
                // Attach handlers; note Attach* methods are no-ops if control not found
                AttachSliderHandler("LevelValue", updateAndRefresh);
                AttachSliderHandler("PercentageValue", updateAndRefresh);
                AttachSliderHandler("DivideValue", updateAndRefresh);
                AttachSliderHandler("ColumnValue", updateAndRefresh);
                AttachSliderHandler("GapValue", updateAndRefresh);
                AttachCheckBoxHandler("OriginalLN", updateAndRefresh);
                AttachCheckBoxHandler("FixError", updateAndRefresh);
                AttachTextBoxHandler("OverallDifficulty", updateAndRefresh);

                // Check whether at least one key control was found and wired (LevelValue slider is a good sentinel)
                var sentinel = FindInLNHost<Slider>("LevelValue");
                var found = sentinel != null;
                Debug.WriteLine("AttachHandlersOnce: sentinel found = " + found);
                return found;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AttachHandlersOnce failed: " + ex.Message);
                return false;
            }
        }

        // Try immediately
        if (AttachHandlersOnce())
        {
            UpdateLNLabels();
            if (LNPreview != null) DebouncedRefresh(LNPreview);
            return;
        }

        // If immediate wiring failed, defer until the LN settings content is loaded / layout completed
        try
        {
            if (LNSettingsHost is { Content: FrameworkElement fe })
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    try
                    {
                        // Remove handler
                        fe.Loaded -= loadedHandler;
                    }
                    catch (Exception exRemove)
                    {
                        Debug.WriteLine("Failed removing Loaded handler: " + exRemove.Message);
                    }

                    // Schedule a retry at Render priority to ensure templates/visual tree are ready
                    try
                    {
                        Debug.WriteLine("LN settings Loaded - scheduling deferred attach of handlers.");
                        var disp = fe.Dispatcher;
                        if (disp != null)
                            disp.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    var ok = AttachHandlersOnce();
                                    Debug.WriteLine("AttachHandlersOnce after Loaded returned: " + ok);
                                    if (ok)
                                    {
                                        UpdateLNLabels();
                                        if (LNPreview != null) DebouncedRefresh(LNPreview);
                                    }
                                    else
                                    {
                                        // Final fallback: small delayed retry
                                        disp.BeginInvoke(new Action(() =>
                                        {
                                            var ok2 = AttachHandlersOnce();
                                            Debug.WriteLine("Final AttachHandlersOnce retry returned: " + ok2);
                                            if (ok2)
                                            {
                                                UpdateLNLabels();
                                                if (LNPreview != null) DebouncedRefresh(LNPreview);
                                            }
                                        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Deferred AttachHandlersOnce failed: " + ex.Message);
                                }
                            }), System.Windows.Threading.DispatcherPriority.Render);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Dispatcher retry scheduling failed: " + ex.Message);
                    }
                };

                // Subscribe to Loaded to attempt wiring when element becomes part of the visual tree
                fe.Loaded += loadedHandler;
            }
            else
            {
                // As a last resort, schedule a dispatcher retry even if we don't have a FrameworkElement
                try
                {
                    Dispatcher.BeginInvoke(new Action(() => AttachHandlersOnce()),
                        System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Debug.WriteLine("Scheduled AttachHandlersOnce on main dispatcher as fallback.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed scheduling fallback dispatcher attach: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("WireLNControlEvents deferred wiring failed: " + ex.Message);
        }
    }

    // 更新 LN embedded 设置页上方的标签（因为 ElementName 绑定在被重parent 后失效）
    private void UpdateLNLabels()
    {
        try
        {
            // Level
            if (FindInLNHost<TextBlock>("LevelLabel") is { } level)
                level.Text = $"Level {GetSliderValue("LevelValue"):N0}";

            if (FindInLNHost<TextBlock>("PercentageLabel") is { } perc)
                perc.Text = $"LN {GetSliderValue("PercentageValue"):N0}%";

            if (FindInLNHost<TextBlock>("DivideLabel") is { } div)
                div.Text = $"Divide 1/{GetSliderValue("DivideValue"):N0}";

            if (FindInLNHost<TextBlock>("ColumnLabel") is { } col)
                col.Text = $"Column {GetSliderValue("ColumnValue"):N0}";

            if (FindInLNHost<TextBlock>("GapLabel") is { } gap)
                gap.Text = $"Gap {GetSliderValue("GapValue"):N0}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine("UpdateLNLabels failed: " + ex.Message);
        }
    }

    private double GetSliderValue(string name)
    {
        if (FindInLNHost<Slider>(name) is { } s) return s.Value;
        return 0;
    }

    private bool GetCheckBoxValue(string name)
    {
        if (FindInLNHost<CheckBox>(name) is { IsChecked: true }) return true;
        return false;
    }

    private double GetTextBoxDouble(string name)
    {
        if (FindInLNHost<TextBox>(name) is { } t &&
            double.TryParse(t.Text, out var v)) return v;
        return 0;
    }

    private void AttachSliderHandler(string name, Action? act)
    {
        if (string.IsNullOrEmpty(name) || act == null) return;
        var s = FindInLNHost<Slider>(name);
        if (s != null) s.ValueChanged += (_, _) => act();
    }

    private void AttachCheckBoxHandler(string name, Action act)
    {
        if (string.IsNullOrEmpty(name)) return;
        var c = FindInLNHost<CheckBox>(name);
        if (c != null)
        {
            c.Checked += (_, _) => act();
            c.Unchecked += (_, _) => act();
        }
    }

    private void AttachTextBoxHandler(string name, Action act)
    {
        if (string.IsNullOrEmpty(name)) return;
        var t = FindInLNHost<TextBox>(name);
        if (t != null) t.TextChanged += (_, _) => act();
    }

    private T? FindInLNHost<T>(string name) where T : FrameworkElement
    {
        if (LNSettingsHost?.Content is FrameworkElement root) return FindDescendant<T>(root, name);

        return null;
    }

    // Enhanced descendant search: try visual children first, fall back to logical children when needed
    private T? FindDescendant<T>(FrameworkElement root, string name) where T : FrameworkElement
    {
        if (root.Name == name && root is T tt) return tt;
        // Visual tree
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            if (VisualTreeHelper.GetChild(root, i) is FrameworkElement child)
            {
                var found = FindDescendant<T>(child, name);
                if (found != null) return found;
            }

        // Logical tree
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<FrameworkElement>())
        {
            if (child.Name == name && child is T ttt) return ttt;
            var found = FindDescendant<T>(child, name);
            if (found != null) return found;
        }

        return null;
    }

    // Helper to remove an element from its logical/visual parent so it can be reparented elsewhere.
    private void UnparentElement(UIElement element)
    {
        try
        {
            var logicalParent = LogicalTreeHelper.GetParent(element);
            if (logicalParent is ContentControl cc && Equals(cc.Content, element))
            {
                cc.Content = null;
                return;
            }

            if (logicalParent is ScrollViewer sv && Equals(sv.Content, element))
            {
                sv.Content = null;
                return;
            }

            if (logicalParent is Border b && Equals(b.Child, element))
            {
                b.Child = null;
                return;
            }

            if (logicalParent is Panel p && p.Children.Contains(element))
            {
                p.Children.Remove(element);
                return;
            }

            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is ContentControl vcc && Equals(vcc.Content, element))
                vcc.Content = null;
            else if (visualParent is ScrollViewer vsv && Equals(vsv.Content, element))
                vsv.Content = null;
            else if (visualParent is Border vb && vb.Child == element)
                vb.Child = null;
            else if (visualParent is Panel vp && vp.Children.Contains(element)) vp.Children.Remove(element);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UnparentElement failed: {ex.Message}");
        }
    }

    private void ConverterPreview_StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;
        if (_convWindowInstance == null) return;

        // Filter out internal sample (preview default) so it is never converted
        var toProcess = paths.Where(p => !string.Equals(p, _internalOsuPath, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (toProcess.Length == 0) return;

        var created = new List<string>();
        var failed = new List<string>();

        // Process each staged .osu file using the converter's public single-file API
        foreach (var p in toProcess)
            try
            {
                var result = _convWindowInstance.ProcessSingleFile(p);
                if (!string.IsNullOrEmpty(result))
                    created.Add(result);
                else
                    failed.Add(p);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Converter processing error for {p}: {ex.Message}");
                failed.Add(p);
            }

        // If any succeeded, clear staged UI and show success; otherwise keep staged UI so user can retry
        if (created.Count > 0)
        {
            try
            {
                // Clear staged state across previews
                DualPreviewControl.BroadcastStagedPaths(null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BroadcastStagedPaths error: {ex.Message}");
            }

            // Show summary with the created files (or directories)
            try
            {
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show conversion result message: {ex.Message}");
            }
        }
        else
        {
            // Nothing created: report failure and keep staged UI so user can retry
            try
            {
                var msg = failed.Count > 0
                    ? "Conversion failed for the selected files. The staged files remain so you can retry."
                    : "Conversion did not produce any output.";
                MessageBox.Show(msg, "Conversion Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show conversion failure message: {ex.Message}");
            }
        }
    }

    private void LNPreview_StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;
        if (_lnWindowInstance == null) return;

        // Filter out internal sample and invalid files
        var toProcess = paths.Where(p => !string.Equals(p, _internalOsuPath, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (toProcess.Length == 0) return;

        foreach (var p in toProcess)
            try
            {
                _lnWindowInstance.ProcessSingleFile(p);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LN processing error for {p}: {ex.Message}");
            }
    }

    private void DPPreview_StartConversionRequested(object? sender, string[]? paths)
    {
        if (paths == null || paths.Length == 0) return;
        if (_dpWindowInstance == null) return;

        // Filter out internal sample and invalid files
        var toProcess = paths.Where(p => !string.Equals(p, _internalOsuPath, StringComparison.OrdinalIgnoreCase))
            .Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (toProcess.Length == 0) return;

        foreach (var p in toProcess)
            try
            {
                _dpWindowInstance.ProcessSingleFile(p);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DP processing error for {p}: {ex.Message}");
            }
    }

    private void GlobalOsuListenerButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedTab = MainTabControl.SelectedItem as TabItem;

        object? source = null;
        var sourceId = 0;
        switch (selectedTab?.Tag as string)
        {
            case OptionsConstants.ConverterToolName when _convWindowInstance != null:
                source = _convWindowInstance;
                sourceId = 1;
                break;
            case OptionsConstants.LNToolName when _lnWindowInstance != null:
                source = _lnWindowInstance;
                sourceId = 2;
                break;
            case OptionsConstants.DPToolName when _dpWindowInstance != null:
                source = _dpWindowInstance;
                sourceId = 3;
                break;
        }

        var listenerControl = new tools.Listener.ListenerControl(source, sourceId);
        var listenerWindow = new Window
        {
            Title = Strings.ListenerTitlePrefix,
            Content = listenerControl,
            Width = 800,
            Height = 600
        };
        listenerWindow.Show();
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
}