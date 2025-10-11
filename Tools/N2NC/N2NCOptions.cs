using System;
using System.Collections.Generic;
using krrTools.Configuration;
using krrTools.Bindable;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// 转换选项类，用于封装所有转换参数
    /// </summary>
    public class N2NCOptions : ToolOptionsBase
    {
        [Option(LabelKey = nameof(KeysSliderLabel), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public Bindable<double> TargetKeys { get; } = new Bindable<double>();

        // 动态最大值将由ViewModel的约束管理处理
        [Option(LabelKey = nameof(N2NCMaxKeysTemplate), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public Bindable<double> MaxKeys { get; } = new Bindable<double>();

        [Option(LabelKey = nameof(N2NCMinKeysTemplate), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public Bindable<double> MinKeys { get; } = new Bindable<double>();

        [Option(LabelKey = nameof(N2NCTransformSpeedTemplate), Min = 1, Max = 8, UIType = UIType.Slider, DisplayMapField = nameof(TransformSpeedDict), DataType = typeof(double))]
        public Bindable<double> TransformSpeed { get; } = new Bindable<double>();

        /// <summary>
        /// 默认构造函数，使用默认值
        /// </summary>
        public N2NCOptions()
        {
            TargetKeys.Value = 10;
            MaxKeys.Value = 10;
            MinKeys.Value = 2;
            TransformSpeed.Value = 4.0;

            // Wire up property changed events for Bindable<T> properties
            TargetKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TargetKeys));
            MaxKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(MaxKeys));
            MinKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(MinKeys));
            TransformSpeed.PropertyChanged += (_, _) => OnPropertyChanged(nameof(TransformSpeed));
        }

        // N2NC 的 TransformSpeed 显示映射
        public static readonly Dictionary<double, string> TransformSpeedDict = new Dictionary<double, string>
        {
            { 1, "1/16" },
            { 2, "1/8" },
            { 3, "1/4" },
            { 4, "1/2" },
            { 5, "1" },
            { 6, "2" },
            { 7, "4" },
            { 8, "8" }
        };

        [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox, DataType = typeof(int?))]
        public int? Seed
        {
            get => _seed;
            set => SetProperty(ref _seed, value);
        }
        private int? _seed = 114514;

        public List<int>? SelectedKeyTypes { get; set; }

        public KeySelectionFlags? SelectedKeyFlags { get; set; } = KeySelectionFlags.None;

        public override void Validate()
        {
            base.Validate(); // First clamp to Min/Max
            
            // 确保 TargetKeys 在合理范围内
            if (TargetKeys.Value < 1) TargetKeys.Value = 1;
            if (TargetKeys.Value > 18) TargetKeys.Value = 18;

            // 确保 TransformSpeed 在有效范围内
            if (TransformSpeed.Value < 1) TransformSpeed.Value = 1;
            if (TransformSpeed.Value > 8) TransformSpeed.Value = 8;

            // 确保 MinKeys 和 MaxKeys 在合理范围内
            if (MinKeys.Value < 1) MinKeys.Value = 1;
            if (MaxKeys.Value > 18) MaxKeys.Value = 18;

            if (MaxKeys.Value > TargetKeys.Value) MaxKeys.Value = TargetKeys.Value;
            if (MaxKeys.Value < MinKeys.Value) MaxKeys.Value = MinKeys.Value;
            if (MinKeys.Value > MaxKeys.Value) MinKeys.Value = MaxKeys.Value;
        }
    }
}