using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace krrTools.Core
{
    /// <summary>
    /// 模块管理器实现
    /// </summary>
    public class ModuleManager : IModuleManager
    {
        private readonly Dictionary<ToolModuleType, IToolModule> _modules = new();
        private readonly Dictionary<string, ITool> _tools = new();

        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 构造函数，通过 DI 注入所有模块
        /// </summary>
        public ModuleManager(IEnumerable<IToolModule> modules, IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            foreach (var module in modules) RegisterModule(module);
        }

        /// <summary>
        /// 获取所有模块
        /// </summary>
        public IEnumerable<IToolModule> GetAllModules()
        {
            return _modules.Values;
        }

        /// <summary>
        /// 注册模块
        /// </summary>
        public void RegisterModule(IToolModule module)
        {
            if (_modules.TryAdd(module.ModuleType, module))
                // Console.WriteLine($"[INFO] 注册模块: {module.DisplayName}");
                RegisterTool(module.CreateTool());
        }

        /// <summary>
        /// 注销模块
        /// </summary>
        public void UnregisterModule(IToolModule module)
        {
            if (_modules.Remove(module.ModuleType))
            {
                Console.WriteLine($"[INFO] 注销模块: {module.DisplayName}");
                _tools.Remove(module.CreateTool().Name);
            }
        }

        /// <summary>
        /// 注册工具
        /// </summary>
        /// <param name="tool">工具实例</param>
        private void RegisterTool(ITool tool)
        {
            _tools[tool.Name] = tool;
        }

        /// <summary>
        /// 根据名称获取工具
        /// </summary>
        public ITool? GetToolName(string toolName)
        {
            return _tools.GetValueOrDefault(toolName);
        }

        /// <summary>
        /// 通过DI容器发现所有已注册的IToolModule实现
        /// </summary>
        public static IEnumerable<IToolModule> DiscoverModules(IServiceProvider serviceProvider)
        {
            // 从DI容器获取所有已注册的IToolModule实例
            return serviceProvider.GetServices<IToolModule>();
        }
    }
}