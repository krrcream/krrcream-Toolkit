using System.Collections.Generic;

namespace krrTools.Core
{
    /// <summary>
    /// 模块管理器接口
    /// </summary>
    public interface IModuleManager
    {
        /// <summary>
        /// 获取所有模块
        /// </summary>
        IEnumerable<IToolModule> GetAllModules();

        /// <summary>
        /// 注册模块
        /// </summary>
        void RegisterModule(IToolModule module);

        /// <summary>
        /// 注销模块
        /// </summary>
        void UnregisterModule(IToolModule module);
    }
}