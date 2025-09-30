
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using krrTools.Localization;
using static krrTools.UI.SharedUIComponents;
using Button = Wpf.Ui.Controls.Button;
using Image = System.Windows.Controls.Image;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace krrTools.Tools.Preview;
public class DualPreviewControl : UserControl
{
    private const string OriginalHintBase = "原始预览 (Original)";
    private const string ConvertedHintBase = "结果预览 (Converted)";
    private const string PreviewTitleBase = "预览 / Preview";
    private const string DropHintCnBase = "将 .osu 文件拖到此区域";
    private const string DropHintEnBase = "Drag & Drop .osu files in here";
    private const string StartButtonTextCn = "开始转换";
    private const string StartButtonTextEn = "Start";

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
    private TextBlock StartTimeDisplay;

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
        if (Processor is PreviewProcessor baseProc && ColumnOverride != null)
            baseProc.ColumnOverride = (int)ColumnOverride;
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
        if (_lastPaths is not { Length: > 0 } || Processor == null) return;

        UpdatePreviewTitleFromPaths(_lastPaths);
        SetBackgroundImage(_lastPaths[0]);
        ApplyColumnOverrideToProcessor();

        try
        {
            BuildAndSetVisuals();
        }
        catch (Exception ex)
        {
            AppendPreviewLog("Refresh failed: " + ex.Message);
        }
    }

    private void SetBackgroundImage(string path)
    {
        string? bgPath = PreviewTransformation.GetBackgroundImagePath(path);
        if (bgPath != null && File.Exists(bgPath))
        {
            var bgBitmap = new BitmapImage();
            bgBitmap.BeginInit();
            bgBitmap.UriSource = new Uri(bgPath);
            bgBitmap.CacheOption = BitmapCacheOption.OnLoad;
            bgBitmap.EndInit();
            if (_sharedBgImage != null)
            {
                _sharedBgImage.Source = bgBitmap;
                _sharedBgImage.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void BuildAndSetVisuals()
    {
        var originalVisual = Processor.BuildVisual(_lastPaths, false);
        var convertedVisual = Processor.BuildVisual(_lastPaths, true);

        if (originalVisual is { } ofe) EnsureStretch(ofe);
        if (convertedVisual is { } cfe) EnsureStretch(cfe);

        OriginalContent.Content = originalVisual;
        ConvertedContent.Content = convertedVisual;
        OriginalContent.Visibility = Visibility.Visible;
        ConvertedContent.Visibility = Visibility.Visible;

        UpdateHints();
    }

    private void UpdateHints()
    {
        OriginalHint.Text = OriginalHintBase;
        ConvertedHint.Text = ConvertedHintBase;
        string startMsText = (Processor is PreviewProcessor bp && bp.LastStartMs != 0) ? $"start {bp.LastStartMs} ms" : string.Empty;
        StartTimeDisplay.Text = startMsText;
    }

    public DualPreviewControl()
    {
        AllowDrop = true;
        InitializeUI();
        InitializeEvents();
    }

    private void InitializeUI()
    {
        var rootBorder = CreateRootBorder();
        var grid = CreateMainGrid();

        PreviewTitle = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, Text = PreviewTitleBase };
        Grid.SetRow(PreviewTitle, 0);
        grid.Children.Add(PreviewTitle);

        _sharedBgImage = CreateBackgroundImage();
        Grid.SetRow(_sharedBgImage, 1);
        Grid.SetRowSpan(_sharedBgImage, 3);
        Panel.SetZIndex(_sharedBgImage, -1);
        grid.Children.Add(_sharedBgImage);

        OriginalBorder = CreateOriginalBorder();
        Grid.SetRow(OriginalBorder, 1);
        grid.Children.Add(OriginalBorder);

        var centerArrow = CreateCenterArrow();
        Grid.SetRow(centerArrow, 2);
        grid.Children.Add(centerArrow);

        ConvertedBorder = CreateConvertedBorder();
        Grid.SetRow(ConvertedBorder, 3);
        grid.Children.Add(ConvertedBorder);

        DropZone = CreateDropZone();
        Grid.SetRow(DropZone, 4);
        grid.Children.Add(DropZone);

        rootBorder.Child = grid;
        Content = rootBorder;
    }

    private Border CreateRootBorder() => new()
    {
        Background = Brushes.Transparent,
        BorderBrush = PanelBorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = PanelCornerRadius,
        Padding = new Thickness(12)
    };

    private Grid CreateMainGrid()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        return grid;
    }

    private Image CreateBackgroundImage() => new()
    {
        Stretch = Stretch.UniformToFill,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Opacity = PreviewBackgroundOpacity,
        Effect = new BlurEffect { Radius = PreviewBackgroundBlurRadius },
        Visibility = Visibility.Collapsed
    };

    private Border CreateOriginalBorder()
    {
        var border = CreateBorder();
        border.Drop += OnDrop;

        var grid = CreateContentGrid();
        OriginalHint = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51)),
            Margin = new Thickness(2, 0, 2, 4),
            Text = OriginalHintBase
        };
        Grid.SetRow(OriginalHint, 0);
        grid.Children.Add(OriginalHint);

        OriginalContent = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        Grid.SetRow(OriginalContent, 1);
        grid.Children.Add(OriginalContent);

        border.Child = grid;
        return border;
    }

    private Border CreateConvertedBorder()
    {
        var border = CreateBorder();
        border.Drop += OnDrop;

        var grid = CreateContentGrid();
        ConvertedHint = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51)),
            Margin = new Thickness(2, 0, 2, 4),
            Text = ConvertedHintBase
        };
        Grid.SetRow(ConvertedHint, 0);
        grid.Children.Add(ConvertedHint);

        ConvertedContent = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        Grid.SetRow(ConvertedContent, 1);
        grid.Children.Add(ConvertedContent);

        border.Child = grid;
        return border;
    }

    private Border CreateBorder() => new()
    {
        AllowDrop = true,
        Background = Brushes.Transparent,
        BorderBrush = PanelBorderBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = PanelCornerRadius,
        Margin = new Thickness(0, 4, 0, 4),
        Padding = new Thickness(6),
        ClipToBounds = true
    };

    private Grid CreateContentGrid()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        return grid;
    }

    private StackPanel CreateCenterArrow()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Orientation = Orientation.Horizontal };
        StartTimeDisplay = new TextBlock
        {
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 90, 99, 112)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 10, 0),
            Text = ""
        };
        stack.Children.Add(StartTimeDisplay);
        stack.Children.Add(new TextBlock
        {
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 90, 99, 112)),
            Text = "↓ ↓"
        });
        return stack;
    }

    private Border CreateDropZone()
    {
        var border = new Border
        {
            AllowDrop = true,
            Background = new SolidColorBrush(Color.FromArgb(255, 245, 248, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 175, 200, 255)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(12)
        };
        border.Drop += OnDrop;

        var grid = new Grid();
        var centerTexts = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        DropHintCn = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xB5)),
            Text = DropHintCnBase,
            TextAlignment = TextAlignment.Center
        };
        DropHintEn = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6A, 0x7A, 0x90)),
            Text = DropHintEnBase,
            TextAlignment = TextAlignment.Center
        };
        centerTexts.Children.Add(DropHintCn);
        centerTexts.Children.Add(DropHintEn);
        grid.Children.Add(centerTexts);

        StartConversionButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Content = StartButtonTextCn,
            Padding = new Thickness(8, 6, 8, 6),
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(8, 0, 6, 0),
            MinWidth = 92
        };
        StartConversionButton.Click += StartConversionButton_Click;
        grid.Children.Add(StartConversionButton);

        border.Child = grid;
        return border;
    }

    private void InitializeEvents()
    {
        Loaded += DualPreviewControl_Loaded;
        Unloaded += DualPreviewControl_Unloaded;
        DataContextChanged += DualPreviewControl_DataContextChanged;
        SharedPathsChanged += OnSharedPathToPreviewChanged;
        StagedPathsChanged += OnSharedStagedPathsChanged;
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
        PreviewTitle.Text = LocalizationManager.IsChineseLanguage() ? "预览" : "Preview";
        UpdatePreviewTitleFromPaths(_lastPaths);

        OriginalHint.Text = LocalizationManager.IsChineseLanguage() ? "原始预览" : "Original";
        ConvertedHint.Text = LocalizationManager.IsChineseLanguage() ? "结果预览" : "Converted";

        UpdateDropZoneTexts();
    }

    private void UpdateDropZoneTexts()
    {
        string buttonText = LocalizationManager.IsChineseLanguage() ? StartButtonTextCn : StartButtonTextEn;
        if (_stagedPaths == null || _stagedPaths.Length == 0)
        {
            DropHintEn.Text = DropHintEnBase;
            DropHintCn.Text = DropHintCnBase;
        }
        else
        {
            DropHintEn.Text = $"{_stagedPaths.Length} file(s) staged. Click Start to convert.";
            DropHintCn.Text = $"已暂存 {_stagedPaths.Length} 个文件，点击开始转换。";
        }
        StartConversionButton.Content = buttonText;
    }

    private void OnSharedPathToPreviewChanged(object? sender, string[]? paths)
    {
        if (paths == null || (_lastPaths != null && paths.SequenceEqual(_lastPaths))) return;
        LoadPreview(paths, suppressBroadcast: true);
    }

    private void OnSharedStagedPathsChanged(object? sender, string[]? paths)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => OnSharedStagedPathsChanged(sender, paths)));
            return;
        }

        if (paths == null || paths.Length == 0)
        {
            ResetDropZone();
            return;
        }

        if (_stagedPaths != null && paths.SequenceEqual(_stagedPaths)) return;

        _stagedPaths = paths.ToArray();
        UpdateDropZoneForStagedFiles();
    }

    private void ResetDropZone()
    {
        _stagedPaths = null;
        StartConversionButton.Visibility = Visibility.Collapsed;
        StartConversionButton.IsHitTestVisible = false;
        Panel.SetZIndex(StartConversionButton, 0);

        DropHintEn.Text = DropHintEnBase;
        DropHintCn.Text = DropHintCnBase;

        DropZone.InvalidateMeasure();
        DropZone.UpdateLayout();
        InvalidateMeasure();
        UpdateLayout();
    }

    private void UpdateDropZoneForStagedFiles()
    {
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
        if (_autoLoadedSample || Processor == null || OriginalContent.Content != null) return;

        string samplePath = FindSampleFile();
        if (!string.IsNullOrEmpty(samplePath))
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

    private static void OnProcessorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DualPreviewControl ctrl)
        {
            ctrl._autoLoadedSample = false;
            ctrl.TryAutoLoadSample();
            ctrl.Refresh();
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

        _lastPaths = osu;

        if (!suppressBroadcast)
        {
            SharedPathsChanged?.Invoke(this, osu);
        }

        UpdatePreviewTitleFromPaths(_lastPaths);
        ApplyColumnOverrideToProcessor();

        if (Processor == null)
        {
            SetNoProcessorState();
            return;
        }

        try
        {
            BuildAndSetVisuals();
        }
        catch (Exception ex)
        {
            SetErrorState(ex.Message);
        }
    }

    private void SetNoProcessorState()
    {
        OriginalContent.Content = new TextBlock { Text = "No processor set", Foreground = Brushes.DarkRed };
        OriginalContent.Visibility = Visibility.Visible;
        ConvertedContent.Content = null;
        ConvertedContent.Visibility = Visibility.Collapsed;
        OriginalHint.Text = OriginalHintBase;
    }

    private void SetErrorState(string message)
    {
        OriginalContent.Content = new TextBlock
        {
            Text = "Preview error: " + message,
            Foreground = Brushes.DarkRed,
            TextWrapping = TextWrapping.Wrap
        };
        OriginalContent.Visibility = Visibility.Visible;
        OriginalHint.Text = OriginalHintBase;
        AppendPreviewLog("Preview build failed: " + message);
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

        var osuFiles = CollectOsuFiles(files);
        if (osuFiles.Count == 0) return;

        LoadPreview(osuFiles.ToArray());
        BroadcastStagedPaths(osuFiles.ToArray());
        OnSharedStagedPathsChanged(this, osuFiles.ToArray());
    }

    private List<string> CollectOsuFiles(string[] items)
    {
        var osuFiles = new List<string>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item)) continue;
            if (File.Exists(item) && Path.GetExtension(item).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            {
                osuFiles.Add(item);
            }
            else if (Directory.Exists(item))
            {
                try
                {
                    var found = Directory.GetFiles(item, "*.osu", SearchOption.AllDirectories);
                    osuFiles.AddRange(found);
                }
                catch (Exception ex)
                {
                    AppendPreviewLog($"Directory enumerate failed for '{item}': {ex.Message}");
                }
            }
        }
        return osuFiles;
    }

    public void StageFiles(string[]? osuFiles)
    {
        if (osuFiles == null || osuFiles.Length == 0) return;
        BroadcastStagedPaths(osuFiles.ToArray());
        OnSharedStagedPathsChanged(this, osuFiles.ToArray());
    }

    public void ApplyDropZoneStagedUI(string[]? osuFiles)
    {
        if (osuFiles == null || osuFiles.Length == 0) return;
        _stagedPaths = osuFiles.ToArray();
        _sharedStagedPaths = _stagedPaths;
        UpdateDropZoneForStagedFiles();
    }

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

    public static string[]? GetSharedStagedPaths() => _sharedStagedPaths?.ToArray();

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
        Refresh();
    }

    private void UpdatePreviewTitleFromPaths(string[]? paths)
    {
        if (paths == null || paths.Length == 0)
        {
            PreviewTitle.Text = PreviewTitleBase;
            return;
        }

        string name = Path.GetFileName(paths[0]);
        if (paths.Length > 1) name += $" (+{paths.Length - 1} more)";

        string truncated = TruncateFileNameMiddle(name, 40);
        PreviewTitle.Text = PreviewTitleBase + " : " + truncated;
        PreviewTitle.ToolTip = paths.Length == 1 ? paths[0] : string.Join("\n", paths);
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

    private void StartConversionButton_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"Start conversion clicked, staged paths: {_stagedPaths?.Length ?? 0}");
        StartConversionRequested?.Invoke(this, _stagedPaths);
    }

    private static readonly string _previewLogPath = Path.Combine(Path.GetTempPath(), "krr_preview.log");

    private static void AppendPreviewLog(string msg)
    {
#if DEBUG
        string line = DateTime.Now.ToString("s") + " " + msg + Environment.NewLine;
        File.AppendAllText(_previewLogPath, line, Encoding.UTF8);
#endif
    }

    public string? CurrentTool { get; set; }
}
