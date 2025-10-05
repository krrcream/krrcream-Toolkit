using System.Collections.Generic;

namespace krrTools.Configuration
{
    /// <summary>
    /// 统一的应用程序配置类，包含所有工具的设置
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 转换器工具设置
        /// </summary>
        public Dictionary<ConverterEnum, object?> Converters { get; set; } = new();

        /// <summary>
        /// 其他模块设置
        /// </summary>
        public Dictionary<ModuleEnum, object?> Modules { get; set; } = new();

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
        public bool ForceChinese { get; set; }
    }
}