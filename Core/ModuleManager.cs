using System;
using System.Collections.Generic;

namespace krrTools.Core;

/// <summary>
/// 模块管理器实现
/// </summary>
public class ModuleManager : IModuleManager
{
    private readonly Dictionary<ToolModuleType, IToolModule> _modules = new();
    private readonly Dictionary<string, ITool> _tools = new();

    /// <summary>
    /// 构造函数，通过 DI 注入所有模块
    /// </summary>
    public ModuleManager(IEnumerable<IToolModule> modules)
    {
        foreach (var module in modules) RegisterModule(module);
    }

    /// <summary>
    /// 构造函数，通过反射自动发现模块
    /// </summary>
    public ModuleManager() : this(DiscoverModules())
    {
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

    // /// <summary>
    // /// 获取已注册的工具列表
    // /// </summary>
    // public IEnumerable<ITool> GetRegisteredTools()
    // {
    //     return _tools.Values;
    // }

    /// <summary>
    /// 根据名称获取工具
    /// </summary>
    public ITool? GetToolName(string toolName)
    {
        return _tools.GetValueOrDefault(toolName);
    }

    /// <summary>
    /// 通过反射发现所有IToolModule实现
    /// </summary>
    private static IEnumerable<IToolModule> DiscoverModules()
    {
        var modules = new List<IToolModule>();
        var assembly = typeof(ModuleManager).Assembly;

        foreach (var type in assembly.GetTypes())
            if (typeof(IToolModule).IsAssignableFrom(type) && type is { IsAbstract: false, IsInterface: false })
                try
                {
                    var instance = Activator.CreateInstance(type);
                    if (instance is IToolModule module) modules.Add(module);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 创建模块实例失败: {type.FullName}, {ex.Message}");
                }

        return modules;
    }
}