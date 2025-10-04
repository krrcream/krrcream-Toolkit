using System;
using krrTools.Configuration;
using OsuParsers.Beatmaps;

namespace krrTools.Core
{
    /// <summary>
    /// 工具模块接口，用于自动注册
    /// </summary>
    public interface IToolModule
    {
        /// <summary>
        /// 枚举值
        /// </summary>
        object EnumValue { get; }

        /// <summary>
        /// 选项类型
        /// </summary>
        Type OptionsType { get; }

        ToolModuleType ModuleType { get; }
        string ModuleName { get; }
        string DisplayName { get; }
        IToolOptions CreateDefaultOptions();
        ITool CreateTool();
        object CreateControl();
        object CreateViewModel();

        /// <summary>
        /// 非泛型版本：处理Beatmap（用于GenericTool）
        /// </summary>
        Beatmap? ProcessBeatmapWithOptions(Object input, IToolOptions options);
    }
}