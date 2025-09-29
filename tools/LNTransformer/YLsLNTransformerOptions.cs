using krrTools.tools.Shared;

namespace krrTools.tools.LNTransformer
{
    public class YLsLNTransformerOptions : IToolOptions
    {
        public double LevelValue { get; set; } = 5;
        public double PercentageValue { get; set; } = 50;
        public double DivideValue { get; set; } = 1;
        public double ColumnValue { get; set; } = 1;
        public double GapValue { get; set; } = 0;
        public double OverallDifficulty { get; set; } = 8;
        public bool IgnoreIsChecked { get; set; }
        public bool OriginalLNIsChecked { get; set; }
        public bool FixErrorIsChecked { get; set; } = true;

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
