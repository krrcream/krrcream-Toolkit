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
using Wpf.Ui.Controls;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using Button = Wpf.Ui.Controls.Button;
using Image = System.Windows.Controls.Image;

namespace krrTools.tools.Preview;

public class DualPreviewControl : UserControl
{
    // Named controls that were previously defined in XAML
    // initialize with null-forgiving to avoid nullable warnings (they are assigned in ctor)
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

    // Event used to broadcast loaded paths to other preview controls so they stay in sync.
    public static event EventHandler<string[]?>? SharedPathsChanged;
    // Shared staged paths across previews so Start button/state persists when switching tabs
    private static string[]? _sharedStagedPaths;
    public static event EventHandler<string[]?>? StagedPathsChanged;

    private static readonly List<DualPreviewControl> _instances = new List<DualPreviewControl>();

    private string[]? _stagedPaths;
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
                // Only log errors — avoid noisy catches elsewhere
                AppendPreviewLog("Refresh failed: " + ex.Message);
            }
        }
    }

    public DualPreviewControl()
    {
        // Build the UI in code to replace XAML
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
        Grid.SetZIndex(_sharedBgImage, -1);
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
        OriginalBorder.DragEnter += HighlightEnter;
        OriginalBorder.DragLeave += HighlightLeave;

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
        ConvertedBorder.DragEnter += HighlightEnter;
        ConvertedBorder.DragLeave += HighlightLeave;

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
        DropZone.DragEnter += HighlightEnter;
        DropZone.DragLeave += HighlightLeave;

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

        // Wire existing behaviors
        Loaded += DualPreviewControl_Loaded;
        Unloaded += DualPreviewControl_Unloaded;
        DataContextChanged += DualPreviewControl_DataContextChanged;

        // Subscribe to shared path broadcasts so this control follows other previews.
        SharedPathsChanged += OnSharedPathsChanged;
        StagedPathsChanged += OnSharedStagedPathsChanged;

        // Subscribe to language changes to update localized UI elements
        LanguageChanged += OnLanguageChanged;

        // Register instance in a thread-safe manner
        lock (_instances)
        {
            _instances.Add(this);
        }
    }

    // Ensure we unregister instances when collected/unloaded
    private void DualPreviewControl_Unloaded(object? sender, RoutedEventArgs e)
    {
        lock (_instances)
        {
            _instances.Remove(this);
        }

        // Unsubscribe static events to avoid leaks
        SharedPathsChanged -= OnSharedPathsChanged;
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
        // Preview title base
        if (_lastPaths == null || _lastPaths.Length == 0)
        {
            PreviewTitle.Text = IsChineseLanguage() ? "\u9884\u89c8" : "Preview";
        }
        else
        {
            UpdatePreviewTitleFromPaths(_lastPaths);
        }

        // Hints for original/converted
        if (Processor is BasePreviewProcessor bp)
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

    // Public static helper to enable/disable drop on all existing preview instances
    public static void SetGlobalDropEnabled(bool enabled)
    {
        DualPreviewControl[] snapshot;
        lock (_instances)
        {
            snapshot = _instances.ToArray();
        }
        foreach (var inst in snapshot)
        {
            try
            {
                inst.SetDropEnabled(enabled);
            }
            catch (Exception ex)
            {
                AppendPreviewLog($"SetGlobalDropEnabled failed on an instance: {ex.Message}");
            }
        }
    }

    private void OnSharedPathsChanged(object? sender, string[]? paths)
    {
        if (paths == null) return;
        if (_lastPaths != null && paths.Length == _lastPaths.Length && paths.SequenceEqual(_lastPaths)) return;

        // Load without re-broadcasting to avoid loops.
        LoadFiles(paths, suppressBroadcast: true);
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
        var samplePath = Path.Combine(baseDir, "mania-last-object-not-latest.osu");
        if (!File.Exists(samplePath))
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

        if (File.Exists(samplePath))
        {
            try
            {
                LoadFiles([samplePath]);
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

    public void LoadFiles(string[]? paths, bool suppressBroadcast = false)
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

        // Load files into preview (this will broadcast to other previews)
        LoadFiles(osuFiles.ToArray());

        // Stage files for conversion and show start button: broadcast first
        _sharedStagedPaths = osuFiles.ToArray();
        try
        {
            StagedPathsChanged?.Invoke(this, _sharedStagedPaths);
        }
        catch (Exception ex)
        {
            AppendPreviewLog("StagedPathsChanged broadcast failed: " + ex.Message);
        }

        // Also attempt a synchronous local update in case static event handlers aren't invoked synchronously
        try
        {
            OnSharedStagedPathsChanged(this, _sharedStagedPaths);
        }
        catch (Exception ex)
        {
            AppendPreviewLog("Failed to apply local staged UI update: " + ex.Message);
        }
    }

    // Public helper so host windows can stage files programmatically (and notify other previews)
    public void StageFiles(string[]? osuFiles)
    {
        if (osuFiles == null || osuFiles.Length == 0) return;
        BroadcastStagedPaths(osuFiles.ToArray());
        try { OnSharedStagedPathsChanged(this, osuFiles.ToArray()); } catch (Exception ex) { AppendPreviewLog("StageFiles local update failed: " + ex.Message); }
    }

    // Force-apply staged UI locally (sets internal state and updates visuals) — safe to call from host.
    public void ApplyStagedUI(string[]? osuFiles)
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

    // Public static helper so hosts can broadcast staged files to all preview instances reliably
    public static void BroadcastStagedPaths(string[]? paths)
    {
        AppendPreviewLog($"BroadcastStagedPaths invoked, count={paths?.Length ?? 0}");
        try
        {
            StagedPathsChanged?.Invoke(null, paths);
        }
        catch (Exception ex)
        {
            AppendPreviewLog("BroadcastStagedPaths failed: " + ex.Message);
        }
    }

    // New: public getter to read currently staged paths (safe copy)
    public static string[]? GetSharedStagedPaths()
    {
        return _sharedStagedPaths?.ToArray();
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
        // Raise event to notify host (MainWindow) to perform conversion using staged files.
        StartConversionRequested?.Invoke(this, _stagedPaths);
    }

    private static readonly string _previewLogPath = Path.Combine(Path.GetTempPath(), "krr_preview.log");

    private static void AppendPreviewLog(string msg)
    {
        try
        {
            var line = DateTime.Now.ToString("s") + " " + msg + Environment.NewLine;
            File.AppendAllText(_previewLogPath, line, Encoding.UTF8);
        }
        catch
        {
            // ignore logging failures
        }
    }

    // Public helper to enable/disable the drop zone accepting drops (host windows may call this)
    public void SetDropEnabled(bool enabled)
    {
        DropZone.IsHitTestVisible = enabled;
        DropZone.Opacity = enabled ? 1.0 : 0.65;
    }

    public string? CurrentTool { get; set; }
}
