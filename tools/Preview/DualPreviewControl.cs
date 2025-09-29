using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using krrTools.tools.Shared;
using static krrTools.tools.Shared.SharedUIComponents;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using Button = Wpf.Ui.Controls.Button;
using System.Diagnostics;
using Image = System.Windows.Controls.Image;

namespace krrTools.tools.Preview;

public class DualPreviewControl : UserControl
{
    private TextBlock PreviewTitle;
    private TextBlock OriginalHint;
    private TextBlock ConvertedHint;
    private ContentControl OriginalContent;
    private ContentControl ConvertedContent;
    private Border DropZone;
    private TextBlock DropHintCn;
    private TextBlock DropHintEn;
    private Button StartConversionButton;
    private Border OriginalBorder;
    private Border ConvertedBorder;
    private Image? _sharedBgImage;
    
    private INotifyPropertyChanged? _observedDc;
    private DateTime _lastRefresh = DateTime.MinValue;
    
    private static readonly List<DualPreviewControl> Instances = new();
    private static string[]? _sharedStagedPaths;
    private string[]? _stagedPaths;
    private string[]? _lastPaths;
    private bool _autoLoadedSample;
    
    public static event EventHandler<string[]?>? SharedPathsChanged;
    public static event EventHandler<string[]?>? StagedPathsChanged;
    public event EventHandler<string[]?>? StartConversionRequested;
    
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
        // 若当前 Processor 是 PreviewProcessor，则把外部列数覆盖传递给它
        if (Processor is PreviewProcessor baseProc)
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

            // Update title and attempt to rebuild visuals. Keep a focused try/catch only around the build
            UpdatePreviewTitleFromPaths(_lastPaths);

            // Set background image
            string? bgPath = PreviewTransformation.GetBackgroundImagePath(_lastPaths[0]);
            BitmapImage? bgBitmap = null;
            if (bgPath != null && File.Exists(bgPath))
            {
                bgBitmap = new BitmapImage();
                bgBitmap.BeginInit();
                bgBitmap.UriSource = new Uri(bgPath);
                bgBitmap.CacheOption = BitmapCacheOption.OnLoad;
                bgBitmap.EndInit();
            }
            if (_sharedBgImage != null)
            {
                _sharedBgImage.Source = bgBitmap;
                _sharedBgImage.Visibility = Visibility.Collapsed; // 背景统一由窗口处理
            }

