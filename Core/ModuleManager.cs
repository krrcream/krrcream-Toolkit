using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace krrTools.Core
{
    /// <summary>
    /// 模块管理器实现
    /// </summary>
    public class ModuleManager : IModuleManager
    {
        private readonly ConcurrentDictionary<ToolModuleType, IToolModule> _modules = new();
        private readonly ConcurrentDictionary<string, IToolModule> _tools = new();

        /// <summary>
        /// 构造函数，通过 DI 注入所有模块
        /// </summary>
        public ModuleManager(IEnumerable<IToolModule> modules)
        {
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
                RegisterTool(module);
        }

        /// <summary>
        /// 注册工具
        /// </summary>
        /// <param name="tool">工具实例</param>
        private void RegisterTool(IToolModule tool)
        {
            _tools[tool.ModuleName] = tool;
        }
        
        /// <summary>
        /// 根据名称获取工具
        /// </summary>
        public IToolModule? GetToolByName(string toolName)
        {
            return _tools.GetValueOrDefault(toolName);
        }
        
        /// <summary>
        /// 注销模块
        /// </summary>
        public void UnregisterModule(IToolModule module)
        {
            if (_modules.TryRemove(module.ModuleType, out var dummy))
            {
                _tools.TryRemove(module.ModuleName, out _);
            }
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