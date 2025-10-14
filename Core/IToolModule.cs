using System;

namespace krrTools.Core
{
    /// <summary>
    /// 工具控件接口
    /// </summary>
    public interface IToolControl
    {
        // 标记接口，用于类型安全
    }

    /// <summary>
    /// 工具视图模型接口
    /// </summary>
    public interface IToolViewModel
    {
        // 标记接口，用于类型安全
    }

    /// <summary>
    /// 工具模块接口，包含元信息和工厂方法
    /// </summary>
    public interface IToolModule
    {
        /// <summary>
        /// 选项类型
        /// </summary>
        Type OptionsType { get; }

        ToolModuleType ModuleType { get; }
        string ModuleName { get; }
        string DisplayName { get; }

        // 工厂方法
        IToolOptions CreateDefaultOptions();
        ITool CreateTool();
        IToolControl CreateControl();
        IToolViewModel CreateViewModel();
    }
}