using System.Collections.Generic;
using krrTools.tools.Shared;

namespace krrTools.tools.LNTransformer
{
    public class LNTransformerOptions : IToolOptions
    {
        public string? SeedText { get; set; }
        public double LevelValue { get; set; }
        public double PercentageValue { get; set; }
        public double DivideValue { get; set; }
        public double ColumnValue { get; set; }
        public double GapValue { get; set; }

        public bool IgnoreIsChecked { get; set; }
        public bool OriginalLNIsChecked { get; set; }
        public bool FixErrorIsChecked { get; set; }

        public double OverallDifficulty { get; set; }
        public string? CreatorText { get; set; }
        public List<int>? CheckKeys { get; set; }

        public void Validate()
        {
            if (LevelValue < 0) LevelValue = 0;
            if (PercentageValue < 0) PercentageValue = 0;
            if (DivideValue < 1) DivideValue = 1;
            if (ColumnValue < 1) ColumnValue = 1;
            if (GapValue < 0) GapValue = 0;
        }
    }
}
