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

        // 专门用于测试的构造函数，不进行依赖注入
        public FileDropZone(FileDispatcher fileDispatcher, bool skipInjection)
        {
            _viewModel = new FileDropZoneViewModel(fileDispatcher);
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
            AllowDrop = true; // 启用文件拖放

            // 绑定 BorderBrush 到 ProgressBrush
            var borderBrushBinding = new Binding("ProgressBrush") { Source = _viewModel };
            SetBinding(BorderBrushProperty, borderBrushBinding);

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 文本居中
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 按钮右侧

            var dropHint = new TextBlock
            {
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xB5)),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            dropHint.SetBinding(TextBlock.TextProperty, new Binding("DisplayText"));

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

            Grid.SetColumn(dropHint, 0);
            Grid.SetColumn(StartConversionButton, 1);
            mainGrid.Children.Add(dropHint);
            mainGrid.Children.Add(StartConversionButton);

            Child = mainGrid;

            Drop += OnDrop;
            DragOver += OnDragOver;
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

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) 
                ? DragDropEffects.Copy 
                : DragDropEffects.None;
            
            e.Handled = true;
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
