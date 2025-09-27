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

        // 滑条上下限定义为常量，不保存在配置文件中
        public const int MinKeysLimit = 0;
        public const int MaxKeysLimit = 5;

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

        // 移除MinKeys和MaxKeys属性，这些应该是UI中的常量，不应该保存在配置文件中
        // 用户设置的默认值应该在ViewModel中定义

        public override void Validate()
        {
            // 不再需要验证MinKeys/MaxKeys，因为它们不再是可配置的属性
        }
    }
}
