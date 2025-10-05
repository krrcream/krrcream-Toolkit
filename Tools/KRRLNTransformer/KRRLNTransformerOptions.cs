using krrTools.Configuration;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : UnifiedToolOptions
    {
        public ShortSettings Short { get; set; } = new ShortSettings();
        public LongSettings Long { get; set; } = new LongSettings();
        public LengthThresholdSettings LengthThreshold { get; set; } = new LengthThresholdSettings();
        public AlignmentSettings Alignment { get; set; } = new AlignmentSettings();
        public LNAlignmentSettings LNAlignment { get; set; } = new LNAlignmentSettings();
        public GeneralSettings General { get; set; } = new GeneralSettings();

        public class ShortSettings : ToolOptionsBase
        {
            [Option(LabelKey = nameof(KRRShortPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
            public double PercentageValue { get; set; } = 50;

            [Option(LabelKey = nameof(KRRShortLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider, DataType = typeof(double))]
            public double LevelValue { get; set; } = 5;

            [Option(LabelKey = nameof(KRRShortLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider, DataType = typeof(double))]
            public double LimitValue { get; set; } = 20;

            [Option(LabelKey = nameof(KRRShortRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
            public double RandomValue { get; set; } = 50;
        }

        public class LongSettings : ToolOptionsBase
        {
            [Option(LabelKey = nameof(KRRLongPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
            public double PercentageValue { get; set; } = 50;

            [Option(LabelKey = nameof(KRRLongLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider, DataType = typeof(double))]
            public double LevelValue { get; set; } = 5;

            [Option(LabelKey = nameof(KRRLongLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider, DataType = typeof(double))]
            public double LimitValue { get; set; } = 20;

            [Option(LabelKey = nameof(KRRLongRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider, DataType = typeof(double))]
            public double RandomValue { get; set; } = 50;
        }

        public class LengthThresholdSettings : ToolOptionsBase
        {
            [Option(LabelKey = nameof(LengthThresholdLabel), Min = 0, Max = 12, UIType = UIType.Slider, DataType = typeof(double))]
            public double Value
            {
                get => _value;
                set => SetProperty(ref _value, value);
            }

            [Option(LabelKey = nameof(LengthThresholdLabel), UIType = UIType.Toggle, DataType = typeof(bool))]
            public bool IsChecked
            {
                get => _isChecked;
                set => SetProperty(ref _isChecked, value);
            }

            private double _value = 4;
            private bool _isChecked;
        }
        
        public class AlignmentSettings : ToolOptionsBase
        {
            [Option(LabelKey = nameof(KRRAlignLabel), Min = 1, Max = 9, UIType = UIType.Slider, DataType = typeof(double))]
            public double Value
            {
                get => _value;
                set => SetProperty(ref _value, value);
            }

            [Option(LabelKey = nameof(KRRAlignLabel), UIType = UIType.Toggle, DataType = typeof(bool))]
            public bool IsChecked
            {
                get => _isChecked;
                set => SetProperty(ref _isChecked, value);
            }

            private double _value = 4;
            private bool _isChecked;
        }

        public class LNAlignmentSettings : ToolOptionsBase
        {
            [Option(LabelKey = nameof(KRRLNAlignLabel), Min = 1, Max = 9, UIType = UIType.Slider, DataType = typeof(double))]
            public double Value
            {
                get => _value;
                set => SetProperty(ref _value, value);
            }

            [Option(LabelKey = nameof(KRRLNAlignLabel), UIType = UIType.Toggle, DataType = typeof(bool))]
            public bool IsChecked
            {
                get => _isChecked;
                set => SetProperty(ref _isChecked, value);
            }

            private double _value = 4;
            private bool _isChecked;
        }

        public class GeneralSettings : ToolOptionsBase
        {
            [Option(LabelKey = nameof(KRRProcessOriginalLabel), UIType = UIType.Toggle, DataType = typeof(bool))]
            public bool ProcessOriginalIsChecked { get; set; }

            [Option(LabelKey = nameof(ODSliderLabel), Min = 0, Max = 10, UIType = UIType.Slider, DataType = typeof(double))]
            public double ODValue { get; set; } = 8;
        }

        [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox, DataType = typeof(int?))]
        public int? Seed { get; set; } = 114514;
    }
}
