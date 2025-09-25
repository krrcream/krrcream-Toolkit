using krrTools.tools.Shared;

namespace krrTools.tools.DPtool
{
    /// <summary>
    /// DP工具选项类，用于封装所有DP参数
    /// </summary>
    public class DPToolOptions : ObservableObject
    {
        private bool _modifySingleSideKeyCount;
        private int _singleSideKeyCount = 5;

        // Use SideOptions to hold left/right specific settings (mirror/density/min/max)
        public SideOptions Left { get; } = new SideOptions();
        public SideOptions Right { get; } = new SideOptions();

        /// <summary>
        /// 是否修改单侧按键数量
        /// </summary>
        public bool ModifySingleSideKeyCount
        {
            get => _modifySingleSideKeyCount;
            set => SetProperty(ref _modifySingleSideKeyCount, value);
        }

        /// <summary>
        /// 单侧按键数量 (1-16 recommended clamp)
        /// </summary>
        public int SingleSideKeyCount
        {
            get => _singleSideKeyCount;
            set
            {
                // Clamp to a reasonable range to avoid invalid states in UI/logic
                int newVal = value < 1 ? 1 : value;
                if (newVal > 16) newVal = 16;
                if (SetProperty(ref _singleSideKeyCount, newVal))
                {
                    // keep consistency with side options (ensure min/max sensible)
                    Left.Validate();
                    Right.Validate();
                }
            }
        }

        // Legacy wrapper properties - keep names for existing bindings/code
        public bool LMirror
        {
            get => Left.Mirror;
            set => Left.Mirror = value;
        }

        public bool LDensity
        {
            get => Left.Density;
            set => Left.Density = value;
        }

        public int LMaxKeys
        {
            get => Left.MaxKeys;
            set => Left.MaxKeys = value;
        }

        public int LMinKeys
        {
            get => Left.MinKeys;
            set => Left.MinKeys = value;
        }

        public bool RMirror
        {
            get => Right.Mirror;
            set => Right.Mirror = value;
        }

        public bool RDensity
        {
            get => Right.Density;
            set => Right.Density = value;
        }

        public int RMaxKeys
        {
            get => Right.MaxKeys;
            set => Right.MaxKeys = value;
        }

        public int RMinKeys
        {
            get => Right.MinKeys;
            set => Right.MinKeys = value;
        }
    }
}
