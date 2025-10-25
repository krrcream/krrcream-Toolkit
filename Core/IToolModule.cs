using krrTools.Beatmaps;

namespace krrTools.Core
{
    /// <summary>
    /// 工具模块接口，包含元信息和工厂方法
    /// </summary>
    public interface IToolModule : IApplyToBeatmap
    {
        ToolModuleType ModuleType { get; }
        string ModuleName { get; }
        string DisplayName { get; }

        // // 工厂方法,用于插件支持
        // IToolOptions CreateDefaultOptions();
        // IToolControl CreateControl();
        // IToolViewModel CreateViewModel();
        // Type OptionsType { get; }
    }
}
