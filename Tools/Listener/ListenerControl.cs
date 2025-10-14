using System;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using krrTools.Beatmaps;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using krrTools.UI;
using krrTools.Utilities;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace krrTools.Tools.Listener
{
    public partial class ListenerControl
    {
        private ConversionHotkeyManager? _conversionHotkeyManager;
        private readonly BeatmapTransformationService _transformationService;

        public RelayCommand BrowseCommand { get; }

        private readonly ListenerViewModel _viewModel;
        public ListenerViewModel ViewModel => _viewModel;

        private readonly FileDropZoneViewModel _dropZoneViewModel;
        public FileDropZoneViewModel DropZoneViewModel => _dropZoneViewModel;

        internal ListenerControl()
        {
            // 自动注入标记了 [Inject] 的属性
            this.InjectServices();

            InitializeComponent();
            _viewModel = new ListenerViewModel();
            DataContext = _viewModel;

            // 初始化转换服务
            var moduleManager = App.Services.GetService(typeof(IModuleManager)) as IModuleManager;
            _transformationService = new BeatmapTransformationService(moduleManager!);

            // 初始化拖拽区 ViewModel
            var fileDispatcher = new FileDispatcher();
            _dropZoneViewModel = new FileDropZoneViewModel(fileDispatcher);
            
            BrowseCommand = new RelayCommand(() => _viewModel.SetSongsPathWindow());

            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;

            _viewModel.WindowTitle = Strings.OSUListener.Localize();

            Loaded += (_, _) =>
            {
                InitializeConversionHotkeys();
            };
            Unloaded += Window_Closing;
        }

        private void Window_Closing(object? sender, RoutedEventArgs e)
        {
            _conversionHotkeyManager?.Dispose();
        }

        private void OnLanguageChanged()
        {
            Dispatcher.BeginInvoke(new Action(() => { _viewModel.WindowTitle = Strings.OSUListener; }));
        }

        private void InitializeConversionHotkeys()
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;

            _conversionHotkeyManager = new ConversionHotkeyManager(
                ExecuteConvertWithModule,
                mainWindow
            );

            // 注册快捷键
            var globalSettings = BaseOptionsManager.GetGlobalSettings();
            _conversionHotkeyManager.RegisterHotkeys(globalSettings);
        }

        private void ExecuteConvertWithModule(ConverterEnum converter)
        {
            // 获取当前谱面
            string beatmapPath = _viewModel.MonitorOsuFilePath;

            try
            {
                // 解码谱面
                var beatmap = OsuParsers.Decoders.BeatmapDecoder.Decode(beatmapPath);

                // 使用转换服务
                var transformedBeatmap = _transformationService.TransformBeatmap(beatmap, converter);

                // 保存转换后谱面
                var outputPath = transformedBeatmap!.GetOutputOsuFileName();
                var outputDir = Path.GetDirectoryName(beatmapPath);
                var fullOutputPath = Path.Combine(outputDir!, outputPath);
                transformedBeatmap!.Save(fullOutputPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Conversion failed: {ex.Message}", Strings.CannotConvert.Localize(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}