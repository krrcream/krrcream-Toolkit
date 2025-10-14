using System.Collections.Generic;
using krrTools.Bindable;
using krrTools.Core;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : ToolOptionsBase
    {
        // Threshold and alignment settings (nullable for toggle behavior)
        [Option(LabelKey = nameof(LengthThresholdLabel), Min = 0, Max = 65, UIType = UIType.Slider, DisplayMapField = nameof(LengthThresholdDict), IsRefresher = true)]
        public Bindable<double?> LengthThreshold { get; } = new(16);
        
        // Short LN settings
        [Option(LabelKey = nameof(KRRShortPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ShortPercentage { get; } = new(100);

        [Option(LabelKey = nameof(KRRShortLevelLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ShortLevel { get; } = new(50);

        [Option(LabelKey = nameof(KRRShortLimitLabel), Min = 0, Max = 10, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ShortLimit { get; } = new(10);

        [Option(LabelKey = nameof(KRRShortRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> ShortRandom { get; } = new();

        // Long LN settings
        [Option(LabelKey = nameof(KRRLongPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> LongPercentage { get; } = new(50);

        [Option(LabelKey = nameof(KRRLongLevelLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> LongLevel { get; } = new(50);

        [Option(LabelKey = nameof(KRRLongLimitLabel), Min = 0, Max = 10, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> LongLimit { get; } = new(10);

        [Option(LabelKey = nameof(KRRLongRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double> LongRandom { get; } = new(50);

        [Option(LabelKey = nameof(KRRAlignLabel), Min = 1, Max = 8, UIType = UIType.Slider, DisplayMapField = nameof(AlignValuesDict), IsRefresher = true)]
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
            { 1, "1/16"},
            { 2, "2/16"},
            { 3, "3/16"},
            { 4, "4/16"},
            { 5, "5/16"},
            { 6, "6/16"},
            { 7, "7/16"},
            { 8, "8/16"},
            { 9, "9/16"},
            { 10, "10/16"},
            { 11, "11/16"},
            { 12, "12/16"},
            { 13, "13/16"},
            { 14, "14/16"},
            { 15, "15/16"},
            { 16, "16/16"},
            { 17, "17/16"},
            { 18, "18/16"},
            { 19, "19/16"},
            { 20, "20/16"},
            { 21, "21/16"},
            { 22, "22/16"},
            { 23, "23/16"},
            { 24, "24/16"},
            { 25, "25/16"},
            { 26, "26/16"},
            { 27, "27/16"},
            { 28, "28/16"},
            { 29, "29/16"},
            { 30, "30/16"},
            { 31, "31/16"},
            { 32, "32/16"},
            { 33, "33/16"},
            { 34, "34/16"},
            { 35, "35/16"},
            { 36, "36/16"},
            { 37, "37/16"},
            { 38, "38/16"},
            { 39, "39/16"},
            { 40, "40/16"},
            { 41, "41/16"},
            { 42, "42/16"},
            { 43, "43/16"},
            { 44, "44/16"},
            { 45, "45/16"},
            { 46, "46/16"},
            { 47, "47/16"},
            { 48, "48/16"},
            { 49, "49/16"},
            { 50, "50/16"},
            { 51, "51/16"},
            { 52, "52/16"},
            { 53, "53/16"},
            { 54, "54/16"},
            { 55, "55/16"},
            { 56, "56/16"},
            { 57, "57/16"},
            { 58, "58/16"},
            { 59, "59/16"},
            { 60, "60/16"},
            { 61, "61/16"},
            { 62, "62/16"},
            { 63, "63/16"},
            { 64, "64/16"},
            { 65, "AllIsShortLN"}
        };
    }
}