using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using krrTools.Bindable;
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

        private ScrollViewer? _originalScrollViewer;
        private ScrollViewer? _convertedScrollViewer;
        private bool _isSyncingScroll;
        private DateTime _lastScrollSync = DateTime.MinValue;
        private const int ScrollSyncThrottleMs = 16;

        public PreviewViewDual(PreviewViewModel viewModel)
        {
            ViewModel = viewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            InitializeUI();
        }
        
        public void SetCurrentTool(ConverterEnum? tool)
        {
            _currentTool = tool;
            ViewModel?.SetCurrentTool(tool);
        }
        
        private DateTime _lastSettingsChange = DateTime.MinValue;
        private const int SettingsChangeThrottleMs = 50;


        private Bindable<string> BGPath { get; } = new(string.Empty);
        private Bindable<bool> IsProcessing { get; } = new();

        public PreviewViewModel? ViewModel { get; set; }

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
            if (IsProcessing.Value || changedConverter != _currentTool) 
                return;
            
            if ((DateTime.UtcNow - _lastSettingsChange).TotalMilliseconds < SettingsChangeThrottleMs) return;
            
            _lastSettingsChange = DateTime.UtcNow;
            
            ViewModel?.TriggerRefresh();
        }

        public void LoadPreview(string input)
        {
            ViewModel?.LoadPreviewPath(input);
        }
        
        private void OnLanguageChanged()
        {
            _originalHint.Text = Strings.OriginalHint.Localize();
            _convertedHint.Text = Strings.ConvertedHint.Localize();
        }

        private void TryAutoLoadSample()
        {
            // if (ViewModel?.OriginalVisual != null) return;
            ViewModel?.ResetPreview();
        }

        // 加载谱面背景图的方法，统一在项目中使用
        public void LoadBackgroundBrush(string path)
        {
            if (BGPath.Value == path) return;

            BGPath.Value = path;
            // 确保在UI线程执行
            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                LoadBackgroundBrushInternal();
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(LoadBackgroundBrushInternal);
            }
        }

        private void LoadBackgroundBrushInternal()
        {
            try
            {
                var bgBitmap = new BitmapImage();
                bgBitmap.BeginInit();
                bgBitmap.UriSource = new Uri(BGPath.Value, UriKind.Absolute);
                bgBitmap.CacheOption = BitmapCacheOption.OnLoad;
                bgBitmap.EndInit();
                Background = new ImageBrush
                {
                    ImageSource = bgBitmap,
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0.25
                };
                Console.WriteLine("[PreviewViewDual] Loaded BG from " + BGPath.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PreviewViewDual] Failed to load background image from {0}: {1}",
                    BGPath.Value, ex.Message);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
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
                    _originalScrollViewer = FindScrollViewer(ViewModel.OriginalVisual);
                    if (_originalScrollViewer != null)
                    {
                        _originalScrollViewer.ScrollChanged += OnOriginalScrollChanged;
                    }
                    break;
                case nameof(ViewModel.ConvertedVisual):
                    _convertedContent.Content = ViewModel.ConvertedVisual;
                    _convertedScrollViewer = FindScrollViewer(ViewModel.ConvertedVisual);
                    if (_convertedScrollViewer != null)
                    {
                        _convertedScrollViewer.ScrollChanged += OnConvertedScrollChanged;
                    }
                    break;
            }
        }

        private ScrollViewer? FindScrollViewer(DependencyObject? element)
        {
            if (element == null) return null;
            if (element is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var found = FindScrollViewer(child);
                if (found != null) return found;
            }
            return null;
        }

        private void OnOriginalScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll) return;
            _isSyncingScroll = true;
            if ((DateTime.UtcNow - _lastScrollSync).TotalMilliseconds >= ScrollSyncThrottleMs)
            {
                _lastScrollSync = DateTime.UtcNow;
                if (_convertedScrollViewer != null)
                {
                    _convertedScrollViewer.ScrollToVerticalOffset(_originalScrollViewer!.VerticalOffset);
                    _convertedScrollViewer.ScrollToHorizontalOffset(_originalScrollViewer!.HorizontalOffset);
                }
            }
            _isSyncingScroll = false;
        }

        private void OnConvertedScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll) return;
            _isSyncingScroll = true;
            if ((DateTime.UtcNow - _lastScrollSync).TotalMilliseconds >= ScrollSyncThrottleMs)
            {
                _lastScrollSync = DateTime.UtcNow;
                if (_originalScrollViewer != null)
                {
                    _originalScrollViewer.ScrollToVerticalOffset(_convertedScrollViewer!.VerticalOffset);
                    _originalScrollViewer.ScrollToHorizontalOffset(_convertedScrollViewer!.HorizontalOffset);
                }
            }
            _isSyncingScroll = false;
        }
    }
}