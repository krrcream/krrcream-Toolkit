using System.Collections.Generic;
using krrTools.Configuration;
using krrTools.Bindable;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : ToolOptionsBase
    {
        // Short LN settings
        [Option(LabelKey = nameof(KRRShortPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider)]
        public Bindable<double> ShortPercentage { get; } = new(50);

        [Option(LabelKey = nameof(KRRShortLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider)]
        public Bindable<double> ShortLevel { get; } = new(5);

        [Option(LabelKey = nameof(KRRShortLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider)]
        public Bindable<double> ShortLimit { get; } = new(20);

        [Option(LabelKey = nameof(KRRShortRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider)]
        public Bindable<double> ShortRandom { get; } = new(50);

        // Long LN settings
        [Option(LabelKey = nameof(KRRLongPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider)]
        public Bindable<double> LongPercentage { get; } = new(50);

        [Option(LabelKey = nameof(KRRLongLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider)]
        public Bindable<double> LongLevel { get; } = new(5);

        [Option(LabelKey = nameof(KRRLongLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider)]
        public Bindable<double> LongLimit { get; } = new(20);

        [Option(LabelKey = nameof(KRRLongRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider)]
        public Bindable<double> LongRandom { get; } = new(50);

        // Threshold and alignment settings (nullable for toggle behavior)
        [Option(LabelKey = nameof(LengthThresholdLabel), Min = 0, Max = 10, UIType = UIType.Slider, DisplayMapField = "LengthThresholdDict")]
        public Bindable<double?> LengthThreshold { get; } = new(4);

        [Option(LabelKey = nameof(KRRAlignLabel), Min = 1, Max = 9, UIType = UIType.Slider, DisplayMapField = "AlignValuesDict")]
        public Bindable<double?> Alignment { get; } = new(4);

        [Option(LabelKey = nameof(KRRLNAlignLabel), Min = 1, Max = 9, UIType = UIType.Slider, DisplayMapField = "AlignValuesDict")]
        public Bindable<double?> LNAlignment { get; } = new(4);

        // General settings
        [Option(UIType = UIType.Toggle)]
        public Bindable<bool> ProcessOriginalIsChecked { get; } = new();

        [Option(LabelKey = nameof(ODSliderLabel), Min = 0, Max = 10, UIType = UIType.Slider)]
        public Bindable<double?> ODValue { get; } = new();

        [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox)]
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
            { 1, "1/16" },
            { 2, "1/8" },
            { 3, "1/7" },
            { 4, "1/6" },
            { 5, "1/5" },
            { 6, "1/4" },
            { 7, "1/3" },
            { 8, "1/2" },
            { 9, "1/1" }
        };

        public static Dictionary<double, string> LengthThresholdDict = new()
        {
            { 0, "Off" },
            { 1, "1/8" },
            { 2, "1/6" },
            { 3, "1/4" },
            { 4, "1/3" },
            { 5, "1/2" },
            { 6, "2/3" },
            { 7, "3/4" },
            { 8, "1" },
            { 9, "4/3" },
            { 10, "3/2" }
        };
    }
}