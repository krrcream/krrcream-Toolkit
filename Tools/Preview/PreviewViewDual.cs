using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using krrTools.Beatmaps;
using krrTools.Localization;
using krrTools.Utilities;
using krrTools.Core;
using OsuParsers.Beatmaps;
using static krrTools.UI.SharedUIComponents;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace krrTools.Tools.Preview;

public class PreviewViewDual : Wpf.Ui.Controls.Grid
{
    // UI 相关常量
    private static readonly Thickness DefaultBorderThickness = new(1);
    private static readonly CornerRadius DefaultCornerRadius = PanelCornerRadius;
    private static readonly Thickness BorderMargin = new(0, 4, 0, 4);
    private static readonly Thickness BorderPadding = new(6);

    // 常量
    private const int MaxFileNameLength = 80; // 显示在标题中的文件名最大长度
    private const int RefreshThrottleMs = 150;

    // 字段只声明
    private readonly TextBlock _previewTitle;
    private readonly TextBlock _originalHint;
    private readonly TextBlock _convertedHint;
    private readonly ContentControl _originalContent;
    private readonly ContentControl _convertedContent;
    private TextBlock _startTimeDisplay = null!;

    private bool _autoLoadedSample;
    private INotifyPropertyChanged? _observedDc;
    private DateTime _lastRefresh = DateTime.MinValue;
    private Beatmap? _originalBeatmap;
    public IModuleManager? ModuleScheduler { get; set; }

    #region 回调属性刷新预览

    // public static readonly DependencyProperty ColumnOverrideProperty = DependencyProperty.Register(
    //     nameof(ColumnOverride), typeof(int?), typeof(PreviewViewDual),
    //     new PropertyMetadata(null, OnAnyPropertyChanged));

    // public int? ColumnOverride
    // {
    //     get => (int?)GetValue(ColumnOverrideProperty);
    //     set => SetValue(ColumnOverrideProperty, value);
    // }

    public static readonly DependencyProperty AutoRefreshTokenProperty = DependencyProperty.Register(
        nameof(AutoRefreshToken), typeof(object), typeof(PreviewViewDual),
        new PropertyMetadata(null, OnAnyPropertyChanged));

    public object? AutoRefreshToken
    {
        get => GetValue(AutoRefreshTokenProperty);
        set => SetValue(AutoRefreshTokenProperty, value);
    }

    public static readonly DependencyProperty ProcessorProperty = DependencyProperty.Register(
        nameof(Processor), typeof(IPreviewProcessor), typeof(PreviewViewDual),
        new PropertyMetadata(null, OnAnyPropertyChanged));

    public IPreviewProcessor? Processor
    {
        get => (IPreviewProcessor?)GetValue(ProcessorProperty);
        set => SetValue(ProcessorProperty, value);
    }

