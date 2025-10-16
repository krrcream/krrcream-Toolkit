using System;
using System.Collections.Generic;
using krrTools.Bindable;
using krrTools.Core;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : ToolOptionsBase
    {
        // Threshold and alignment settings (nullable for toggle behavior)
        [Option(LabelKey = nameof(LengthThresholdLabel), Min = 0, Max = 65, UIType = UIType.Slider,
            DisplayMapField = nameof(LengthThresholdDict), IsRefresher = true)]
        public Bindable<double?> LengthThreshold { get; } = new(16);

        // 在绑定短面上限值
        public double ShortLevelMax => LengthThreshold.Value >= 65 ? 64 : (LengthThreshold.Value ?? 16);


        // Short LN settings
        [Option(LabelKey = nameof(KRRShortPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider,
            IsRefresher = true)]
        public Bindable<double> ShortPercentage { get; } = new(100);

        [Option(LabelKey = nameof(KRRShortLevelLabel), Min = 0, Max = 256, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ShortLevel { get; } = new(8);

        [Option(LabelKey = nameof(KRRShortLimitLabel), Min = 0, Max = 10, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ShortLimit { get; } = new(10);

        [Option(LabelKey = nameof(KRRShortRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ShortRandom { get; } = new();

        // Long LN settings
        [Option(LabelKey = nameof(KRRLongPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider,
            IsRefresher = true)]
        public Bindable<double> LongPercentage { get; } = new(50);

        [Option(LabelKey = nameof(KRRLongLevelLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> LongLevel { get; } = new(50);

        [Option(LabelKey = nameof(KRRLongLimitLabel), Min = 0, Max = 10, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> LongLimit { get; } = new(10);

        [Option(LabelKey = nameof(KRRLongRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> LongRandom { get; } = new(50);

        [Option(LabelKey = nameof(KRRAlignLabel), Min = 1, Max = 8, UIType = UIType.Slider,
            DisplayMapField = nameof(AlignValuesDict), IsRefresher = true)]
        public Bindable<double?> Alignment { get; } = new(5);

        /*
        // 暂时隐藏长短面对齐选项
        [Option(LabelKey = nameof(KRRLNAlignLabel), Min = 1, Max = 8, UIType = UIType.Slider, DisplayMapField = nameof(AlignValuesDict), IsRefresher = true)]
        */
        public Bindable<double?> LNAlignment { get; } = new(6);

        // General settings
        [Option(LabelKey = nameof(ProcessOriginalLabel), UIType = UIType.Toggle, IsRefresher = true)]
        public Bindable<bool> ProcessOriginalIsChecked { get; } = new();

        [Option(LabelKey = nameof(ODSliderLabel), Min = 0, Max = 10, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ODValue { get; } = new(0);

        [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox, IsRefresher = true)]
        public Bindable<int?> Seed { get; } = new(114514);

        public KRRLNTransformerOptions()
        {
            // Wire up property change events for UI updates
            LengthThreshold.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LengthThreshold));
            Alignment.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Alignment));
            LNAlignment.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LNAlignment));

            ShortPercentage.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ShortPercentage));
            ShortLevel.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ShortLevel));
            ShortLimit.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ShortLimit));
            ShortRandom.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ShortRandom));

            LongPercentage.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LongPercentage));
            LongLevel.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LongLevel));
            LongLimit.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LongLimit));
            LongRandom.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LongRandom));

            ProcessOriginalIsChecked.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ProcessOriginalIsChecked));
            ODValue.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ODValue));

            Seed.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Seed));
        }

        // KRRLN 工具的映射字典定义
        public static Dictionary<double, string> AlignValuesDict = new()
        {
            { 1, "1/8" },
            { 2, "1/7" },
            { 3, "1/6" },
            { 4, "1/5" },
            { 5, "1/4" },
            { 6, "1/3" },
            { 7, "1/2" },
            { 8, "1/1" }
        };

        public static Dictionary<double, string> LengthThresholdDict = new()
        {
            { 0, "AllIsLongLN" },
            { 1, "1/4" },
            { 2, "2/4" },
            { 3, "3/4" },
            { 4, "1" },
            { 5, "1+1/4" },
            { 6, "1+2/4" },
            { 7, "1+3/4" },
            { 8, "2" },
            { 9, "2+1/4" },
            { 10, "2+2/4" },
            { 11, "2+3/4" },
            { 12, "3" },
            { 13, "3+1/4" },
            { 14, "3+2/4" },
            { 15, "3+3/4" },
            { 16, "4" },
            { 17, "4+1/4" },
            { 18, "4+2/4" },
            { 19, "4+3/4" },
            { 20, "5" },
            { 21, "5+1/4" },
            { 22, "5+2/4" },
            { 23, "5+3/4" },
            { 24, "6" },
            { 25, "6+1/4" },
            { 26, "6+2/4" },
            { 27, "6+3/4" },
            { 28, "7" },
            { 29, "7+1/4" },
            { 30, "7+2/4" },
            { 31, "7+3/4" },
            { 32, "8" },
            { 33, "8+1/4" },
            { 34, "8+2/4" },
            { 35, "8+3/4" },
            { 36, "9" },
            { 37, "9+1/4" },
            { 38, "9+2/4" },
            { 39, "9+3/4" },
            { 40, "10" },
            { 41, "10+1/4" },
            { 42, "10+2/4" },
            { 43, "10+3/4" },
            { 44, "11" },
            { 45, "11+1/4" },
            { 46, "11+2/4" },
            { 47, "11+3/4" },
            { 48, "12" },
            { 49, "12+1/4" },
            { 50, "12+2/4" },
            { 51, "12+3/4" },
            { 52, "13" },
            { 53, "13+1/4" },
            { 54, "13+2/4" },
            { 55, "13+3/4" },
            { 56, "14" },
            { 57, "14+1/4" },
            { 58, "14+2/4" },
            { 59, "14+3/4" },
            { 60, "15" },
            { 61, "15+1/4" },
            { 62, "15+2/4" },
            { 63, "15+3/4" },
            { 64, "16" },
            { 65, "AllIsShortLN" }
        };

    }
}
