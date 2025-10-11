using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using krrTools.Configuration;
using krrTools.Localization;
using Wpf.Ui.Controls;
using static krrTools.UI.SharedUIComponents;
using Border = System.Windows.Controls.Border;
using Button = Wpf.Ui.Controls.Button;
using Grid = Wpf.Ui.Controls.Grid;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace krrTools.Tools.Preview
{
    public class PreviewViewDual : Grid
    {
        // UI 相关常量
        private static readonly Thickness DefaultBorderThickness = new(1);
        private static readonly CornerRadius DefaultCornerRadius = PanelCornerRadius;
        private static readonly Thickness BorderMargin = new(0, 4, 0, 4);
        private static readonly Thickness BorderPadding = new(6);

        // 字段只声明
        private TextBlock _previewTitle = null!;
        private TextBlock _originalHint = null!;
        private TextBlock _convertedHint = null!;
        private ContentControl _originalContent = null!;
        private ContentControl _convertedContent = null!;
        private TextBlock _startTimeDisplay = null!;
        private ConverterEnum? _currentTool;

        public void SetCurrentTool(ConverterEnum? tool)
        {
            _currentTool = tool;
            ViewModel?.SetCurrentTool(tool);
        }

        public void SetCurrentViewModel(object? viewModel)
        {
            ViewModel?.SetCurrentViewModel(viewModel);
        }
        private DateTime _lastSettingsChange = DateTime.MinValue;
        private const int SettingsChangeThrottleMs = 50;

        private bool _autoLoadedSample;
        private string? _currentBackgroundPath;
        private bool _isProcessing;

        public PreviewViewModel? ViewModel { get; set; }

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
            new PropertyMetadata(null)); // 移除回调

        public object? AutoRefreshToken
        {
            get => GetValue(AutoRefreshTokenProperty);
            set => SetValue(AutoRefreshTokenProperty, value);
        }

        #endregion

        public void Refresh()
        {
            ViewModel?.TriggerRefresh();
        }

        public void LoadPreview(string input)
        {
            ViewModel?.LoadFromPath(input);
        }

        public PreviewViewDual(PreviewViewModel viewModel)
        {
            ViewModel = viewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            InitializeUI();
        }

        private void InitializeUI()
        {
            _previewTitle = new TextBlock
                { FontSize = 16, FontWeight = FontWeights.Bold, Text = Strings.PreviewTitle.Localize() };

            // 创建重置按钮
            var resetButton = new Button
            {
                Content = new SymbolIcon { Symbol = SymbolRegular.ArrowReset32 },
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 14
            };
            resetButton.Click += ResetButton_Click;

            // 创建标题网格
            var titleGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Children = { _previewTitle, resetButton }
            };
            SetColumn(_previewTitle, 0);
            SetColumn(resetButton, 1);

            var originalBorder =
                CreatePreviewBorder(Strings.OriginalHint.Localize(), out _originalHint, out _originalContent);

            var centerStack = CreateCenterStack();

            var convertedBorder =
                CreatePreviewBorder(Strings.ConvertedHint.Localize(), out _convertedHint, out _convertedContent);

            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Children.Add(titleGrid);
            Children.Add(originalBorder);
            Children.Add(centerStack);
            Children.Add(convertedBorder);
            SetRow(titleGrid, 0);
            SetRow(originalBorder, 1);
            SetRow(centerStack, 2);
            SetRow(convertedBorder, 3);

            Loaded += DualPreviewControl_Loaded;
            Unloaded += DualPreviewControl_Unloaded;
            // DataContextChanged += DualPreviewControl_DataContextChanged;
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
                Visibility = Visibility.Visible
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
            BaseOptionsManager.SettingsChanged += OnSettingsChanged;
            TryAutoLoadSample();
            Visibility = Visibility.Visible;
        }

        private void DualPreviewControl_Unloaded(object? sender, RoutedEventArgs e)
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
            BaseOptionsManager.SettingsChanged -= OnSettingsChanged;
            Visibility = Visibility.Collapsed;
        }

        private void OnSettingsChanged(ConverterEnum changedConverter)
        {
            if ((DateTime.UtcNow - _lastSettingsChange).TotalMilliseconds < SettingsChangeThrottleMs) return;
            _lastSettingsChange = DateTime.UtcNow;

            if (_isProcessing || changedConverter != _currentTool) return;
            _isProcessing = true;
            try
            {
                ViewModel?.TriggerRefresh();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void OnLanguageChanged()
        {
            _originalHint.Text = Strings.OriginalHint.Localize();
            _convertedHint.Text = Strings.ConvertedHint.Localize();
        }

        private void TryAutoLoadSample()
        {
            if (_autoLoadedSample || ViewModel?.OriginalVisual != null) return;
            ViewModel?.LoadBuiltInSample();
            _autoLoadedSample = true;
        }

        // 加载谱面背景图的方法，统一在项目中使用
        public void LoadBackgroundBrush(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("[LoadBackgroundBrush] Empty path provided");
                return;
            }

            // 检查是否已经加载了相同的背景，避免重复加载
            if (_currentBackgroundPath == path) return;

            if (!File.Exists(path))
            {
                Console.WriteLine("[LoadBackgroundBrush] No Find:" + path);
                return;
            }

            try
            {
                var bgBitmap = new BitmapImage();
                bgBitmap.BeginInit();
                bgBitmap.UriSource = new Uri(path, UriKind.Absolute);
                bgBitmap.CacheOption = BitmapCacheOption.OnLoad;
                bgBitmap.EndInit();
                Background = new ImageBrush
                {
                    ImageSource = bgBitmap,
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0.25
                };
                _currentBackgroundPath = path;
                Console.WriteLine("[PreviewViewDual] Loaded BG from " + path);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PreviewViewDual] Failed to load background image from {0}: {1}",
                    path, ex.Message);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.Reset();
        }

        public void ResetPreview()
        {
            ViewModel?.Reset();
            _autoLoadedSample = false; // 允许重新加载内置样本
            TryAutoLoadSample();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ViewModel == null) return;

            switch (e.PropertyName)
            {
                case nameof(ViewModel.Title):
                    _previewTitle.Text = ViewModel.Title;
                    break;
                case nameof(ViewModel.OriginalVisual):
                    _originalContent.Content = ViewModel.OriginalVisual;
                    break;
                case nameof(ViewModel.ConvertedVisual):
                    _convertedContent.Content = ViewModel.ConvertedVisual;
                    break;
            }
        }
    }
}