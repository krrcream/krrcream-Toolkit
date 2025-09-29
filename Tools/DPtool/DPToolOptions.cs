using krrTools.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// 侧边选项类，用于封装单侧的DP参数
    /// </summary>
    public class SideOptions : ObservableObject
    {
        private bool _mirror;
        private bool _density;
        private int _maxKeys = 5;
        private int _minKeys = 1;

        public bool Mirror
        {
            get => _mirror;
            set => SetProperty(ref _mirror, value);
        }

        public bool Density
        {
            get => _density;
            set => SetProperty(ref _density, value);
        }

        public int MaxKeys
        {
            get => _maxKeys;
            set => SetProperty(ref _maxKeys, value);
        }

        public int MinKeys
        {
            get => _minKeys;
            set => SetProperty(ref _minKeys, value);
        }

        public void Validate()
        {
            if (MaxKeys < 1) MaxKeys = 1;
            if (MaxKeys > 5) MaxKeys = 5;
            if (MinKeys < 1) MinKeys = 1;
            if (MinKeys > MaxKeys) MinKeys = MaxKeys;
        }
    }

    /// <summary>
    /// DP工具选项类，用于封装所有DP参数
    /// </summary>
    public class DPToolOptions : UnifiedToolOptions
    {
        private bool _modifySingleSideKeyCount;
        private int _singleSideKeyCount = 5;

        // Use SideOptions to hold left/right specific settings (mirror/density/min/max)
        public SideOptions Left { get; } = new SideOptions();
        public SideOptions Right { get; } = new SideOptions();

        private int _lMaxKeys = 5;
        private int _lMinKeys = 1;
        private int _rMaxKeys = 5;
        private int _rMinKeys = 1;
        private bool _lRemove;
        private bool _rRemove;

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
            set
            {
                if (Left.Mirror != value)
                {
                    Left.Mirror = value;
                    OnPropertyChanged(nameof(LMirror));
                }
            }
        }

        public bool LDensity
        {
            get => Left.Density;
            set
            {
                if (Left.Density != value)
                {
                    Left.Density = value;
                    OnPropertyChanged(nameof(LDensity));
                }
            }
        }

        public int LMaxKeys
        {
            get => _lMaxKeys;
            set => SetProperty(ref _lMaxKeys, value);
        }

        public int LMinKeys
        {
            get => _lMinKeys;
            set => SetProperty(ref _lMinKeys, value);
        }

        public bool LRemove
        {
            get => _lRemove;
            set => SetProperty(ref _lRemove, value);
        }

        public bool RMirror
        {
            get => Right.Mirror;
            set
            {
                if (Right.Mirror != value)
                {
                    Right.Mirror = value;
                    OnPropertyChanged(nameof(RMirror));
                }
            }
        }

        public bool RDensity
        {
            get => Right.Density;
            set
            {
                if (Right.Density != value)
                {
                    Right.Density = value;
                    OnPropertyChanged(nameof(RDensity));
                }
            }
        }

        public int RMaxKeys
        {
            get => _rMaxKeys;
            set => SetProperty(ref _rMaxKeys, value);
        }

        public int RMinKeys
        {
            get => _rMinKeys;
            set => SetProperty(ref _rMinKeys, value);
        }

        public bool RRemove
        {
            get => _rRemove;
            set => SetProperty(ref _rRemove, value);
        }

        /// <summary>
        /// 选中的预设
        /// </summary>
        public new void Validate()
        {
            Left.Validate();
            Right.Validate();
            if (SingleSideKeyCount < 1) SingleSideKeyCount = 1;
            if (SingleSideKeyCount > 16) SingleSideKeyCount = 16;
            // Also validate legacy wrapper properties
            if (LMaxKeys < 1) LMaxKeys = 1;
            if (LMaxKeys > 5) LMaxKeys = 5;
            if (LMinKeys < 1) LMinKeys = 1;
            if (LMinKeys > LMaxKeys) LMinKeys = LMaxKeys;
            if (RMaxKeys < 1) RMaxKeys = 1;
            if (RMaxKeys > 5) RMaxKeys = 5;
            if (RMinKeys < 1) RMinKeys = 1;
            if (RMinKeys > RMaxKeys) RMinKeys = RMaxKeys;
        }
    }
}
