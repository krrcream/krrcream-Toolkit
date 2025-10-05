using System.Collections.Generic;
using krrTools.Configuration;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// 转换选项类，用于封装所有转换参数
    /// </summary>
    public class N2NCOptions : UnifiedToolOptions
    {
        [Option(LabelKey = nameof(KeysSliderLabel), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public double TargetKeys { get; set; } = 10;

        //TODO: 功能脱节，需要检查修复
        [Option(LabelKey = nameof(N2NCMaxKeysTemplate), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public double MaxKeys { get; set; } = 10;

        [Option(LabelKey = nameof(N2NCMinKeysTemplate), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public double MinKeys { get; set; } = 2;

        [Option(LabelKey = nameof(N2NCTransformSpeedTemplate), Min = 1, Max = 8, UIType = UIType.Slider, DataType = typeof(double))]
        public double TransformSpeed { get; set; } = 4.0;

        [Option(LabelKey = nameof(SeedButtonLabel), UIType = UIType.NumberBox, DataType = typeof(int?))]
        public int? Seed { get; set; } = 114514;

        public List<int>? SelectedKeyTypes { get; set; }

        public KeySelectionFlags? SelectedKeyFlags { get; set; } = KeySelectionFlags.None;

        public new void Validate()
        {
        }
    }
}