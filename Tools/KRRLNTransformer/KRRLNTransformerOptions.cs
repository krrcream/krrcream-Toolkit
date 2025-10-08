using krrTools.Configuration;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.KRRLNTransformer;

public class KRRLNTransformerOptions : UnifiedToolOptions
{
    public ShortSettings Short { get; set; } = new();
    public LongSettings Long { get; set; } = new();
    public LengthThresholdSettings LengthThreshold { get; set; } = new();
    public AlignmentSettings Alignment { get; set; } = new();
    public LNAlignmentSettings LNAlignment { get; set; } = new();
    public GeneralSettings General { get; set; } = new();

    public class ShortSettings : ToolOptionsBase
    {
        [Option(LabelKey = nameof(KRRShortPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider,
            DataType = typeof(double))]
        public double PercentageValue
        {
            get => _percentageValue;
            set => SetProperty(ref _percentageValue, value);
        }

        private double _percentageValue = 50;

        [Option(LabelKey = nameof(KRRShortLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider,
            DataType = typeof(double))]
        public double LevelValue
        {
            get => _levelValue;
            set => SetProperty(ref _levelValue, value);
        }

        private double _levelValue = 5;

        [Option(LabelKey = nameof(KRRShortLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider,
            DataType = typeof(double))]
        public double LimitValue
        {
            get => _limitValue;
            set => SetProperty(ref _limitValue, value);
        }

        private double _limitValue = 20;

        [Option(LabelKey = nameof(KRRShortRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider,
            DataType = typeof(double))]
        public double RandomValue
        {
            get => _randomValue;
            set => SetProperty(ref _randomValue, value);
        }

        private double _randomValue = 50;
    }

    public class LongSettings : ToolOptionsBase
    {
        [Option(LabelKey = nameof(KRRLongPercentageLabel), Min = 0, Max = 100, UIType = UIType.Slider,
            DataType = typeof(double))]
        public double PercentageValue
        {
            get => _percentageValue;
            set => SetProperty(ref _percentageValue, value);
        }

        private double _percentageValue = 50;

        [Option(LabelKey = nameof(KRRLongLevelLabel), Min = 0, Max = 10, UIType = UIType.Slider,
            DataType = typeof(double))]
        public double LevelValue
        {
            get => _levelValue;
            set => SetProperty(ref _levelValue, value);
        }

        private double _levelValue = 5;

        [Option(LabelKey = nameof(KRRLongLimitLabel), Min = 0, Max = 50, UIType = UIType.Slider,
            DataType = typeof(double))]
        public double LimitValue
        {
            get => _limitValue;
            set => SetProperty(ref _limitValue, value);
        }

        private double _limitValue = 20;

        [Option(LabelKey = nameof(KRRLongRandomLabel), Min = 0, Max = 100, UIType = UIType.Slider,
            DataType = typeof(double))]
        public int RandomValue
        {
            get => _randomValue;
            set => SetProperty(ref _randomValue, value);
        }

        private int _randomValue = 50;
    }

    public class LengthThresholdSettings : ToolOptionsBase
    {
        [Option(LabelKey = nameof(LengthThresholdLabel), Min = 0, Max = 10, UIType = UIType.Slider,
            DataType = typeof(double))]
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
        [Option(LabelKey = nameof(KRRLNAlignLabel), Min = 1, Max = 9, UIType = UIType.Slider,
            DataType = typeof(double))]
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
        public bool ProcessOriginalIsChecked
        {
            get => _processOriginalIsChecked;
            set => SetProperty(ref _processOriginalIsChecked, value);
        }

        private bool _processOriginalIsChecked;

        [Option(LabelKey = nameof(ODSliderLabel), Min = 0, Max = 10, UIType = UIType.Slider, DataType = typeof(double))]
        public double ODValue
        {
            get => _odValue;
            set => SetProperty(ref _odValue, value);
        }

        private double _odValue;
    }

    [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox, DataType = typeof(int?))]
    public int? Seed
    {
        get => _seed;
        set => SetProperty(ref _seed, value);
    }

    private int? _seed = 114514;
}