            ApplyColumnOverrideToProcessor();
            try
            {
                var originalVisual = Processor.BuildOriginalVisual(_lastPaths);
                var convertedVisual = Processor.BuildConvertedVisual(_lastPaths);

                if (originalVisual is { } ofe) EnsureStretch(ofe);
                if (convertedVisual is { } cfe) EnsureStretch(cfe);

                OriginalContent.Content = originalVisual;
                ConvertedContent.Content = convertedVisual;
                OriginalContent.Visibility = Visibility.Visible;
                ConvertedContent.Visibility = Visibility.Visible;

                if (Processor is PreviewProcessor bp)
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
                // Only log errors — avoid noisy catches elsewhere
                AppendPreviewLog("Refresh failed: " + ex.Message);
            }
        }
    }

    public DualPreviewControl()
    {
        AllowDrop = true;

        var rootBorder = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = SharedUIComponents.PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = SharedUIComponents.PanelCornerRadius,
            Padding = new Thickness(12)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        PreviewTitle = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, Text = "预览 / Preview" };
        Grid.SetRow(PreviewTitle, 0);
        grid.Children.Add(PreviewTitle);

        // Shared background image
        _sharedBgImage = new Image { Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Opacity = PreviewBackgroundOpacity,
            Effect = new BlurEffect { Radius = PreviewBackgroundBlurRadius }, Visibility = Visibility.Collapsed
        };
        Grid.SetRow(_sharedBgImage, 1);
        Grid.SetRowSpan(_sharedBgImage, 3);
        Panel.SetZIndex(_sharedBgImage, -1);
        grid.Children.Add(_sharedBgImage);

        // Original Border
        OriginalBorder = new Border
        {
            AllowDrop = true,
            Background = Brushes.Transparent,
            BorderBrush = SharedUIComponents.PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = SharedUIComponents.PanelCornerRadius,
            Margin = new Thickness(0,4,0,4),
            Padding = new Thickness(6),
            ClipToBounds = true
        };
        OriginalBorder.Drop += OnDrop;

        var obGrid = new Grid();
        obGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        obGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        obGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        OriginalHint = new TextBlock { FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33,0x33,0x33)), Margin = new Thickness(2,0,2,4), Text = "原始预览 (Original)" };
        Grid.SetRow(OriginalHint, 0);
        obGrid.Children.Add(OriginalHint);
        OriginalContent = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Visibility = Visibility.Collapsed };
        Grid.SetRow(OriginalContent, 1);
        obGrid.Children.Add(OriginalContent);
        OriginalBorder.Child = obGrid;
        Grid.SetRow(OriginalBorder, 1);
        grid.Children.Add(OriginalBorder);

        // center arrow
        var centerStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal };
        centerStack.Children.Add(new TextBlock { FontSize = 18, Foreground = new SolidColorBrush(Color.FromRgb(0x5A,0x63,0x70)), Text = "↓ ↓" });
        Grid.SetRow(centerStack, 2);
        grid.Children.Add(centerStack);

        // Converted Border
        ConvertedBorder = new Border
        {
            AllowDrop = true,
            Background = Brushes.Transparent,
            BorderBrush = SharedUIComponents.PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = SharedUIComponents.PanelCornerRadius,
            Margin = new Thickness(0,4,0,4),
            Padding = new Thickness(6),
            ClipToBounds = true
        };
        ConvertedBorder.Drop += OnDrop;

        var cbGrid = new Grid();
        cbGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cbGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cbGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ConvertedHint = new TextBlock { FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33,0x33,0x33)), Margin = new Thickness(2,0,2,4), Text = "结果预览 (Converted)" };
        Grid.SetRow(ConvertedHint, 0);
        cbGrid.Children.Add(ConvertedHint);
        ConvertedContent = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Visibility = Visibility.Collapsed };
        Grid.SetRow(ConvertedContent, 1);
        cbGrid.Children.Add(ConvertedContent);
        ConvertedBorder.Child = cbGrid;
        Grid.SetRow(ConvertedBorder, 3);
        grid.Children.Add(ConvertedBorder);

        // Drop Zone
        DropZone = new Border
        {
            AllowDrop = true,
            Background = new SolidColorBrush(Color.FromRgb(0xF5,0xF8,0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xAF,0xC8,0xFF)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0,8,0,0),
            Padding = new Thickness(12)
        };
        DropZone.Drop += OnDrop;

        var dzGrid = new Grid();
        var centerTexts = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        DropHintCn = new TextBlock { FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x33,0x67,0xB5)), Text = "将 .osu 文件拖到此区域", TextAlignment = TextAlignment.Center };
        DropHintEn = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x6A,0x7A,0x90)), Text = "Drag & Drop .osu files in here", TextAlignment = TextAlignment.Center };
        centerTexts.Children.Add(DropHintCn);
        centerTexts.Children.Add(DropHintEn);
        dzGrid.Children.Add(centerTexts);

        StartConversionButton = new Button { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Content = "开始转换", Padding = new Thickness(8,6,8,6), Visibility = Visibility.Collapsed, Margin = new Thickness(8,0,6,0), MinWidth = 92 };
        StartConversionButton.Click += StartConversionButton_Click;
        dzGrid.Children.Add(StartConversionButton);

        DropZone.Child = dzGrid;
        Grid.SetRow(DropZone, 4);
        grid.Children.Add(DropZone);

        rootBorder.Child = grid;
        Content = rootBorder;

        Loaded += DualPreviewControl_Loaded;
        Unloaded += DualPreviewControl_Unloaded;
        
        DataContextChanged += DualPreviewControl_DataContextChanged;
        SharedPathsChanged += OnSharedPathToPreviewChanged; // 统一所有控件的预览
        StagedPathsChanged += OnSharedStagedPathsChanged; // 统一所有控件的暂存路径
        LanguageChanged += OnLanguageChanged;

        lock (Instances)
        {
            Instances.Add(this);
        }
    }

    private void DualPreviewControl_Unloaded(object? sender, RoutedEventArgs e)
    {
        lock (Instances)
        {
            Instances.Remove(this);
        }

        // Unsubscribe static events to avoid leaks
        SharedPathsChanged -= OnSharedPathToPreviewChanged;
        StagedPathsChanged -= OnSharedStagedPathsChanged;
        LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(OnLanguageChanged));
            return;
        }

        UpdateLanguageTexts();
    }

    private void UpdateLanguageTexts()
    {
        if (_lastPaths == null || _lastPaths.Length == 0)
        {
            PreviewTitle.Text = IsChineseLanguage() ? "\u9884\u89c8" : "Preview";
        }
        else
        {
            UpdatePreviewTitleFromPaths(_lastPaths);
        }

        // Hints for original/converted
        if (Processor is PreviewProcessor bp)
        {
            OriginalHint.Text = (IsChineseLanguage() ? "原始预览" : "Original") + (bp.LastOriginalStartMs.HasValue ? " - start " + bp.LastOriginalStartMs.Value.ToString() + " ms" : string.Empty);
            ConvertedHint.Text = (IsChineseLanguage() ? "结果预览" : "Converted") + (bp.LastConvertedStartMs.HasValue ? " - start " + bp.LastConvertedStartMs.Value.ToString() + " ms" : string.Empty);
        }
        else
        {
            OriginalHint.Text = IsChineseLanguage() ? "原始预览" : "Original";
            ConvertedHint.Text = IsChineseLanguage() ? "结果预览" : "Converted";
        }

        // Drop hints and Start button
        if (_stagedPaths == null || _stagedPaths.Length == 0)
        {
            DropHintEn.Text = "Drag & Drop .osu files in here";
            DropHintCn.Text = "将 .osu 文件拖到此区域";
            StartConversionButton.Content = IsChineseLanguage() ? "开始转换" : "Start";
        }
        else
        {
            DropHintEn.Text = $"{_stagedPaths.Length} file(s) staged. Click Start to convert.";
            DropHintCn.Text = $"已暂存 {_stagedPaths.Length} 个文件，点击开始转换。";
            StartConversionButton.Content = IsChineseLanguage() ? "开始转换" : "Start";
        }
    }

    private void OnSharedPathToPreviewChanged(object? sender, string[]? paths)
    {
        if (paths == null) return;
        if (_lastPaths != null && paths.Length == _lastPaths.Length && paths.SequenceEqual(_lastPaths)) return;

        LoadPreview(paths, suppressBroadcast: true);
    }

    private void OnSharedStagedPathsChanged(object? sender, string[]? paths)
    {
        // Ensure UI updates always happen on the UI thread
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => OnSharedStagedPathsChanged(sender, paths)));
            return;
        }

        // If no staged paths, hide button and reset hints
        if (paths == null || paths.Length == 0)
        {
            _stagedPaths = null;
            StartConversionButton.Visibility = Visibility.Collapsed;
            StartConversionButton.IsHitTestVisible = false;
            Panel.SetZIndex(StartConversionButton, 0);

            DropHintEn.Text = "Drag & Drop .osu files in here";
            DropHintCn.Text = "将 .osu 文件拖到此区域";

            DropZone.InvalidateMeasure();
            DropZone.UpdateLayout();
            InvalidateMeasure();
            UpdateLayout();
            return;
        }

        // If identical to current, ignore
        if (_stagedPaths != null && paths.Length == _stagedPaths.Length && paths.SequenceEqual(_stagedPaths))
            return;

        _stagedPaths = paths.ToArray();
        StartConversionButton.Visibility = Visibility.Visible;
        StartConversionButton.IsHitTestVisible = true;
        Panel.SetZIndex(StartConversionButton, 100);
        StartConversionButton.BringIntoView();

        DropHintEn.Text = $"{_stagedPaths.Length} file(s) staged. Click Start to convert.";
        DropHintCn.Text = $"已暂存 {_stagedPaths.Length} 个文件，点击开始转换。";

        DropZone.InvalidateMeasure();
        DropZone.UpdateLayout();
        InvalidateMeasure();
        UpdateLayout();
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

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var samplePath = Path.Combine(baseDir, "mania-PreView.osu");
        if (!File.Exists(samplePath))
        {
            var dir = new DirectoryInfo(baseDir);
            for (var depth = 0; depth < 5 && dir != null; depth++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "tools", "Preview",
                    "mania-PreView.osu");
                if (File.Exists(candidate))
                {
                    samplePath = candidate;
                    break;
                }
            }
        }

        if (File.Exists(samplePath))
        {
            try
            {
                LoadPreview([samplePath]);
                _autoLoadedSample = true;
            }
            catch (Exception ex)
            {
                AppendPreviewLog("Auto-load sample failed: " + ex.Message);
            }
        }
    }

    private static void OnProcessorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DualPreviewControl ctrl)
        {
            ctrl._autoLoadedSample = false; // 处理器变更时允许重新自动加载
            ctrl.TryAutoLoadSample();
            ctrl.Refresh(); // 强制刷新预览以反映新的处理器
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

    public void LoadPreview(string[]? paths, bool suppressBroadcast = false)
    {
        if (paths == null || paths.Length == 0) return;
        var osu = paths.Where(p => File.Exists(p) && Path.GetExtension(p).Equals(".osu", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (osu.Length == 0) return;
        _lastPaths = osu; // 存储以便刷新使用

        // Broadcast to other preview controls (unless suppressed)
        if (!suppressBroadcast)
        {
            SharedPathsChanged?.Invoke(this, osu);
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
            EnsureStretch(originalVisual);
            EnsureStretch(convertedVisual);

            OriginalContent.Content = originalVisual;
            ConvertedContent.Content = convertedVisual;
            OriginalContent.Visibility = Visibility.Visible;
            ConvertedContent.Visibility = Visibility.Visible;

            if (Processor is PreviewProcessor bp)
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
            AppendPreviewLog("Preview build failed: " + ex.Message);
        }
    }

    private void EnsureStretch(FrameworkElement fe)
    {
        fe.HorizontalAlignment = HorizontalAlignment.Stretch;
        fe.VerticalAlignment = VerticalAlignment.Stretch;
        fe.Height = double.NaN;
        fe.MinHeight = 0;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0) return;

        // Collect .osu files from dropped items. Support both files and directories.
        var osuFiles = new List<string>();
        foreach (var item in files)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            if (File.Exists(item))
            {
                if (Path.GetExtension(item).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                    osuFiles.Add(item);
            }
            else if (Directory.Exists(item))
            {
                try
                {
                    var found = Directory.GetFiles(item, "*.osu", SearchOption.AllDirectories);
                    if (found.Length > 0)
                        osuFiles.AddRange(found);
                }
                catch (Exception ex)
                {
                    AppendPreviewLog($"Directory enumerate failed for '{item}': {ex.Message}");
                }
            }
        }

        if (osuFiles.Count == 0) return;

        LoadPreview(osuFiles.ToArray());

        _sharedStagedPaths = osuFiles.ToArray();
        try
        {
            StagedPathsChanged?.Invoke(this, _sharedStagedPaths);
        }
        catch (Exception ex)
        {
            AppendPreviewLog("StagedPathsChanged broadcast failed: " + ex.Message);
        }

        try
        {
            OnSharedStagedPathsChanged(this, _sharedStagedPaths);
        }
        catch (Exception ex)
        {
            AppendPreviewLog("Failed to apply local staged UI update: " + ex.Message);
        }
    }

    public void StageFiles(string[]? osuFiles)
    {
        if (osuFiles == null || osuFiles.Length == 0) return;
        BroadcastStagedPaths(osuFiles.ToArray());
        try { OnSharedStagedPathsChanged(this, osuFiles.ToArray()); } catch (Exception ex) { AppendPreviewLog("StageFiles local update failed: " + ex.Message); }
    }

    public void ApplyDropZoneStagedUI(string[]? osuFiles)
    {
        if (osuFiles == null || osuFiles.Length == 0) return;
        _stagedPaths = osuFiles.ToArray();
        _sharedStagedPaths = _stagedPaths;

        StartConversionButton.Visibility = Visibility.Visible;
        StartConversionButton.IsHitTestVisible = true;
        Panel.SetZIndex(StartConversionButton, 100);
        StartConversionButton.BringIntoView();

        DropHintEn.Text = $"{_stagedPaths.Length} file(s) staged. Click Start to convert.";
        DropHintCn.Text = $"已暂存 {_stagedPaths.Length} 个文件，点击开始转换。";

        DropZone.InvalidateMeasure();
        DropZone.UpdateLayout();
        InvalidateMeasure();
        UpdateLayout();
    }

    // 公开的静态方法，允许外部广播暂存的路径（例如文件调度器）
    public static void BroadcastStagedPaths(string[]? paths)
    {
        try
        {
            StagedPathsChanged?.Invoke(null, paths);
        }
        catch (Exception ex)
        {
            AppendPreviewLog("公开暂存路径失败: " + ex.Message);
        }
    }

    // 共享暂存路径
    public static string[]? GetSharedStagedPaths()
    {
        return _sharedStagedPaths?.ToArray();
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

    private void UpdatePreviewTitleFromPaths(string[]? paths)
    {
        if (paths == null || paths.Length == 0)
        {
            PreviewTitle.Text = "预览 / Preview";
            return;
        }

        var name = Path.GetFileName(paths[0]);
        if (paths.Length > 1) name += $" (+{paths.Length - 1} more)";

        var truncated = TruncateFileNameMiddle(name, 40);
        PreviewTitle.Text = "预览 / Preview : " + truncated;
        PreviewTitle.ToolTip = paths.Length == 1 ? paths[0] : string.Join("\n", paths);
    }

    // 防止文件名过长，进行中间截断
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
        Debug.WriteLine($"Start conversion clicked, staged paths: {_stagedPaths?.Length ?? 0}");
        StartConversionRequested?.Invoke(this, _stagedPaths);
    }

    private static readonly string _previewLogPath = Path.Combine(Path.GetTempPath(), "krr_preview.log");

    private static void AppendPreviewLog(string msg)
    {
#if DEBUG
            var line = DateTime.Now.ToString("s") + " " + msg + Environment.NewLine;
            File.AppendAllText(_previewLogPath, line, Encoding.UTF8);
#endif
    }

    public string? CurrentTool { get; set; }
}
