using krrTools.Bindable;
using krrTools.Core;
using static krrTools.Localization.Strings;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP工具选项类，用于封装所有DP参数
    /// </summary>
    public class DPToolOptions : ToolOptionsBase
    {
        [Option(LabelKey = nameof(DPKeysSliderLabel), Min = 1, Max = 10, UIType = UIType.Slider, IsRefresher = true)]
        public Bindable<double?> ModifyKeys { get; } = new();        
        
        #region 左手区

        [Option(LabelKey = nameof(DPMirrorLabel), TooltipKey = nameof(DPMirrorTooltipLeft), UIType = UIType.Toggle, IsRefresher = true)]
        public Bindable<bool> LMirror { get; } = new Bindable<bool>();

        [Option(LabelKey = nameof(DPDensityLabel), TooltipKey = nameof(DPDensityTooltipLeft), UIType = UIType.Toggle, IsRefresher = true)]
        public Bindable<bool> LDensity { get; } = new Bindable<bool>();

        [Option(LabelKey = nameof(RemoveLabel), TooltipKey = nameof(RemoveTooltip), UIType = UIType.Toggle, IsRefresher = true)]
        public Bindable<bool> LRemove { get; } = new Bindable<bool>();

        [Option(LabelKey = nameof(DPLeftMaxKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider,
            DataType = typeof(double), IsRefresher = true)]
        public Bindable<double> LMaxKeys { get; } = new Bindable<double>(5);

        [Option(LabelKey = nameof(DPLeftMinKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider,
            DataType = typeof(double), IsRefresher = true)]
        public Bindable<double> LMinKeys { get; } = new Bindable<double>(1);

        #endregion

        #region 右手区

        [Option(LabelKey = nameof(DPMirrorLabel), TooltipKey = nameof(DPMirrorTooltipRight), UIType = UIType.Toggle, IsRefresher = true)]
        public Bindable<bool> RMirror { get; } = new Bindable<bool>();

        [Option(LabelKey = nameof(DPDensityLabel), TooltipKey = nameof(DPDensityTooltipRight), UIType = UIType.Toggle, IsRefresher = true)]
        public Bindable<bool> RDensity { get; } = new Bindable<bool>();

        [Option(LabelKey = nameof(RemoveLabel), TooltipKey = nameof(RemoveTooltip), UIType = UIType.Toggle, IsRefresher = true)]
        public Bindable<bool> RRemove { get; } = new Bindable<bool>();

        [Option(LabelKey = nameof(DPRightMaxKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider,
            DataType = typeof(double), IsRefresher = true)]
        public Bindable<double> RMaxKeys { get; } = new Bindable<double>(5);

        [Option(LabelKey = nameof(DPRightMinKeysTemplate), Min = 1, Max = 5, UIType = UIType.Slider,
            DataType = typeof(double), IsRefresher = true)]
        public Bindable<double> RMinKeys { get; } = new Bindable<double>(1);

        #endregion

        public DPToolOptions()
        {
            // Wire up property changed events for Bindable<T> properties
            ModifyKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ModifyKeys));
            LMirror.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LMirror));
            LDensity.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LDensity));
            LRemove.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LRemove));
            LMaxKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LMaxKeys));
            LMinKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LMinKeys));
            RMirror.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RMirror));
            RDensity.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RDensity));
            RRemove.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RRemove));
            RMaxKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RMaxKeys));
            RMinKeys.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RMinKeys));
        }

        // 设置约束, 以后走接口实现
        public override void Validate()
        {
            base.Validate(); // First clamp to Min/Max
            IsValidating = true;

            // 左手键数约束
            if (LMinKeys.Value < 1) LMinKeys.Value = 1;
            if (LMaxKeys.Value > 5) LMaxKeys.Value = 5;
            if (LMinKeys.Value > LMaxKeys.Value) LMinKeys.Value = LMaxKeys.Value;

            // 右手键数约束
            if (RMinKeys.Value < 1) RMinKeys.Value = 1;
            if (RMaxKeys.Value > 5) RMaxKeys.Value = 5;
            if (RMinKeys.Value > RMaxKeys.Value) RMinKeys.Value = RMaxKeys.Value;

            IsValidating = false;
        }
    }
}