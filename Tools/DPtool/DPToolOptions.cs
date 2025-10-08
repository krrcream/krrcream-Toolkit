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
        public bool ModifySingleSideKeyCount
        {
            get => _modifySingleSideKeyCount;
            set => SetProperty(ref _modifySingleSideKeyCount, value);
        }
        
        [Option(LabelKey = nameof(KeysSliderLabel), Min = 1, Max = 18, UIType = UIType.Slider, DataType = typeof(double))]
        public double SingleSideKeyCount
        {
            get => _singleSideKeyCount;
            set => SetProperty(ref _singleSideKeyCount, value);
        }
        private double _singleSideKeyCount = 10;
        private bool _modifySingleSideKeyCount;
        
#region 左手区
        [Option(LabelKey = nameof(DPMirrorLabel), TooltipKey = nameof(DPMirrorTooltipLeft), UIType = UIType.Toggle)]
        public bool LMirror
        {
            get => _lMirror;
            set => SetProperty(ref _lMirror, value);
        }
        private bool _lMirror;

        [Option(LabelKey = nameof(DPDensityLabel), TooltipKey = nameof(DPDensityTooltipLeft), UIType = UIType.Toggle)]
        public bool LDensity
        {
            get => _lDensity;
            set => SetProperty(ref _lDensity, value);
        }
        private bool _lDensity;

        [Option(LabelKey = nameof(RemoveLabel), TooltipKey = nameof(RemoveTooltip), UIType = UIType.Toggle)]
        public bool LRemove
        {
            get => _lRemove;
            set => SetProperty(ref _lRemove, value);
        }
        private bool _lRemove;
        
        [Option(LabelKey = nameof(DPLeftMaxKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(double))]
        public double LMaxKeys
        {
            get => _lMaxKeys;
            set => SetProperty(ref _lMaxKeys, value);
        }
        private double _lMaxKeys = 5;

        [Option(LabelKey = nameof(DPLeftMinKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(double))]
        public double LMinKeys
        {
            get => _lMinKeys;
            set => SetProperty(ref _lMinKeys, value);
        }
        private double _lMinKeys = 1;
        
#endregion

#region 右手区
        [Option(LabelKey = nameof(DPMirrorLabel), TooltipKey = nameof(DPMirrorTooltipRight), UIType = UIType.Toggle)]
        public bool RMirror
        {
            get => _rMirror;
            set => SetProperty(ref _rMirror, value);
        }
        private bool _rMirror;

        [Option(LabelKey = nameof(DPDensityLabel), TooltipKey = nameof(DPDensityTooltipRight), UIType = UIType.Toggle)]
        public bool RDensity
        {
            get => _rDensity;
            set => SetProperty(ref _rDensity, value);
        }
        private bool _rDensity;

        [Option(LabelKey = nameof(RemoveLabel), TooltipKey = nameof(RemoveTooltip), UIType = UIType.Toggle)]
        public bool RRemove
        {
            get => _rRemove;
            set => SetProperty(ref _rRemove, value);
        }
        private bool _rRemove;
        
        [Option(LabelKey = nameof(DPRightMaxKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(double))]
        public double RMaxKeys
        {
            get => _rMaxKeys;
            set => SetProperty(ref _rMaxKeys, value);
        }
        private double _rMaxKeys = 5;

        [Option(LabelKey = nameof(DPRightMinKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider, DataType = typeof(double))]
        public double RMinKeys
        {
            get => _rMinKeys;
            set => SetProperty(ref _rMinKeys, value);
        }
        private double _rMinKeys = 1;
        #endregion
        // 设置约束, 以后走接口实现
        public new void Validate()
        {
            base.Validate(); // First clamp to Min/Max
            IsValidating = true;
            if (LMinKeys > LMaxKeys) LMinKeys = LMaxKeys;
            if (RMinKeys > RMaxKeys) RMinKeys = RMaxKeys;
            IsValidating = false;
        }
    }
}
