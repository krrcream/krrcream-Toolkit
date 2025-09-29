using System.Collections.Generic;
using System.Diagnostics;
using krrTools.Configuration;
using krrTools.Core.Interfaces;
using krrTools.Core.Scheduling;
using krrTools.Tools.DPtool;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.N2NC;

namespace krrTools.Core.Modules
{
    /// <summary>
    /// 工具模块注册表
    /// </summary>
    public static class ToolModuleRegistry
    {
        private static readonly Dictionary<ToolModuleType, IToolModule> _modules = new();
        private static readonly ToolScheduler _toolScheduler = new();

        static ToolModuleRegistry()
        {
            // 注册所有模块
            RegisterModule(new N2NCModule());
            RegisterModule(new DPModule());
            RegisterModule(new KRRLNModule());

            // 将所有模块的工具注册到调度器
            foreach (var module in _modules.Values)
            {
                _toolScheduler.RegisterTool(module.CreateTool());
            }
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        public static void RegisterModule(IToolModule module)
        {
            if (_modules.TryAdd(module.ModuleType, module))
            {
                _toolScheduler.RegisterTool(module.CreateTool());
                Debug.WriteLine($"Registered module: {module.DisplayName}");
            }
        }

        /// <summary>
        /// 注销模块
        /// </summary>
        public static void UnregisterModule(IToolModule module)
        {
            if (_modules.Remove(module.ModuleType))
            {
                // 注意：ToolScheduler目前不支持注销工具
                // _toolScheduler.UnregisterTool(module.CreateTool());
                Debug.WriteLine($"Unregistered module: {module.DisplayName}");
            }
        }

        /// <summary>
        /// 获取所有模块
        /// </summary>
        public static IEnumerable<IToolModule> GetAllModules() => _modules.Values;
    }

    /// <summary>
    /// 工具模块接口
    /// </summary>
    public interface IToolModule
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