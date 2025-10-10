using System;
using krrTools.Configuration;

namespace krrTools.Core
{
    /// <summary>
    /// 工具模块元信息接口
    /// </summary>
    public interface IToolModuleInfo
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
    }

    /// <summary>
    /// 工具模块工厂接口
    /// </summary>
    public interface IToolFactory
    {
        ITool CreateTool();
        object CreateControl();
        object CreateViewModel();
    }

    /// <summary>
    /// 工具模块接口，组合元信息和工厂
    /// </summary>
    public interface IToolModule : IToolModuleInfo, IToolFactory
    {
    }
}