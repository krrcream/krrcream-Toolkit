using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using krrTools.Beatmaps;
using krrTools.Localization;
using krrTools.Utilities;
using krrTools.Core;
using static krrTools.UI.SharedUIComponents;
using Image = System.Windows.Controls.Image;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace krrTools.Tools.Preview;
public class DualPreviewControl : Border
{
    // 移除_borders字段
    private (TextBlock PreviewTitle, TextBlock OriginalHint, TextBlock ConvertedHint, TextBlock StartTimeDisplay) _textBlocks = (new TextBlock(), new TextBlock(), new TextBlock(), new TextBlock());
    private (ContentControl OriginalContent, ContentControl ConvertedContent) _contentControls = (new ContentControl(), new ContentControl());
    private Image? _sharedBgImage = new Image();
    private bool _autoLoadedSample;
    private INotifyPropertyChanged? _observedDc;
    private DateTime _lastRefresh = DateTime.MinValue;
    private ManiaBeatmap? _lastBeatmap;
    public ToolScheduler? Scheduler { get; set; }

    // 列数覆盖：用于转换后的预览（可能改变列数）
    public static readonly DependencyProperty ColumnOverrideProperty = DependencyProperty.Register(
        nameof(ColumnOverride), typeof(int?), typeof(DualPreviewControl),
        new PropertyMetadata(null, OnAnyPropertyChanged));

    public int? ColumnOverride
    {
        get => (int?)GetValue(ColumnOverrideProperty);
        set => SetValue(ColumnOverrideProperty, value);
    }

    public static readonly DependencyProperty AutoRefreshTokenProperty = DependencyProperty.Register(
        nameof(AutoRefreshToken), typeof(object), typeof(DualPreviewControl),
        new PropertyMetadata(null, OnAnyPropertyChanged));

    public object? AutoRefreshToken
    {
        get => GetValue(AutoRefreshTokenProperty);
        set => SetValue(AutoRefreshTokenProperty, value);
    }

    public static readonly DependencyProperty ProcessorProperty = DependencyProperty.Register(
        nameof(Processor), typeof(IPreviewProcessor), typeof(DualPreviewControl),
        new PropertyMetadata(null, OnAnyPropertyChanged));

    public IPreviewProcessor? Processor
    {
        get => (IPreviewProcessor?)GetValue(ProcessorProperty);
        set => SetValue(ProcessorProperty, value);
    }

