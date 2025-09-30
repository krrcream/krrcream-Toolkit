using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using krrTools.Core;

namespace krrTools.Managers
{
    /// <summary>
    /// 插件管理器 - 负责动态加载外部模块插件
    /// </summary>
    public static class PluginManager
    {
        private static readonly List<IToolModule> _loadedPlugins = new();
        private static readonly HashSet<string> _loadedAssemblies = new();

        /// <summary>
        /// 获取所有已加载的插件模块
        /// </summary>
        public static IReadOnlyList<IToolModule> LoadedPlugins => _loadedPlugins.AsReadOnly();

        /// <summary>
        /// 从指定目录加载插件
        /// </summary>
        /// <param name="pluginDirectory">插件目录路径</param>
        public static void LoadPluginsFromDirectory(string pluginDirectory)
        {
            if (!Directory.Exists(pluginDirectory))
            {
                Debug.WriteLine($"Plugin directory does not exist: {pluginDirectory}");
                return;
            }

            var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll");
            foreach (var dllFile in dllFiles)
            {
                LoadPluginFromAssembly(dllFile);
            }
        }

        /// <summary>
        /// 从程序集文件加载插件
        /// </summary>
        /// <param name="assemblyPath">程序集文件路径</param>
        public static void LoadPluginFromAssembly(string assemblyPath)
        {
            try
            {
                if (!File.Exists(assemblyPath))
                {
                    Debug.WriteLine($"Plugin assembly does not exist: {assemblyPath}");
                    return;
                }

                var assemblyName = Path.GetFileName(assemblyPath);
                if (_loadedAssemblies.Contains(assemblyName))
                {
                    Debug.WriteLine($"Plugin assembly already loaded: {assemblyName}");
                    return;
                }

                var assembly = Assembly.LoadFrom(assemblyPath);
                _loadedAssemblies.Add(assemblyName);

                var moduleTypes = assembly.GetTypes()
                    .Where(t => typeof(IToolModule).IsAssignableFrom(t) &&
                                t is { IsAbstract: false, IsInterface: false } &&
                                t.GetConstructor(Type.EmptyTypes) != null);

                foreach (var moduleType in moduleTypes)
                {
                    try
                    {
                        var module = (IToolModule)Activator.CreateInstance(moduleType)!;
                        _loadedPlugins.Add(module);

                        // 自动注册到ToolModuleRegistry
                        ToolModuleRegistry.RegisterModule(module);

                        Debug.WriteLine($"Loaded plugin module: {module.DisplayName} ({module.ModuleName})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to instantiate plugin module {moduleType.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load plugin assembly {assemblyPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public static void UnloadAllPlugins()
        {
            foreach (var plugin in _loadedPlugins)
            {
                ToolModuleRegistry.UnregisterModule(plugin);
            }

            _loadedPlugins.Clear();
            _loadedAssemblies.Clear();
        }

        /// <summary>
        /// 获取插件目录路径（相对于应用程序目录）
        /// </summary>
        public static string GetDefaultPluginDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, "plugins");
        }
    }
}