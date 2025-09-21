using System;
using System.ComponentModel;
using System.Diagnostics; // for INotifyPropertyChanged
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace krrTools.tools.Preview;

public partial class DualPreviewControl
{
    // Shared paths across all preview controls. When one control loads files it will broadcast
    // the new paths so other preview controls load the same files.
    private static string[]? _sharedPaths;
    public static event EventHandler<string[]?>? SharedPathsChanged;

    // staged paths for user-confirmed conversion
    private string[]? _stagedPaths;
    // Event raised when user clicks the Start Conversion button in the drop zone
    public event EventHandler<string[]?>? StartConversionRequested;

    private bool _autoLoadedSample;
    private string[]? _lastPaths;
    private INotifyPropertyChanged? _observedDc;
    private DateTime _lastRefresh = DateTime.MinValue;

    // 列数覆盖：用于转换后的预览（可能改变列数）
    public static readonly DependencyProperty ColumnOverrideProperty = DependencyProperty.Register(
        nameof(ColumnOverride), typeof(int?), typeof(DualPreviewControl),
        new PropertyMetadata(null, OnColumnOverrideChanged));

    public int? ColumnOverride
    {
        get => (int?)GetValue(ColumnOverrideProperty);
        set => SetValue(ColumnOverrideProperty, value);
    }

