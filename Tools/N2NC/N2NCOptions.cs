using System.Collections.Generic;
using krrTools.Configuration;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// 转换选项类，用于封装所有转换参数
    /// </summary>
    public class N2NCOptions : UnifiedToolOptions
    {
        [Option(LabelKey = nameof(KeysSliderLabel), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public double TargetKeys
        {
            get => _targetKeys;
            set => SetProperty(ref _targetKeys, value);
        }
        private double _targetKeys = 10;

        // 动态最大值将由ViewModel的约束管理处理
        [Option(LabelKey = nameof(N2NCMaxKeysTemplate), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public double MaxKeys
        {
            get => _maxKeys;
            set => SetProperty(ref _maxKeys, value);
        }
        private double _maxKeys = 10;

        [Option(LabelKey = nameof(N2NCMinKeysTemplate), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public double MinKeys
        {
            get => _minKeys;
            set => SetProperty(ref _minKeys, value);
        }
        private double _minKeys = 2;

        [Option(LabelKey = nameof(N2NCTransformSpeedTemplate), Min = 1, Max = 8, UIType = UIType.Slider, DataType = typeof(double))]
        public double TransformSpeed
        {
            get => _transformSpeed;
            set => SetProperty(ref _transformSpeed, value);
        }
        private double _transformSpeed = 4.0;

        [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox, DataType = typeof(int?))]
        public int? Seed
        {
            get => _seed;
            set => SetProperty(ref _seed, value);
        }
        private int? _seed = 114514;

        public List<int>? SelectedKeyTypes { get; set; }

        public KeySelectionFlags? SelectedKeyFlags { get; set; } = KeySelectionFlags.None;

        public new void Validate()
        {
        }
    }
}