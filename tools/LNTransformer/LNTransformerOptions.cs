using krrTools.tools.Shared;

namespace krrTools.tools.LNTransformer
{
    public class LNTransformerOptions : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IToolOptions
    {
        private double _levelValue;
        private double _percentageValue;
        private double _divideValue;
        private double _columnValue;
        private double _gapValue;
        private double _overallDifficulty;
        private bool _ignoreIsChecked;
        private bool _originalLNIsChecked;
        private bool _fixErrorIsChecked;

        public double LevelValue
        {
            get => _levelValue;
            set => SetProperty(ref _levelValue, value);
        }
        public double PercentageValue
        {
            get => _percentageValue;
            set => SetProperty(ref _percentageValue, value);
        }
        public double DivideValue
        {
            get => _divideValue;
            set => SetProperty(ref _divideValue, value);
        }
        public double ColumnValue
        {
            get => _columnValue;
            set => SetProperty(ref _columnValue, value);
        }
        public double GapValue
        {
            get => _gapValue;
            set => SetProperty(ref _gapValue, value);
        }
        public double OverallDifficulty
        {
            get => _overallDifficulty;
            set => SetProperty(ref _overallDifficulty, value);
        }
        public bool IgnoreIsChecked
        {
            get => _ignoreIsChecked;
            set => SetProperty(ref _ignoreIsChecked, value);
        }
        public bool OriginalLNIsChecked
        {
            get => _originalLNIsChecked;
            set => SetProperty(ref _originalLNIsChecked, value);
        }
        public bool FixErrorIsChecked
        {
            get => _fixErrorIsChecked;
            set => SetProperty(ref _fixErrorIsChecked, value);
        }

        public void Validate()
        {
            // 验证滑条值是否在合理范围内
            if (LevelValue < 0) LevelValue = 0;
            if (LevelValue > 100) LevelValue = 100;
            
            if (PercentageValue < 0) PercentageValue = 0;
            if (PercentageValue > 100) PercentageValue = 100;
            
            if (DivideValue < 1) DivideValue = 1;
            if (DivideValue > 10) DivideValue = 10;
            
            if (ColumnValue < 1) ColumnValue = 1;
            if (ColumnValue > 18) ColumnValue = 18;
            
            if (GapValue < 0) GapValue = 0;
            if (GapValue > 1000) GapValue = 1000;
            
            if (OverallDifficulty < 0) OverallDifficulty = 0;
            if (OverallDifficulty > 10.0) OverallDifficulty = 10.0;
        }
    }
}