    // 通用属性变更回调
    private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PreviewViewDual ctrl)
        {
            if (e.Property == ProcessorProperty)
            {
                ctrl._autoLoadedSample = false;
                if (e.NewValue == null)
                {
                    // 当 Processor 设置为 null 时，清空预览内容，以便显示内置预览
                    ctrl._originalContent.Content = null;
                    ctrl._convertedContent.Content = null;
                }
                else
                {
                    if (ctrl.Processor is ConverterProcessor cp)
                        cp.ModuleScheduler = ctrl.ModuleScheduler;
                    if (ctrl._originalBeatmap != null)
                        ctrl.LoadConvertedPreview(ctrl._originalBeatmap);
                }
            }
            else
            {
                ctrl.TryAutoLoadSample();
                if (ctrl._originalBeatmap != null)
                    ctrl.LoadConvertedPreview(ctrl._originalBeatmap);
            }
        }
    }

    #endregion

    public void Refresh()
    {
        if (_originalBeatmap != null) LoadConvertedPreview(_originalBeatmap);
    }

    public void LoadPreview(Beatmap beatmap)
    {
        LoadOriginalPreview(beatmap);
        LoadConvertedPreview(beatmap);
    }

    public PreviewViewDual()
    {
        _previewTitle = new TextBlock
            { FontSize = 15, FontWeight = FontWeights.Bold, Text = Strings.PreviewTitle.Localize() };

        var originalBorder =
            CreatePreviewBorder(Strings.OriginalHint.Localize(), out _originalHint, out _originalContent);

        var centerStack = CreateCenterStack();

        var convertedBorder =
            CreatePreviewBorder(Strings.ConvertedHint.Localize(), out _convertedHint, out _convertedContent);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Children.Add(_previewTitle);
        Children.Add(originalBorder);
        Children.Add(centerStack);
        Children.Add(convertedBorder);
        SetRow(_previewTitle, 0);
        SetRow(originalBorder, 1);
        SetRow(centerStack, 2);
        SetRow(convertedBorder, 3);

        Loaded += DualPreviewControl_Loaded;
        Unloaded += DualPreviewControl_Unloaded;
        DataContextChanged += DualPreviewControl_DataContextChanged;
    }

    private Border CreatePreviewBorder(string hintText, out TextBlock hint, out ContentControl content)
    {
        hint = new TextBlock
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = PreviewConstants.UiHintTextBrush,
            Margin = new Thickness(2, 0, 2, 4),
            Text = hintText
        };
        content = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { hint, content }
        };
        SetRow(hint, 0);
        SetRow(content, 1);
        return new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = PanelBorderBrush,
            BorderThickness = DefaultBorderThickness,
            CornerRadius = DefaultCornerRadius,
            Margin = BorderMargin,
            Padding = BorderPadding,
            ClipToBounds = true,
            Child = grid
        };
    }

    private Grid CreateCenterStack()
    {
        _startTimeDisplay = new TextBlock
        {
            FontSize = 14,
            Foreground = PreviewConstants.UiSecondaryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 10, 0),
            Text = ""
        };
        var arrowBlock = new TextBlock
        {
            FontSize = 18,
            Foreground = PreviewConstants.UiSecondaryTextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "↓ ↓"
        };
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            Children = { _startTimeDisplay, arrowBlock }
        };
        SetColumn(_startTimeDisplay, 0);
        SetColumn(arrowBlock, 1);
        return grid;
    }

    private void DualPreviewControl_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged += OnLanguageChanged;
        TryAutoLoadSample();
        Visibility = Visibility.Visible;
    }

    private void DualPreviewControl_Unloaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        Visibility = Visibility.Collapsed;
    }

    private void OnLanguageChanged()
    {
        _originalHint.Text = Strings.OriginalHint.Localize();
        _convertedHint.Text = Strings.ConvertedHint.Localize();
    }

    private void TryAutoLoadSample()
    {
        if (_autoLoadedSample || _originalContent.Content != null) return;
        Processor ??= new ConverterProcessor();
        if (Processor is ConverterProcessor cp)
            cp.ModuleScheduler = ModuleScheduler;
        var beatmap = PreviewManiaNote.BuiltInSampleStream();
        LoadOriginalPreview(beatmap);
        _autoLoadedSample = true;
    }

    private void LoadOriginalPreview(Beatmap beatmap)
    {
        _originalBeatmap = beatmap;
        _previewTitle.Text = UpdateTitleSuffix(_originalBeatmap);

        if (Processor == null)
        {
            SetNoProcessorState();
            return;
        }

        if (_originalBeatmap == null || Processor == null) return;
        var originalVisual = Processor.BuildOriginalVisual(_originalBeatmap);
        _originalContent.Content = originalVisual;
        _originalContent.Visibility = Visibility.Visible;
    }

    private void LoadConvertedPreview(Beatmap beatmap)
    {
        _originalBeatmap = beatmap;
        _previewTitle.Text = UpdateTitleSuffix(_originalBeatmap);
        // ApplyColumnOverrideToProcessor();
        if (Processor == null)
        {
            SetNoProcessorState();
            return;
        }

        if (_originalBeatmap == null || Processor == null) return;
        var convertedVisual = Processor.BuildConvertedVisual(_originalBeatmap);
        _convertedContent.Content = convertedVisual;
        _convertedContent.Visibility = Visibility.Visible;
        
        var startMsText = Processor is ConverterProcessor bp && bp.StartMs != 0
            ? $"start {bp.StartMs} ms"
            : string.Empty;
        _startTimeDisplay.Text = startMsText;
    }
    
    private void SetNoProcessorState()
    {
        _originalContent.Content = new TextBlock
            { Text = Strings.NoProcessorSet.Localize(), Foreground = Brushes.DarkRed };
        _originalContent.Visibility = Visibility.Visible;
        _convertedContent.Content = null;
        _convertedContent.Visibility = Visibility.Collapsed;
    }

    // 监听DataContext变化以自动刷新
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
        if ((DateTime.UtcNow - _lastRefresh).TotalMilliseconds < RefreshThrottleMs) return;
        _lastRefresh = DateTime.UtcNow;
        Refresh();
    }

    #region 更新预览标题，预览文件名

    private string UpdateTitleSuffix(Beatmap? beatmap)
    {
        if (beatmap == null)
        {
            return string.Empty;
        }
        
        string titleSuffix;
        if (beatmap.MetadataSection.Title == "Built-in Sample")
        {
            titleSuffix = "Built-in Sample";
        }
        else
        {
            var name = beatmap.GetOutputOsuFileName(true);
            titleSuffix = TruncateFileNameMiddle(name, MaxFileNameLength);
        }

        return "DIFF: " + titleSuffix;
    }

    private static string TruncateFileNameMiddle(string name, int maxLen)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= maxLen) return name;
        var ext = Path.GetExtension(name);
        var nameOnly = name.Substring(0, name.Length - ext.Length);
        var keep = maxLen - ext.Length - 3;
        if (keep <= 0) return name.Substring(0, maxLen - 3) + "...";
        var head = keep / 2;
        var tail = keep - head;
        return nameOnly.Substring(0, head) + "..." + nameOnly.Substring(nameOnly.Length - tail) + ext;
    }

    #endregion

    // private void ApplyColumnOverrideToProcessor()
    // {
    //     if (Processor is ConverterProcessor baseProc && ColumnOverride != null)
    //         baseProc.ColumnOverride = (int)ColumnOverride;
    // }

    // 加载谱面背景图的方法，统一在项目中使用
    public void LoadBackgroundBrush(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || path == string.Empty)
            return;

        try
        {
            var bgBitmap = new BitmapImage();
            bgBitmap.BeginInit();
            bgBitmap.UriSource = new Uri(path);
            bgBitmap.CacheOption = BitmapCacheOption.OnLoad;
            bgBitmap.EndInit();
            Background = new ImageBrush
            {
                ImageSource = bgBitmap,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.25
            };
            Console.WriteLine("[PreviewViewDual] Loaded BG from " + path);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[PreviewViewDual] Failed to load background image from {0}: {1}",
                path, ex.Message);
        }
    }

    public void ResetToDefaultPreview()
    {
        _autoLoadedSample = false;
        _originalContent.Content = null;
        _convertedContent.Content = null;
        TryAutoLoadSample();
    }
}