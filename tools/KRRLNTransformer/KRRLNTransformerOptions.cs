using System.Collections.Generic;
using krrTools.tools.Shared;

namespace krrTools.tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : IToolOptions
    {
        // 短面条设置
        public double ShortPercentageValue { get; set; }
        public double ShortLevelValue { get; set; }
        public double ShortLimitValue { get; set; }
        public double ShortRandomValue { get; set; }
    
        // 长面条设置
        public double LongPercentageValue { get; set; }
        public double LongLevelValue { get; set; }
        public double LongLimitValue { get; set; }
        public double LongRandomValue { get; set; }
    
        // 对齐设置
        public bool AlignIsChecked { get; set; }
        public double AlignValue { get; set; }
    
        // 处理原始面条
        public bool ProcessOriginalIsChecked { get; set; }
    
        // OD设置
        public double ODValue { get; set; }
    
        // 种子值
        public string? SeedText { get; set; }

        public void Validate()
        {
            // 短面条验证
            if (ShortPercentageValue < 0) ShortPercentageValue = 0;
            if (ShortLevelValue < 0) ShortLevelValue = 0;
            if (ShortLimitValue < 0) ShortLimitValue = 0;
            if (ShortRandomValue < 0) ShortRandomValue = 0;
    
            // 长面条验证
            if (LongPercentageValue < 0) LongPercentageValue = 0;
            if (LongLevelValue < 0) LongLevelValue = 0;
            if (LongLimitValue < 0) LongLimitValue = 0;
            if (LongRandomValue < 0) LongRandomValue = 0;
    
            // 对齐值验证
            if (AlignValue < 1) AlignValue = 1;
            if (AlignValue > 9) AlignValue = 9;
    
            // OD值验证
            if (ODValue < 0) ODValue = 0;
            if (ODValue > 10) ODValue = 10;
        }
    }
}
