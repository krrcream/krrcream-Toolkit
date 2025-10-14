using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Localization;
using krrTools.UI;
using Border = Wpf.Ui.Controls.Border;
using Button = Wpf.Ui.Controls.Button;

namespace krrTools.Utilities
{
    public sealed class FileDropZone : Border
    {
        private Button? StartConversionButton;
        private FileDropZoneViewModel _viewModel;
        public FileDropZoneViewModel ViewModel => _viewModel;

        // 本地化字符串对象
        private readonly DynamicLocalizedString _startButtonTextLocalized = new(Strings.StartButtonText);

        public FileDropZone()
        {
            var fileDispatcher = new FileDispatcher();
            _viewModel = new FileDropZoneViewModel(fileDispatcher);
            Injector.InjectServices(_viewModel); // 注入依赖项
            DataContext = _viewModel;
            InitializeUI();
        }

        public FileDropZone(FileDispatcher fileDispatcher)
        {
            _viewModel = new FileDropZoneViewModel(fileDispatcher);
            Injector.InjectServices(_viewModel); // 注入依赖项
            DataContext = _viewModel;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromArgb(160, 245, 248, 255));
            // BorderBrush 将通过绑定设置
            BorderThickness = new Thickness(2);
            CornerRadius = new CornerRadius(6);
            Margin = new Thickness(8, 2, 8, 5);
            Padding = new Thickness(12);
            Height = 80; // 增加高度以容纳进度条

            // 绑定 BorderBrush 到 ProgressBrush
            var borderBrushBinding = new Binding("ProgressBrush") { Source = _viewModel };
            SetBinding(BorderBrushProperty, borderBrushBinding);

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 提示文本
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮和进度条

            var dropHint = new TextBlock
            {
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xB5)),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            dropHint.SetBinding(TextBlock.TextProperty, new Binding("DisplayText"));

            // 按钮容器
            var bottomPanel = new Grid();
            bottomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 按钮

            StartConversionButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 6, 8, 6),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(8, 0, 6, 0),
                MinWidth = 92
            };
            StartConversionButton.SetBinding(ContentControl.ContentProperty, new Binding("Value") { Source = _startButtonTextLocalized });
            StartConversionButton.SetBinding(VisibilityProperty, new Binding("IsConversionEnabled") { Converter = new BooleanToVisibilityConverter() });

            Grid.SetColumn(StartConversionButton, 0);
            bottomPanel.Children.Add(StartConversionButton);

            Grid.SetRow(dropHint, 0);
            Grid.SetRow(bottomPanel, 1);
            mainGrid.Children.Add(dropHint);
            mainGrid.Children.Add(bottomPanel);

            Child = mainGrid;

            Drop += OnDrop;
            StartConversionButton.Click += StartConversionButton_Click;
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += FileDropZone_Unloaded;
        }

        /// <summary>
        /// 构造函数 - 使用指定的 ViewModel（主要用于测试）
        /// </summary>
        public FileDropZone(FileDropZoneViewModel viewModel) : base()
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        /// <summary>
        /// 设置 ViewModel（主要用于测试）
        /// </summary>
        public void SetViewModel(FileDropZoneViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        private void OnLanguageChanged()
        {
            // ViewModel handles localization
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            var osuFiles = _viewModel.CollectOsuFiles(files);
            if (osuFiles.Count == 0) return;

            _viewModel.SetFiles(osuFiles.ToArray(), source: FileSource.Dropped);
        }

        private void StartConversionButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConvertFiles();
        }

        private void FileDropZone_Unloaded(object sender, RoutedEventArgs e)
        {
            SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }
    }
}
