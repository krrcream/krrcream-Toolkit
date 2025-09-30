using System.Collections.Generic;
using krrTools.Configuration;

namespace krrTools.Core
{
    /// <summary>
    /// 工具模块注册表（兼容性层，使用 IModuleManager 替代）
    /// </summary>
    public static class ToolModuleRegistry
    {
        private static IModuleManager? _moduleManager;

        /// <summary>
        /// 初始化注册表（由 DI 容器调用）
        /// </summary>
        public static void Initialize(IModuleManager moduleManager)
        {
            _moduleManager = moduleManager;
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        public static void RegisterModule(IToolModule module)
        {
            _moduleManager?.RegisterModule(module);
        }

        /// <summary>
        /// 注销模块
        /// </summary>
        public static void UnregisterModule(IToolModule module)
        {
            _moduleManager?.UnregisterModule(module);
        }

        /// <summary>
        /// 获取所有模块
        /// </summary>
        public static IEnumerable<IToolModule> GetAllModules() => _moduleManager?.GetAllModules() ?? [];
    }

    /// <summary>
    /// 工具模块接口
    /// </summary>
    public partial interface IToolModule
    {
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
        OsuParsers.Beatmaps.Beatmap? ProcessBeatmapWithOptions(OsuParsers.Beatmaps.Beatmap input, IToolOptions options);
    }
}