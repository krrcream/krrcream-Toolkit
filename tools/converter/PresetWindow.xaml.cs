using System.Windows;

namespace krrTools.Tools.Converter
{
    public partial class PresetWindow : Window
    {
        private ConverterViewModel _viewModel;
        private ConverterWindow _converterWindow;

        public PresetWindow(ConverterViewModel viewModel, ConverterWindow converterWindow)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _converterWindow = converterWindow;
        }

        private void DefaultPresetButton_Click(object sender, RoutedEventArgs e)
        {
            // 更新 ViewModel 中的值
            _viewModel.TargetKeys = 10;
            _viewModel.MaxKeys = 10;
            _viewModel.MinKeys = 2;
            _viewModel.TransformSpeed = 4;
            _viewModel.Seed = 114514;
            this.Close();
        }

        // 其他预设按钮类似处理
        private void TenKeyPresetButton_Click(object sender, RoutedEventArgs e)
        {
            // 设置10K预设值
            _viewModel.TargetKeys = 10;
            _viewModel.MaxKeys = 8;
            _viewModel.MinKeys = 1;
            _viewModel.TransformSpeed = 5;
            _viewModel.Seed = 0;
            this.Close();
        }

        private void EightKeyPresetButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TargetKeys = 8;
            _viewModel.MaxKeys = 7;
            _viewModel.MinKeys = 3;
            _viewModel.TransformSpeed = 4;
            _viewModel.Seed = 0;
            this.Close();
        }

        private void SevenKeyPresetButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TargetKeys = 7;
            _viewModel.MaxKeys = 5;
            _viewModel.MinKeys = 3;
            _viewModel.TransformSpeed = 4;
            _viewModel.Seed = 0;
            this.Close();
        }
    }

    public class PresetData
    {
        public double TargetKeys { get; set; }
        public double MaxKeys { get; set; }
        public double MinKeys { get; set; }
        public double TransformSpeed { get; set; }
        public int? Seed { get; set; }
    }
}
