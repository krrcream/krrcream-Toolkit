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
        /// 工具预设，按工具名称分组
        /// </summary>
        public Dictionary<string, Dictionary<string, object?>> Presets { get; set; } = new();

        /// <summary>
        /// 管道预设
        /// </summary>
        public Dictionary<string, PipelineOptions> PipelinePresets { get; set; } = new();

        /// <summary>
        /// 全局设置
        /// </summary>
        public GlobalSettings GlobalSettings { get; set; } = new();
    }

    /// <summary>
    /// 全局应用程序设置类
    /// </summary>
    public class GlobalSettings
    {
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
        public bool UpdateAccent { get; set; }

        /// <summary>
        /// 是否强制中文设置
        /// </summary>
        public bool ForceChinese { get; set; }

        /// <summary>
        /// osu! Songs文件夹路径
        /// </summary>
        public string SongsPath { get; set; } = string.Empty;

        /// <summary>
        /// N2NC转换快捷键
        /// </summary>
        public string? N2NCHotkey { get; set; } = "Ctrl+Shift+N";

        /// <summary>
        /// DP转换快捷键
        /// </summary>
        public string? DPHotkey { get; set; } = "Ctrl+Shift+D";

        /// <summary>
        /// KRRLN转换快捷键
        /// </summary>
        public string? KRRLNHotkey { get; set; } = "Ctrl+Shift+K";
    }
}