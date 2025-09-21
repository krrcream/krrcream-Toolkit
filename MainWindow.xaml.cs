// E:\Mug\OSU tool\krrtool\krrTools\krrTools\MainWindow.xaml.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using krrTools.Tools.Converter;
using krrTools.tools.Preview;
using krrTools.tools.DPtool;
using krrTools.tools.Get_files;
using krrTools.tools.KRR_LV;
using krrTools.tools.LNTransformer;

namespace krrTools
{
    public partial class MainWindow
    {
        // 跟踪选项卡拖动/分离
        private Point _dragStartPoint;
        private TabItem? _draggedTab;

        private ConverterViewModel? _converterVM; // 保存转换器 VM 引用，用于预览转换
        private DPToolViewModel? _dpVM;
        private DateTime _lastPreviewRefresh = DateTime.MinValue;
        private string? _internalOsuPath;

        private void DebouncedRefresh(DualPreviewControl control, int ms = 150)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastPreviewRefresh).TotalMilliseconds < ms) return;
            _lastPreviewRefresh = now;
            control.Refresh();
        }

        private ConverterWindow? _convWindowInstance;
        private LNTransformer? _lnWindowInstance;
        private DPToolWindow? _dpWindowInstance;

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                LoadToolSettingsHosts();
                SetupPreviewProcessors();
                // Subscribe to preview StartConversionRequested events (raised by DualPreviewControl Start button)
                ConverterPreview.StartConversionRequested += (s, paths) => ConverterPreview_StartConversionRequested(s, paths);
                LNPreview.StartConversionRequested += (s, paths) => LNPreview_StartConversionRequested(s, paths);
                DPPreview.StartConversionRequested += (s, paths) => DPPreview_StartConversionRequested(s, paths);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error during initialization: " + ex.Message);
            }
        }

        private void SetupPreviewProcessors()
        {
            // 解析内部示例 osu 路径
            _internalOsuPath = ResolveInternalSample();
            try
            {
                ConverterPreview.Processor =
                    new ConverterPreviewProcessor(columnOverride: null, converterOptionsProvider: null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to assign ConverterPreview.Processor: " + ex.Message);
            }

            try
            {
                LNPreview.Processor = new LNPreviewProcessor(columnOverride: null, lnParamsProvider: null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to assign LNPreview.Processor: " + ex.Message);
            }

            try
            {
                DPPreview.Processor = new DPPreviewProcessor(columnOverride: null, dpOptionsProvider: null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to assign DPPreview.Processor: " + ex.Message);
            }

            // 捕获 Converter 的 ViewModel
            try
            {
                _converterVM = ConverterSettingsHost.DataContext as ConverterViewModel;
                if (_converterVM != null && ConverterPreview.Processor is ConverterPreviewProcessor cpp)
                {
                    cpp.ConverterOptionsProvider = () => _converterVM.GetConversionOptions();
                    _converterVM.PropertyChanged += (_, _) => DebouncedRefresh(ConverterPreview);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to wire ConverterViewModel: " + ex.Message);
            }

            // 捕获 DP 的 ViewModel
            try
            {
                _dpVM = DPSettingsHost.DataContext as DPToolViewModel;
                if (_dpVM != null && DPPreview.Processor is DPPreviewProcessor dpp)
                {
                    dpp.DPOptionsProvider = () => _dpVM.Options;
                    _dpVM.PropertyChanged += (_, _) => DebouncedRefresh(DPPreview);
                    _dpVM.Options.PropertyChanged += (_, _) => DebouncedRefresh(DPPreview);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to wire DPToolViewModel: " + ex.Message);
            }

            // 直接为 LN 控件绑定事件（它们没有单独的 ViewModel）
            try
            {
                WireLNControlEvents();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to wire LN control events: " + ex.Message);
            }

            // 初始加载（不需要拖动）
            try
            {
                if (!string.IsNullOrEmpty(_internalOsuPath) && File.Exists(_internalOsuPath))
                {
                    var arr = new[] { _internalOsuPath };
                    ConverterPreview.LoadFiles(arr, suppressBroadcast: true);
                    LNPreview.LoadFiles(arr, suppressBroadcast: true);
                    DPPreview.LoadFiles(arr, suppressBroadcast: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Initial preview load failed: " + ex.Message);
            }
        }

        private void ClearFixedSizes(DependencyObject? element)
        {
            if (element is FrameworkElement fe)
            {
                // 取消固定尺寸，让控件可以伸缩
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
            try
            {
                if (element != null)
                {
                    var children = LogicalTreeHelper.GetChildren(element).OfType<object>().ToList();
                    foreach (var child in children)
                        // 仅对 DependencyObject 子项递归
                        if (child is DependencyObject dob)
                            ClearFixedSizes(dob);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ClearFixedSizes logical tree traversal error: " + ex.Message);
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
            try
            {
                _convWindowInstance = new ConverterWindow();
                var conv = _convWindowInstance;
                if (conv.Content is UIElement convContent)
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to load Converter settings: " + ex.Message);
            }

            // LNTransformer 嵌入
            try
            {
                _lnWindowInstance = new LNTransformer();
                var ln = _lnWindowInstance;
                if (ln.Content is UIElement lnContent)
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to load LNTransformer settings: " + ex.Message);
            }

            // DP Tool 嵌入
            try
            {
                _dpWindowInstance = new DPToolWindow();
                var dp = _dpWindowInstance;
                if (dp.Content is UIElement dpContent)
                {
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to load DPTool settings: " + ex.Message);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        // 窗口级别拖放：转发到全局处理器
        private void Window_Drop(object sender, DragEventArgs e)
        {
            GlobalDropArea_Drop(e);
        }

        private void GlobalDropArea_Drop(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            var allOsu = new List<string>();
            foreach (var path in files)
                if (File.Exists(path) && Path.GetExtension(path).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                    allOsu.Add(path);
                else if (Directory.Exists(path))
                    try
                    {
                        allOsu.AddRange(Directory.GetFiles(path, "*.osu", SearchOption.AllDirectories));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Dir enum error: " + ex.Message);
                    }

            if (allOsu.Count == 0)
            {
                MessageBox.Show("No .osu files found in dropped items.");
                return;
            }

            var selectedTab = MainTabControl.SelectedItem as TabItem;
            if (selectedTab == Tab_Converter)
            {
                ConverterPreview.LoadFiles(allOsu.ToArray());
            }
            else if (selectedTab == Tab_LNTransformer)
            {
                LNPreview.LoadFiles(allOsu.ToArray());
            }
            else if (selectedTab == Tab_DPTool)
            {
                DPPreview.LoadFiles(allOsu.ToArray());
            }
            else
            {
                // 回退：显示文件管理器
                var getFilesWindow = new GetFilesWindow();
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
            // Use a smaller threshold so detaching is easier (user reported it was hard to drag out)
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
            var header = tab.Header?.ToString() ?? "Detached";
            var isPreviewTool = header == "Converter" || header == "LN Transformer" || header == "DP tool";

            Func<Window>? createFreshWindow = header switch
            {
                "Converter" => () => new ConverterWindow(),
                "LN Transformer" => () => new LNTransformer(),
                "DP tool" => () => new DPToolWindow(),
                "LV Calculator" => () => new KRRLVWindow(),
                "osu! file manager" => () => new GetFilesWindow(),
                _ => null
            };

            Window win;
            // hoist these so win.Closing can reference them
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
                        "Converter" => ConverterSettingsHost,
                        "LN Transformer" => LNSettingsHost,
                        "DP tool" => DPSettingsHost,
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
                            {
                                host.Content = settingsContent;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Failed to restore host content on merge: " + ex.Message);
                        }

                        det.Close();
                    };

                    win = det;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed detached preview window: " + ex.Message);
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
                        win = createFreshWindow();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Failed to create fresh window: " + ex.Message);
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
                    Debug.WriteLine("Failed to inject merge UI: " + ex.Message);
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
                Debug.WriteLine("Failed to position detached window: " + ex.Message);
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
                            Debug.WriteLine("Merge check error: " + ex2.Message);
                        }

                        followTimer.Stop();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Follow timer tick error: " + ex.Message);
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
                        Debug.WriteLine("Error while restoring detached settings content: " + exInner.Message);
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
                    Debug.WriteLine("Error reinserting original tab: " + ex3.Message);
                }
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
                for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "tools", "Preview",
                        "mania-last-object-not-latest.osu");
                    if (File.Exists(candidate)) return candidate;
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
            if (LNPreview.Processor is not LNPreviewProcessor lnp) return;
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
            // 附加变更处理器
            AttachSliderHandler("LevelValue", () => DebouncedRefresh(LNPreview));
            AttachSliderHandler("PercentageValue", () => DebouncedRefresh(LNPreview));
            AttachSliderHandler("DivideValue", () => DebouncedRefresh(LNPreview));
            AttachSliderHandler("ColumnValue", () => DebouncedRefresh(LNPreview));
            AttachSliderHandler("GapValue", () => DebouncedRefresh(LNPreview));
            AttachCheckBoxHandler("OriginalLN", () => DebouncedRefresh(LNPreview));
            AttachCheckBoxHandler("FixError", () => DebouncedRefresh(LNPreview));
            AttachTextBoxHandler("OverallDifficulty", () => DebouncedRefresh(LNPreview));
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

        private void AttachSliderHandler(string name, Action act)
        {
            if (FindInLNHost<Slider>(name) is { } s) s.ValueChanged += (_, _) => act();
        }

        private void AttachCheckBoxHandler(string name, Action act)
        {
            if (FindInLNHost<CheckBox>(name) is { } c)
            {
                c.Checked += (_, _) => act();
                c.Unchecked += (_, _) => act();
            }
        }

        private void AttachTextBoxHandler(string name, Action act)
        {
            if (FindInLNHost<TextBox>(name) is { } t) t.TextChanged += (_, _) => act();
        }

        private T? FindInLNHost<T>(string name) where T : FrameworkElement
        {
            if (LNSettingsHost.Content is FrameworkElement root) return FindDescendant<T>(root, name);

            return null;
        }

        private T? FindDescendant<T>(FrameworkElement root, string name) where T : FrameworkElement
        {
            if (root.Name == name && root is T tt) return tt;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i) as FrameworkElement;
                if (child == null) continue;
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
                // Try logical parent first
                var logicalParent = LogicalTreeHelper.GetParent(element);
                if (logicalParent is ContentControl cc)
                {
                    if (Equals(cc.Content, element)) cc.Content = null;
                    return;
                }
                if (logicalParent is ScrollViewer sv)
                {
                    if (Equals(sv.Content, element)) sv.Content = null;
                    return;
                }
                if (logicalParent is Border b)
                {
                    if (b.Child == element) b.Child = null;
                    return;
                }
                if (logicalParent is Panel p)
                {
                    if (p.Children.Contains(element)) p.Children.Remove(element);
                    return;
                }

                // Fallback: try VisualTreeHelper parent removal (best-effort)
                var visualParent = VisualTreeHelper.GetParent(element);
                if (visualParent is ContentControl vcc)
                {
                    if (Equals(vcc.Content, element)) vcc.Content = null;
                }
                else if (visualParent is ScrollViewer vsv)
                {
                    if (Equals(vsv.Content, element)) vsv.Content = null;
                }
                else if (visualParent is Border vb)
                {
                    if (vb.Child == element) vb.Child = null;
                }
                else if (visualParent is Panel vp)
                {
                    if (vp.Children.Contains(element)) vp.Children.Remove(element);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UnparentElement failed: " + ex.Message);
            }
        }

        private void ConverterPreview_StartConversionRequested(object? sender, string[]? paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (_convWindowInstance == null) return;

            // Process each staged .osu file using the converter's public single-file API
            foreach (var p in paths)
            {
                try
                {
                    _convWindowInstance.ProcessSingleFile(p);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Converter processing error for {p}: {ex.Message}");
                }
            }
        }

        private void LNPreview_StartConversionRequested(object? sender, string[]? paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (_lnWindowInstance == null) return;

            foreach (var p in paths)
            {
                try
                {
                    _lnWindowInstance.ProcessSingleFile(p);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"LN processing error for {p}: {ex.Message}");
                }
            }
        }

        private void DPPreview_StartConversionRequested(object? sender, string[]? paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (_dpWindowInstance == null) return;

            foreach (var p in paths)
            {
                try
                {
                    _dpWindowInstance.ProcessSingleFile(p);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DP processing error for {p}: {ex.Message}");
                }
            }
        }

        private void GlobalOsuListenerButton_Click(object sender, RoutedEventArgs e)
        {
            // Determine active tab and provide the corresponding tool window instance as source
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            if (selectedTab == null)
            {
                MessageBox.Show("Please select a tool tab first.", "Select Tab", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            object? source = null;
            int sourceId = 0;
            if (selectedTab == Tab_Converter && _convWindowInstance != null)
            {
                source = _convWindowInstance;
                sourceId = 1;
            }
            else if (selectedTab == Tab_LNTransformer && _lnWindowInstance != null)
            {
                source = _lnWindowInstance;
                sourceId = 2;
            }
            else if (selectedTab == Tab_DPTool && _dpWindowInstance != null)
            {
                source = _dpWindowInstance;
                sourceId = 3;
            }

            var listenerWindow = new krrTools.tools.Listener.ListenerView(source, sourceId);
            listenerWindow.Show();
        }
    }
}