    private static void OnColumnOverrideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DualPreviewControl ctrl)
        {
            ctrl.ApplyColumnOverrideToProcessor();
            ctrl.Refresh();
        }
    }

    private void ApplyColumnOverrideToProcessor()
    {
        // 若当前 Processor 是 BasePreviewProcessor，则把外部列数覆盖传递给它
        if (Processor is BasePreviewProcessor baseProc)
        {
            baseProc.ColumnOverride = ColumnOverride;
        }
    }

    public static readonly DependencyProperty AutoRefreshTokenProperty = DependencyProperty.Register(
        nameof(AutoRefreshToken), typeof(object), typeof(DualPreviewControl),
        new PropertyMetadata(null, OnAutoRefreshTokenChanged));

    public object? AutoRefreshToken
    {
        get => GetValue(AutoRefreshTokenProperty);
        set => SetValue(AutoRefreshTokenProperty, value);
    }

    private static void OnAutoRefreshTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DualPreviewControl c) c.Refresh();
    }

    public void Refresh()
    {
        if (_lastPaths is { Length: > 0 })
        {
            if (Processor == null) return;
            try
            {
                // Update title to reflect the last loaded file(s)
                UpdatePreviewTitleFromPaths(_lastPaths);

                ApplyColumnOverrideToProcessor();
                var originalVisual = Processor.BuildOriginalVisual(_lastPaths);
                var convertedVisual = Processor.BuildConvertedVisual(_lastPaths);
                // 确保注入的视觉元素可以拉伸以填充 ContentControl 可用空间
                if (originalVisual is { } ofe) EnsureStretch(ofe);
                EnsureStretch(convertedVisual);

                OriginalContent.Content = originalVisual;
                ConvertedContent.Content = convertedVisual;
                OriginalContent.Visibility = Visibility.Visible;
                ConvertedContent.Visibility = Visibility.Visible;
                // 如果 Processor 提供起始时间则在标题显示
                if (Processor is BasePreviewProcessor bp)
                {
                    OriginalHint.Text = "原始预览 (Original)" + (bp.LastOriginalStartMs.HasValue ? " - start " + bp.LastOriginalStartMs.Value.ToString() + " ms" : string.Empty);
                    ConvertedHint.Text = "结果预览 (Converted)" + (bp.LastConvertedStartMs.HasValue ? " - start " + bp.LastConvertedStartMs.Value.ToString() + " ms" : string.Empty);
                }
                else
                {
                    OriginalHint.Text = "原始预览 (Original)";
                    ConvertedHint.Text = "结果预览 (Converted)";
                }
            }
            catch
            {
                Debug.WriteLine("Refresh failed");
            }
        }
    }

    public DualPreviewControl()
    {
        InitializeComponent();
        Loaded += DualPreviewControl_Loaded;
        DataContextChanged += DualPreviewControl_DataContextChanged;

        // Subscribe to shared path broadcasts so this control follows other previews.
        SharedPathsChanged += OnSharedPathsChanged;
    }

    private void OnSharedPathsChanged(object? sender, string[]? paths)
    {
        try
        {
            // If the broadcasted paths are null or identical to our last ones, ignore.
            if (paths == null) return;
            if (_lastPaths != null && paths.Length == _lastPaths.Length && paths.SequenceEqual(_lastPaths)) return;

            // Load without re-broadcasting to avoid loops.
            LoadFiles(paths, suppressBroadcast: true);
        }
        catch
        {
            // best-effort
        }
    }

    private void DualPreviewControl_Loaded(object sender, RoutedEventArgs e)
    {
        TryAutoLoadSample();
    }

    private void TryAutoLoadSample()
    {
        if (_autoLoadedSample) return;
        if (Processor == null) return;
        if (OriginalContent.Content != null) return; // 已有内容则不自动加载
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var samplePath = Path.Combine(baseDir, "mania-last-object-not-latest.osu");
            if (!File.Exists(samplePath))
                // 回退：向上查找仓库内的示例文件
                try
                {
                    var dir = new DirectoryInfo(baseDir);
                    for (var depth = 0; depth < 5 && dir != null; depth++, dir = dir.Parent)
                    {
                        var candidate = Path.Combine(dir.FullName, "tools", "Preview",
                            "mania-last-object-not-latest.osu");
                        if (File.Exists(candidate))
                        {
                            samplePath = candidate;
                            break;
                        }
                    }
                }
                catch
                {
                    Debug.WriteLine("Auto-load sample search failed");
                }
            
            if (File.Exists(samplePath))
            {                
                LoadFiles(new[] { samplePath });
                _autoLoadedSample = true;
            }
        }
        catch
        {
            Debug.WriteLine("Auto-load sample failed");
        }
    }

    private static void OnProcessorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DualPreviewControl ctrl)
        {
            ctrl._autoLoadedSample = false; // 处理器变更时允许重新自动加载
            ctrl.TryAutoLoadSample();
        }
    }

    public static readonly DependencyProperty ProcessorProperty = DependencyProperty.Register(
        nameof(Processor), typeof(IPreviewProcessor), typeof(DualPreviewControl),
        new PropertyMetadata(null, OnProcessorChanged));

    public IPreviewProcessor? Processor
    {
        get => (IPreviewProcessor?)GetValue(ProcessorProperty);
        set => SetValue(ProcessorProperty, value);
    }

    public void LoadFiles(string[] paths, bool suppressBroadcast = false)
    {
        if (paths.Length == 0) return;
        var osu = paths.Where(p =>
            File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (osu.Length == 0) return;
        _lastPaths = osu; // 存储以便刷新使用

        // Broadcast to other preview controls (unless suppressed)
        if (!suppressBroadcast)
        {
            try
            {
                _sharedPaths = osu;
                SharedPathsChanged?.Invoke(this, osu);
            }
            catch
            {
                // ignore
            }
        }

        // Update title to show the loaded file name(s)
        UpdatePreviewTitleFromPaths(_lastPaths);

        ApplyColumnOverrideToProcessor();
        if (Processor == null)
        {
            OriginalContent.Content = new TextBlock { Text = "No processor set", Foreground = Brushes.DarkRed };
            OriginalContent.Visibility = Visibility.Visible;
            ConvertedContent.Content = null;
            ConvertedContent.Visibility = Visibility.Collapsed;

            OriginalHint.Text = "原始预览 (Original)";
            return;
        }

        try
        {
            var originalVisual = Processor.BuildOriginalVisual(osu);
            var convertedVisual = Processor.BuildConvertedVisual(osu);
            // 确保注入的视觉元素可以拉伸以填充 ContentControl 可用空间
            if (originalVisual is { } ofe) EnsureStretch(ofe);
            if (convertedVisual is { } cfe) EnsureStretch(cfe);

            OriginalContent.Content = originalVisual;
            ConvertedContent.Content = convertedVisual;
            OriginalContent.Visibility = Visibility.Visible;
            ConvertedContent.Visibility = Visibility.Visible;
            // 构建完成后更新标题
            if (Processor is BasePreviewProcessor bp)
            {
                OriginalHint.Text = "原始预览 (Original)" + (bp.LastOriginalStartMs.HasValue ? " - start " + bp.LastOriginalStartMs.Value.ToString() + " ms" : string.Empty);
                ConvertedHint.Text = "结果预览 (Converted)" + (bp.LastConvertedStartMs.HasValue ? " - start " + bp.LastConvertedStartMs.Value.ToString() + " ms" : string.Empty);
            }
            else
            {
                OriginalHint.Text = "原始预览 (Original)";
                ConvertedHint.Text = "结果预览 (Converted)";
            }
        }
        catch (Exception ex)
        {
            OriginalContent.Content = new TextBlock
            {
                Text = "Preview error: " + ex.Message, Foreground = Brushes.DarkRed,
                TextWrapping = TextWrapping.Wrap
            };
            OriginalContent.Visibility = Visibility.Visible;
            OriginalHint.Text = "原始预览 (Original)";
        }
    }

    private void EnsureStretch(FrameworkElement fe)
    {
        // 统一设置使元素可拉伸并消除最小高度限制
        fe.HorizontalAlignment = HorizontalAlignment.Stretch;
        fe.VerticalAlignment = VerticalAlignment.Stretch;
        fe.Height = double.NaN;
        fe.MinHeight = 0;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null) return;

        // Load files into preview (this will broadcast to other previews)
        LoadFiles(files);

        // Stage files for conversion and show start button
        _stagedPaths = files.Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (_stagedPaths != null && _stagedPaths.Length > 0)
        {
            StartConversionButton.Visibility = Visibility.Visible;
            DropHintEn.Text = $"{_stagedPaths.Length} file(s) staged. Click Start to convert.";
            DropHintCn.Text = $"已暂存 {_stagedPaths.Length} 个文件，点击开始转换。";
        }

        e.Handled = true; // 防止全局窗口的放置处理被重复触发
    }

    private void HighlightEnter(object sender, DragEventArgs e)
    {
        if (sender is Border b) b.Background = new SolidColorBrush(Color.FromRgb(245, 247, 252));
    }

    private void HighlightLeave(object sender, DragEventArgs e)
    {
        if (sender is Border b)
        {
            if (b == OriginalBorder || b == ConvertedBorder)
                b.Background = new SolidColorBrush(Color.FromRgb(250, 250, 251));
            else if (b == DropZone)
                b.Background = new SolidColorBrush(Color.FromRgb(245, 248, 255));
        }
    }

    private void DualPreviewControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_observedDc != null)
            _observedDc.PropertyChanged -= ObservedDcOnPropertyChanged;
        _observedDc = e.NewValue as INotifyPropertyChanged;
        if (_observedDc != null)
            _observedDc.PropertyChanged += ObservedDcOnPropertyChanged;
    }

    private void ObservedDcOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 去抖：距离上次刷新少于 120ms 则忽略，避免频繁触发
        if ((DateTime.UtcNow - _lastRefresh).TotalMilliseconds < 120) return;
        _lastRefresh = DateTime.UtcNow;
        Refresh();
    }

    // Update the preview title to include the first filename (with count) and truncate if too long.
    private void UpdatePreviewTitleFromPaths(string[]? paths)
    {
        try
        {
            if (paths == null || paths.Length == 0)
            {
                PreviewTitle.Text = "预览 / Preview";
                return;
            }

            var name = Path.GetFileName(paths[0]);
            if (paths.Length > 1) name += $" (+{paths.Length - 1} more)";

            // Truncate to a reasonable length, preserving extension if present
            var truncated = TruncateFileNameMiddle(name, 40);
            PreviewTitle.Text = "预览 / Preview : " + truncated;
            // Show full path(s) in tooltip so users can inspect the full filename(s)
            try
            {
                PreviewTitle.ToolTip = paths.Length == 1 ? paths[0] : string.Join("\n", paths);
            }
            catch
            {
                PreviewTitle.ToolTip = null;
            }
        }
        catch
        {
            // best-effort; on failure keep base title
            PreviewTitle.Text = "预览 / Preview";
        }
    }

    // Truncate a filename in the middle, attempting to preserve the file extension.
    private static string TruncateFileNameMiddle(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLen) return name;

        var ext = Path.GetExtension(name);
        var nameOnly = ext.Length > 0 ? name.Substring(0, name.Length - ext.Length) : name;
        var extLen = ext.Length;

        // Reserve 3 chars for ellipsis
        var allowedNameLen = Math.Max(0, maxLen - extLen - 3);
        if (allowedNameLen <= 0)
        {
            // fallback: simple middle truncation without guaranteeing extension
            var headLen = (maxLen - 3) / 2;
            var tailLen = (maxLen - 3) - headLen;
            return name.Substring(0, headLen) + "..." + name.Substring(name.Length - tailLen);
        }

        var head = allowedNameLen / 2;
        var tail = allowedNameLen - head;
        if (nameOnly.Length <= head + tail)
            return name; // shouldn't happen because we already checked length

        var headPart = nameOnly.Substring(0, head);
        var tailPart = nameOnly.Substring(nameOnly.Length - tail);
        return headPart + "..." + tailPart + ext;
    }

    private void StartConversionButton_Click(object sender, RoutedEventArgs e)
    {
        // Raise event to notify host (MainWindow) to perform conversion using staged files
        try
        {
            StartConversionRequested?.Invoke(this, _stagedPaths);
        }
        finally
        {
            // hide button after request
            StartConversionButton.Visibility = Visibility.Collapsed;
            DropHintEn.Text = "Drag &amp; Drop .osu files in here";
            DropHintCn.Text = "将 .osu 文件拖到此区域";
            _stagedPaths = null;
        }
    }
}