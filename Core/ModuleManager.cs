using System.Collections.Generic;
using System.Diagnostics;

namespace krrTools.Core
{
    /// <summary>
    /// 模块管理器实现
    /// </summary>
    public class ModuleManager : IModuleManager
    {
        private readonly Dictionary<ToolModuleType, IToolModule> _modules = new();

        /// <summary>
        /// 构造函数，通过 DI 注入所有模块
        /// </summary>
        public ModuleManager(IEnumerable<IToolModule> modules)
        {
            foreach (var module in modules)
            {
                RegisterModule(module);
            }
        }

        /// <summary>
        /// 获取所有模块
        /// </summary>
        public IEnumerable<IToolModule> GetAllModules() => _modules.Values;

        /// <summary>
        /// 注册模块
        /// </summary>
        public void RegisterModule(IToolModule module)
        {
            if (_modules.TryAdd(module.ModuleType, module))
            {
                Debug.WriteLine($"Registered module: {module.DisplayName}");
            }
        }

        /// <summary>
        /// 注销模块
        /// </summary>
        public void UnregisterModule(IToolModule module)
        {
            if (_modules.Remove(module.ModuleType))
            {
                Debug.WriteLine($"Unregistered module: {module.DisplayName}");
            }
        }
    }
}