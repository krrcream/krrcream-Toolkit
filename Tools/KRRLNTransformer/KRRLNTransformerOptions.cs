using krrTools.Configuration;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : UnifiedToolOptions
    {
        // 短面条设置
        [Option(LabelKey = nameof(KRRShortPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
        public double ShortPercentageValue { get; set; } = 50;

        [Option(LabelKey = nameof(KRRShortLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider, DataType = typeof(double))]
        public double ShortLevelValue { get; set; } = 5;

        [Option(LabelKey = nameof(KRRShortLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider, DataType = typeof(double))]
        public double ShortLimitValue { get; set; } = 20;

        [Option(LabelKey = nameof(KRRShortRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
        public double ShortRandomValue { get; set; } = 50;
    
        // 长面条设置
        [Option(LabelKey = nameof(KRRLongPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
        public double LongPercentageValue { get; set; } = 50;

        [Option(LabelKey = nameof(KRRLongLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider, DataType = typeof(double))]
        public double LongLevelValue { get; set; } = 5;

        [Option(LabelKey = nameof(KRRLongLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider, DataType = typeof(double))]
        public double LongLimitValue { get; set; } = 20;

        [Option(LabelKey = nameof(KRRLongRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
        public double LongRandomValue { get; set; } = 50;
    
        // 对齐设置
        public bool AlignIsChecked { get; set; }
        // AlignValue 不使用[Option]，将在Control中手动处理
        public double AlignValue { get; set; } = 4;
    
        // 处理原始面条设置
        [Option(LabelKey = nameof(KRRProcessOriginalLabel), UIType = UIType.Toggle, DataType = typeof(bool))]
        public bool ProcessOriginalIsChecked { get; set; }
    
        // OD设置
        [Option(LabelKey = nameof(ODSliderLabel), Min = 0, Max = 10, UIType = UIType.Slider, DataType = typeof(double))]
        public double ODValue { get; set; } = 8;
    
        // 种子值
        [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox, DataType = typeof(string))]
        public string? SeedText { get; set; } = "114514";
    }
}
