using krrTools.Configuration;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP工具选项类，用于封装所有DP参数
    /// </summary>
    public class DPToolOptions : UnifiedToolOptions
    {
        [Option(LabelKey = nameof(DPModifyKeysCheckbox), TooltipKey = nameof(DPModifyKeysTooltip), UIType = UIType.Toggle)]
        public bool ModifySingleSideKeyCount { get; set; }

        [Option(LabelKey = nameof(DPMirrorLabel), TooltipKey = nameof(DPMirrorTooltipLeft), UIType = UIType.Toggle)]
        public bool LMirror { get; set; }

        [Option(LabelKey = nameof(DPDensityLabel), TooltipKey = nameof(DPDensityTooltipLeft), UIType = UIType.Toggle)]
        public bool LDensity { get; set; }

        [Option(LabelKey = nameof(DPLeftMaxKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(int))]
        public int LMaxKeys { get; set; } = 5;

        [Option(LabelKey = nameof(DPLeftMinKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(int))]
        public int LMinKeys { get; set; } = 1;

        [Option(LabelKey = nameof(RemoveLabel), TooltipKey = nameof(RemoveTooltip), UIType = UIType.Toggle)]
        public bool LRemove { get; set; }

        [Option(LabelKey = nameof(DPMirrorLabel), TooltipKey = nameof(DPMirrorTooltipRight), UIType = UIType.Toggle)]
        public bool RMirror { get; set; }

        [Option(LabelKey = nameof(DPDensityLabel), TooltipKey = nameof(DPDensityTooltipRight), UIType = UIType.Toggle)]
        public bool RDensity { get; set; }

        [Option(LabelKey = nameof(DPRightMaxKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(int))]
        public int RMaxKeys { get; set; } = 5;

        [Option(LabelKey = nameof(DPRightMinKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(int))]
        public int RMinKeys { get; set; } = 1;

        [Option(LabelKey = nameof(RemoveLabel), TooltipKey = nameof(RemoveTooltip), UIType = UIType.Toggle)]
        public bool RRemove { get; set; }

        [Option(LabelKey = nameof(KeysSliderLabel), Min = 1, Max = 9, UIType = UIType.Slider, DataType = typeof(int))]
        public int SingleSideKeyCount { get; set; } = 5;

        // 设置约束, 以后走接口实现
        public new void Validate()
        {
            if (LMinKeys > LMaxKeys) LMinKeys = LMaxKeys;
            if (RMinKeys > RMaxKeys) RMinKeys = RMaxKeys;
        }
    }
}
