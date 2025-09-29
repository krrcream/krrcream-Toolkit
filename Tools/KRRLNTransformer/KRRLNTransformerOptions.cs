using krrTools.Configuration;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerOptions : UnifiedToolOptions
    {
        // 短面条设置
        public double ShortPercentageValue { get; set; } = 50;
        public double ShortLevelValue { get; set; } = 5;
        public double ShortLimitValue { get; set; } = 20;
        public double ShortRandomValue { get; set; } = 50;
    
        // 长面条设置
        public double LongPercentageValue { get; set; } = 50;
        public double LongLevelValue { get; set; } = 5;
        public double LongLimitValue { get; set; } = 20;
        public double LongRandomValue { get; set; } = 50;
    
        // 对齐设置
        public bool AlignIsChecked { get; set; }
        public double AlignValue { get; set; } = 4;
    
        // 处理原始面条
        public bool ProcessOriginalIsChecked { get; set; } = false;
    
        // OD设置
        public double ODValue { get; set; } = 8;
    
        // 种子值
        public string? SeedText { get; set; } = "114514";

        public override void Validate()
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
