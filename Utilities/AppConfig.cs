using krrTools.Tools.DPtool;
// using krrTools.Tools.LNTransformer;
using krrTools.Tools.N2NC;
using krrTools.Tools.KRRLNTransformer;

namespace krrTools.Utilities
{
    /// <summary>
    /// 统一的应用程序配置类，包含所有工具的设置
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// N2NC 转换器设置
        /// </summary>
        public N2NCOptions? N2NC { get; set; }

        /// <summary>
        /// DP 工具设置
        /// </summary>
        public DPToolOptions? DP { get; set; }

        // /// <summary>
        // /// LN 转换器设置
        // /// </summary>
        // public YLsLNTransformerOptions? LNTransformer { get; set; }

        /// <summary>
        /// KRRLN 转换器设置
        /// </summary>
        public KRRLNTransformerOptions? KRRLNTransformer { get; set; }

        /// <summary>
        /// 实时预览设置
        /// </summary>
        public bool RealTimePreview { get; set; }

        /// <summary>
        /// 应用程序主题设置
        /// </summary>
        public string? ApplicationTheme { get; set; }

        /// <summary>
        /// 窗口背景类型设置
        /// </summary>
        public string? WindowBackdropType { get; set; }

        /// <summary>
        /// 是否更新主题色设置
        /// </summary>
        public bool? UpdateAccent { get; set; }

        /// <summary>
        /// 是否强制中文设置
        /// </summary>
        public bool? ForceChinese { get; set; }
    }
}