    // 通用属性变更回调
    private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DualPreviewControl ctrl)
        {
            if (e.Property == ProcessorProperty)
                ctrl._autoLoadedSample = false;
            ctrl.TryAutoLoadSample();
            ctrl.RefreshWithCatch();
        }
    }

    // 合并异常处理
    private void RunWithCatch(Action action, string? logMsg = null)
    {
        try { action(); }
        catch (Exception ex) { AppendPreviewLog((logMsg ?? "Error") + ": " + ex.Message); SetErrorState(ex.Message); }
    }
    private void RefreshWithCatch() => RunWithCatch(Refresh, "Refresh failed");

    public void Refresh()
    {
        if (_lastBeatmap == null || Processor == null) return;
        UpdatePreviewTitleFromPaths(_lastBeatmap);
        SetBackgroundImage(_lastBeatmap.FilePath);
        ApplyColumnOverrideToProcessor();
        BuildAndSetVisualsWithCatch();
    }
    private void BuildAndSetVisualsWithCatch() => RunWithCatch(BuildAndSetVisuals, "BuildAndSetVisuals failed");

    private void BuildAndSetVisuals()
    {
        if (_lastBeatmap == null || Processor == null) return;
        var originalVisual = Processor.BuildVisual(_lastBeatmap, false);
        var convertedVisual = Processor.BuildVisual(_lastBeatmap, true);
        if (originalVisual is { } ofe) EnsureStretch(ofe);
        if (convertedVisual is { } cfe) EnsureStretch(cfe);
        _contentControls.OriginalContent.Content = originalVisual;
        _contentControls.ConvertedContent.Content = convertedVisual;
        _contentControls.OriginalContent.Visibility = Visibility.Visible;
        _contentControls.ConvertedContent.Visibility = Visibility.Visible;
        UpdateHints();
    }

    private void UpdateHints()
    {
        _textBlocks.OriginalHint.Text = Strings.OriginalHint.Localize();
        _textBlocks.ConvertedHint.Text = Strings.ConvertedHint.Localize();
        string startMsText = (Processor is ConverterProcessor bp && bp.LastStartMs != 0) ? $"start {bp.LastStartMs} ms" : string.Empty;
        _textBlocks.StartTimeDisplay.Text = startMsText;
    }

    public DualPreviewControl()
    {
        Background = Brushes.Transparent;
        BorderBrush = PanelBorderBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = PanelCornerRadius;
        Padding = new Thickness(12);
        Margin = new Thickness(8);
        Visibility = Visibility.Collapsed;
        InitializeUI();
        InitializeEvents();
    }

    private void InitializeUI()
    {
        var previewTitle = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, Text = Strings.PreviewTitle.Localize() };
        var sharedBgImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Opacity = PreviewBackgroundOpacity,
            Effect = new BlurEffect { Radius = PreviewBackgroundBlurRadius },
            Visibility = Visibility.Collapsed
        };
        var originalHint = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51)),
            Margin = new Thickness(2, 0, 2, 4),
            Text = Strings.OriginalHint.Localize()
        };
        var originalContent = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        var originalGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { originalHint, originalContent }
        };
        Grid.SetRow(originalHint, 0);
        Grid.SetRow(originalContent, 1);
        var originalBorder = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = PanelCornerRadius,
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(6),
            ClipToBounds = true,
            Child = originalGrid
        };
        var startTimeDisplay = new TextBlock
        {
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 90, 99, 112)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 10, 0),
            Text = ""
        };
        var centerStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Horizontal,
            Children =
            {
                startTimeDisplay,
                new TextBlock
                {
                    FontSize = 18,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 90, 99, 112)),
                    Text = "↓ ↓"
                }
            }
        };
        var convertedHint = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51)),
            Margin = new Thickness(2, 0, 2, 4),
            Text = Strings.ConvertedHint.Localize()
        };
        var convertedContent = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        var convertedGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { convertedHint, convertedContent }
        };
        Grid.SetRow(convertedHint, 0);
        Grid.SetRow(convertedContent, 1);
        var convertedBorder = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = PanelCornerRadius,
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(6),
            ClipToBounds = true,
            Child = convertedGrid
        };
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { previewTitle, sharedBgImage, originalBorder, centerStack, convertedBorder }
        };
        Child = grid;
        Grid.SetRow(previewTitle, 0);
        Grid.SetRow(sharedBgImage, 1);
        Grid.SetRowSpan(sharedBgImage, 3);
        Panel.SetZIndex(sharedBgImage, -1);
        Grid.SetRow(originalBorder, 1);
        Grid.SetRow(centerStack, 2);
        Grid.SetRow(convertedBorder, 3);

        // 赋值到元组
        _textBlocks = (previewTitle, originalHint, convertedHint, startTimeDisplay);
        _contentControls = (originalContent, convertedContent);
        _sharedBgImage = sharedBgImage;
    }

    private void InitializeEvents()
    {
        Loaded += DualPreviewControl_Loaded;
        Unloaded += DualPreviewControl_Unloaded;
        DataContextChanged += DualPreviewControl_DataContextChanged;
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void DualPreviewControl_Unloaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
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
        _textBlocks.PreviewTitle.Text = Strings.PreviewTitle.Localize();
        UpdatePreviewTitleFromPaths(_lastBeatmap);
        _textBlocks.OriginalHint.Text = Strings.OriginalHint.Localize();
        _textBlocks.ConvertedHint.Text = Strings.ConvertedHint.Localize();
    }

    private void DualPreviewControl_Loaded(object sender, RoutedEventArgs e)
    {
        TryAutoLoadSample();
    }

    private void TryAutoLoadSample()
    {
        if (_autoLoadedSample || Processor == null || _contentControls.OriginalContent.Content != null) return;
        string samplePath = FindSampleFile();
        LoadPreview(samplePath);
        _autoLoadedSample = true;
    }

    private string FindSampleFile()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string samplePath = Path.Combine(baseDir, "mania-PreView.osu");
        if (File.Exists(samplePath)) return samplePath;
        var dir = new DirectoryInfo(baseDir);
        for (int depth = 0; depth < 5 && dir != null; depth++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "tools", "Preview", "mania-PreView.osu");
            if (File.Exists(candidate)) return candidate;
        }
        return string.Empty;
    }

    private void LoadPreview(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path) || !Path.GetExtension(path).Equals(".osu", StringComparison.OrdinalIgnoreCase)) return;
        _lastBeatmap = Scheduler?.LoadBeatmap(path);
        if (_lastBeatmap != null)
        {
            LoadPreview(_lastBeatmap);
        }
    }

    public void LoadPreview(ManiaBeatmap? beatmap)
    {
        if (beatmap == null) return;
        _lastBeatmap = beatmap;
        UpdatePreviewTitleFromPaths(_lastBeatmap);
        ApplyColumnOverrideToProcessor();
        if (Processor == null)
        {
            SetNoProcessorState();
            return;
        }
        BuildAndSetVisualsWithCatch();
    }

    private void SetNoProcessorState()
    {
        _contentControls.OriginalContent.Content = new TextBlock { Text = Strings.NoProcessorSet.Localize(), Foreground = Brushes.DarkRed };
        _contentControls.OriginalContent.Visibility = Visibility.Visible;
        _contentControls.ConvertedContent.Content = null;
        _contentControls.ConvertedContent.Visibility = Visibility.Collapsed;
        _textBlocks.OriginalHint.Text = Strings.OriginalHint.Localize();
    }

    private void SetErrorState(string message)
    {
        _contentControls.OriginalContent.Content = new TextBlock
        {
            Text = string.Format(Strings.PreviewError.Localize(), message),
            Foreground = Brushes.DarkRed,
            TextWrapping = TextWrapping.Wrap
        };
        _contentControls.OriginalContent.Visibility = Visibility.Visible;
        _textBlocks.OriginalHint.Text = Strings.OriginalHint.Localize();
        AppendPreviewLog(string.Format(Strings.PreviewBuildFailed.Localize(), message));
    }

    private void EnsureStretch(FrameworkElement fe)
    {
        fe.HorizontalAlignment = HorizontalAlignment.Stretch;
        fe.VerticalAlignment = VerticalAlignment.Stretch;
        fe.Height = double.NaN;
        fe.MinHeight = 0;
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
        if ((DateTime.UtcNow - _lastRefresh).TotalMilliseconds < 120) return;
        _lastRefresh = DateTime.UtcNow;
        RefreshWithCatch();
    }

    private void UpdatePreviewTitleFromPaths(ManiaBeatmap? beatmap)
    {
        if (beatmap == null)
        {
            _textBlocks.PreviewTitle.Text = Strings.PreviewTitle.Localize();
            return;
        }
        string name = Path.GetFileName(beatmap.FilePath);
        string truncated = TruncateFileNameMiddle(name, 40);
        _textBlocks.PreviewTitle.Text = Strings.PreviewTitle.Localize() + " : " + truncated;
        _textBlocks.PreviewTitle.ToolTip = beatmap.FilePath;
    }

    private void SetBackgroundImage(string? path)
    {
        if (path == null || !File.Exists(path)) return;
        string? bgPath = PreviewTransformation.GetBackgroundImagePath(path);
        if (bgPath != null && File.Exists(bgPath) && _sharedBgImage != null)
        {
            var bgBitmap = new BitmapImage();
            bgBitmap.BeginInit();
            bgBitmap.UriSource = new Uri(bgPath);
            bgBitmap.CacheOption = BitmapCacheOption.OnLoad;
            bgBitmap.EndInit();
            _sharedBgImage.Source = bgBitmap;
            _sharedBgImage.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyColumnOverrideToProcessor()
    {
        if (Processor is ConverterProcessor baseProc && ColumnOverride != null)
            baseProc.ColumnOverride = (int)ColumnOverride;
    }

    private static string TruncateFileNameMiddle(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLen) return name;
        string ext = Path.GetExtension(name);
        string nameOnly = ext.Length > 0 ? name.Substring(0, name.Length - ext.Length) : name;
        int extLen = ext.Length;
        int allowedNameLen = Math.Max(0, maxLen - extLen - 3);
        if (allowedNameLen <= 0)
        {
            int headLen = (maxLen - 3) / 2;
            int tailLen = (maxLen - 3) - headLen;
            return name.Substring(0, headLen) + "..." + name.Substring(name.Length - tailLen);
        }
        int head = allowedNameLen / 2;
        int tail = allowedNameLen - head;
        if (nameOnly.Length <= head + tail) return name;
        string headPart = nameOnly.Substring(0, head);
        string tailPart = nameOnly.Substring(nameOnly.Length - tail);
        return headPart + "..." + tailPart + ext;
    }

    private static readonly string _previewLogPath = Path.Combine(Path.GetTempPath(), "krr_preview.log");
    private static void AppendPreviewLog(string msg)
    {
#if DEBUG
        string line = DateTime.Now.ToString("s") + " " + msg + Environment.NewLine;
        File.AppendAllText(_previewLogPath, line, Encoding.UTF8);
#endif
    }
}
