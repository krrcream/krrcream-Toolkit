using krrTools.tools.DPtool;

namespace krrTools.tools.Shared
{
    // Interface for tool options to support unified handling across different tools
    public interface IToolOptions
    {
        void Validate();
    }

    // Base class for tool option objects that need validation/notifications
    public abstract class ToolOptionsBase : ObservableObject, IToolOptions
    {
        /// <summary>
        /// Validate and normalize option values (called by UI or callers before use)
        /// Default implementation does nothing.
        /// </summary>
        public virtual void Validate() { }
    }

    /// <summary>
    /// Options common to a single hand/side (used by DP tool to represent left/right)
    /// </summary>
    public class SideOptions : ToolOptionsBase
    {
        private bool _mirror;
        private bool _density;
        private int _maxKeys = 5;
        private int _minKeys;

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
            set
            {
                if (value < 0) value = 0;
                if (value < _minKeys)
                {
                    _minKeys = value;
                    OnPropertyChanged(nameof(MinKeys));
                }
                SetProperty(ref _maxKeys, value);
            }
        }

        public int MinKeys
        {
            get => _minKeys;
            set
            {
                if (value < 0) value = 0;
                if (value > _maxKeys)
                {
                    _maxKeys = value;
                    OnPropertyChanged(nameof(MaxKeys));
                }
                SetProperty(ref _minKeys, value);
            }
        }

        public override void Validate()
        {
            if (_minKeys < 0) _minKeys = 0;
            if (_maxKeys < 0) _maxKeys = 0;
            if (_minKeys > _maxKeys) _minKeys = _maxKeys;
        }
    }
}
