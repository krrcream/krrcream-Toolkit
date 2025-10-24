using System;
using System.Collections.Generic;
using krrTools.Bindable;
using krrTools.Core;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : ToolOptionsBase
    {
        // 静态值方便引用

        public static readonly int SAllIsLongLN = 0;
        public static readonly int SAllIsShortLN = 65;
        public static readonly int SShortLengthMax = 256;
        public static readonly int SLongLengthMax = 100;
        
        
        // Threshold and alignment settings (nullable for toggle behavior)
        [Option(LabelKey = nameof(LengthThresholdLabel), Min = 0, Max = 65, UIType = UIType.Slider,
            DisplayMapField = nameof(LengthThresholdDict), IsRefresher = true)]
        public Bindable<double> LengthThreshold { get; } = new(16);

        // Short LN settings
        [Option(LabelKey = nameof(KRRShortPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider,
            IsRefresher = true)]
        public Bindable<double> ShortPercentage { get; } = new(100);

        [Option(LabelKey = nameof(KRRShortLevelLabel), Min = 0, Max = 256, UIType = UIType.Slider,
            DisplayMapField = nameof(ShortLengthDict) , IsRefresher = true)]
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
        public Bindable<double?> ODValue { get; } = new(5);

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
        
        public Dictionary<double, string> ShortLengthDict = new()
        {
            { 0, "0" },
            { 1, "1/16" },
            { 2, "1/8" },
            { 3, "3/16" },
            { 4, "1/4" },
            { 5, "5/16" },
            { 6, "3/8" },
            { 7, "7/16" },
            { 8, "1/2" },
            { 9, "9/16" },
            { 10, "5/8" },
            { 11, "11/16" },
            { 12, "3/4" },
            { 13, "13/16" },
            { 14, "7/8" },
            { 15, "15/16" },
            { 16, "1" },
            { 17, "1+1/16" },
            { 18, "1+1/8" },
            { 19, "1+3/16" },
            { 20, "1+1/4" },
            { 21, "1+5/16" },
            { 22, "1+3/8" },
            { 23, "1+7/16" },
            { 24, "1+1/2" },
            { 25, "1+9/16" },
            { 26, "1+5/8" },
            { 27, "1+11/16" },
            { 28, "1+3/4" },
            { 29, "1+13/16" },
            { 30, "1+7/8" },
            { 31, "1+15/16" },
            { 32, "2" },
            { 33, "2+1/16" },
            { 34, "2+1/8" },
            { 35, "2+3/16" },
            { 36, "2+1/4" },
            { 37, "2+5/16" },
            { 38, "2+3/8" },
            { 39, "2+7/16" },
            { 40, "2+1/2" },
            { 41, "2+9/16" },
            { 42, "2+5/8" },
            { 43, "2+11/16" },
            { 44, "2+3/4" },
            { 45, "2+13/16" },
            { 46, "2+7/8" },
            { 47, "2+15/16" },
            { 48, "3" },
            { 49, "3+1/16" },
            { 50, "3+1/8" },
            { 51, "3+3/16" },
            { 52, "3+1/4" },
            { 53, "3+5/16" },
            { 54, "3+3/8" },
            { 55, "3+7/16" },
            { 56, "3+1/2" },
            { 57, "3+9/16" },
            { 58, "3+5/8" },
            { 59, "3+11/16" },
            { 60, "3+3/4" },
            { 61, "3+13/16" },
            { 62, "3+7/8" },
            { 63, "3+15/16" },
            { 64, "4" },
            { 65, "4+1/16" },
            { 66, "4+1/8" },
            { 67, "4+3/16" },
            { 68, "4+1/4" },
            { 69, "4+5/16" },
            { 70, "4+3/8" },
            { 71, "4+7/16" },
            { 72, "4+1/2" },
            { 73, "4+9/16" },
            { 74, "4+5/8" },
            { 75, "4+11/16" },
            { 76, "4+3/4" },
            { 77, "4+13/16" },
            { 78, "4+7/8" },
            { 79, "4+15/16" },
            { 80, "5" },
            { 81, "5+1/16" },
            { 82, "5+1/8" },
            { 83, "5+3/16" },
            { 84, "5+1/4" },
            { 85, "5+5/16" },
            { 86, "5+3/8" },
            { 87, "5+7/16" },
            { 88, "5+1/2" },
            { 89, "5+9/16" },
            { 90, "5+5/8" },
            { 91, "5+11/16" },
            { 92, "5+3/4" },
            { 93, "5+13/16" },
            { 94, "5+7/8" },
            { 95, "5+15/16" },
            { 96, "6" },
            { 97, "6+1/16" },
            { 98, "6+1/8" },
            { 99, "6+3/16" },
            { 100, "6+1/4" },
            { 101, "6+5/16" },
            { 102, "6+3/8" },
            { 103, "6+7/16" },
            { 104, "6+1/2" },
            { 105, "6+9/16" },
            { 106, "6+5/8" },
            { 107, "6+11/16" },
            { 108, "6+3/4" },
            { 109, "6+13/16" },
            { 110, "6+7/8" },
            { 111, "6+15/16" },
            { 112, "7" },
            { 113, "7+1/16" },
            { 114, "7+1/8" },
            { 115, "7+3/16" },
            { 116, "7+1/4" },
            { 117, "7+5/16" },
            { 118, "7+3/8" },
            { 119, "7+7/16" },
            { 120, "7+1/2" },
            { 121, "7+9/16" },
            { 122, "7+5/8" },
            { 123, "7+11/16" },
            { 124, "7+3/4" },
            { 125, "7+13/16" },
            { 126, "7+7/8" },
            { 127, "7+15/16" },
            { 128, "8" },
            { 129, "8+1/16" },
            { 130, "8+1/8" },
            { 131, "8+3/16" },
            { 132, "8+1/4" },
            { 133, "8+5/16" },
            { 134, "8+3/8" },
            { 135, "8+7/16" },
            { 136, "8+1/2" },
            { 137, "8+9/16" },
            { 138, "8+5/8" },
            { 139, "8+11/16" },
            { 140, "8+3/4" },
            { 141, "8+13/16" },
            { 142, "8+7/8" },
            { 143, "8+15/16" },
            { 144, "9" },
            { 145, "9+1/16" },
            { 146, "9+1/8" },
            { 147, "9+3/16" },
            { 148, "9+1/4" },
            { 149, "9+5/16" },
            { 150, "9+3/8" },
            { 151, "9+7/16" },
            { 152, "9+1/2" },
            { 153, "9+9/16" },
            { 154, "9+5/8" },
            { 155, "9+11/16" },
            { 156, "9+3/4" },
            { 157, "9+13/16" },
            { 158, "9+7/8" },
            { 159, "9+15/16" },
            { 160, "10" },
            { 161, "10+1/16" },
            { 162, "10+1/8" },
            { 163, "10+3/16" },
            { 164, "10+1/4" },
            { 165, "10+5/16" },
            { 166, "10+3/8" },
            { 167, "10+7/16" },
            { 168, "10+1/2" },
            { 169, "10+9/16" },
            { 170, "10+5/8" },
            { 171, "10+11/16" },
            { 172, "10+3/4" },
            { 173, "10+13/16" },
            { 174, "10+7/8" },
            { 175, "10+15/16" },
            { 176, "11" },
            { 177, "11+1/16" },
            { 178, "11+1/8" },
            { 179, "11+3/16" },
            { 180, "11+1/4" },
            { 181, "11+5/16" },
            { 182, "11+3/8" },
            { 183, "11+7/16" },
            { 184, "11+1/2" },
            { 185, "11+9/16" },
            { 186, "11+5/8" },
            { 187, "11+11/16" },
            { 188, "11+3/4" },
            { 189, "11+13/16" },
            { 190, "11+7/8" },
            { 191, "11+15/16" },
            { 192, "12" },
            { 193, "12+1/16" },
            { 194, "12+1/8" },
            { 195, "12+3/16" },
            { 196, "12+1/4" },
            { 197, "12+5/16" },
            { 198, "12+3/8" },
            { 199, "12+7/16" },
            { 200, "12+1/2" },
            { 201, "12+9/16" },
            { 202, "12+5/8" },
            { 203, "12+11/16" },
            { 204, "12+3/4" },
            { 205, "12+13/16" },
            { 206, "12+7/8" },
            { 207, "12+15/16" },
            { 208, "13" },
            { 209, "13+1/16" },
            { 210, "13+1/8" },
            { 211, "13+3/16" },
            { 212, "13+1/4" },
            { 213, "13+5/16" },
            { 214, "13+3/8" },
            { 215, "13+7/16" },
            { 216, "13+1/2" },
            { 217, "13+9/16" },
            { 218, "13+5/8" },
            { 219, "13+11/16" },
            { 220, "13+3/4" },
            { 221, "13+13/16" },
            { 222, "13+7/8" },
            { 223, "13+15/16" },
            { 224, "14" },
            { 225, "14+1/16" },
            { 226, "14+1/8" },
            { 227, "14+3/16" },
            { 228, "14+1/4" },
            { 229, "14+5/16" },
            { 230, "14+3/8" },
            { 231, "14+7/16" },
            { 232, "14+1/2" },
            { 233, "14+9/16" },
            { 234, "14+5/8" },
            { 235, "14+11/16" },
            { 236, "14+3/4" },
            { 237, "14+13/16" },
            { 238, "14+7/8" },
            { 239, "14+15/16" },
            { 240, "15" },
            { 241, "15+1/16" },
            { 242, "15+1/8" },
            { 243, "15+3/16" },
            { 244, "15+1/4" },
            { 245, "15+5/16" },
            { 246, "15+3/8" },
            { 247, "15+7/16" },
            { 248, "15+1/2" },
            { 249, "15+9/16" },
            { 250, "15+5/8" },
            { 251, "15+11/16" },
            { 252, "15+3/4" },
            { 253, "15+13/16" },
            { 254, "15+7/8" },
            { 255, "15+15/16" },
            { 256, "16" }
        };

    }
}
