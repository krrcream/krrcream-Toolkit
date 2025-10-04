using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.DPtool;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.N2NC;
using Microsoft.Extensions.DependencyInjection;

namespace krrTools.Managers
{
    /// <summary>
    /// 预设管理器 - 负责管理工具选项预设
    /// </summary>
    public static class PresetManager
    {
        private static readonly string _presetDirectory;
        private static readonly Dictionary<string, Dictionary<string, IToolOptions>> _presets = new();

        static PresetManager()
        {
            _presetDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "krrcream-Toolkit",
                "presets"
            );

            Directory.CreateDirectory(_presetDirectory);
            LoadAllPresets();
        }

        /// <summary>
        /// 保存预设
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="presetName">预设名称</param>
        /// <param name="options">工具选项</param>
        public static void SavePreset(string toolName, string presetName, IToolOptions options)
        {
            try
            {
                if (!_presets.ContainsKey(toolName))
                {
                    _presets[toolName] = new Dictionary<string, IToolOptions>();
                }

                _presets[toolName][presetName] = options;

                var presetFile = GetPresetFilePath(toolName, presetName);
                Directory.CreateDirectory(Path.GetDirectoryName(presetFile)!);

                var json = JsonSerializer.Serialize(options, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                });

                File.WriteAllText(presetFile, json);
                Logger.WriteLine(LogLevel.Information, "[PresetManager] Saved preset: {0}/{1}", toolName, presetName);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[PresetManager] Failed to save preset {0}/{1}: {2}", toolName, presetName, ex.Message);
            }
        }

        /// <summary>
        /// 加载预设
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="presetName">预设名称</param>
        /// <returns>工具选项，失败返回null</returns>
        public static IToolOptions? LoadPreset(string toolName, string presetName)
        {
            try
            {
                if (_presets.TryGetValue(toolName, out var toolPresets) &&
                    toolPresets.TryGetValue(presetName, out var options))
                {
                    return options;
                }

                var presetFile = GetPresetFilePath(toolName, presetName);
                if (!File.Exists(presetFile))
                    return null;

                var json = File.ReadAllText(presetFile);
                var toolType = GetToolTypeFromName(toolName);
                if (toolType == null)
                    return null;

                var optionsType = GetOptionsTypeForTool(toolType);
                if (optionsType == null)
                    return null;

                var loadedOptions = (IToolOptions?)JsonSerializer.Deserialize(json, optionsType, new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                });

                if (loadedOptions != null)
                {
                    if (!_presets.ContainsKey(toolName))
                    {
                        _presets[toolName] = new Dictionary<string, IToolOptions>();
                    }
                    _presets[toolName][presetName] = loadedOptions;
                }

                return loadedOptions;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[PresetManager] Failed to load preset {0}/{1}: {2}", toolName, presetName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 删除预设
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="presetName">预设名称</param>
        public static void DeletePreset(string toolName, string presetName)
        {
            try
            {
                if (_presets.TryGetValue(toolName, out var toolPresets))
                {
                    toolPresets.Remove(presetName);
                }

                var presetFile = GetPresetFilePath(toolName, presetName);
                if (File.Exists(presetFile))
                {
                    File.Delete(presetFile);
                }

                Logger.WriteLine(LogLevel.Information, "[PresetManager] Deleted preset: {0}/{1}", toolName, presetName);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[PresetManager] Failed to delete preset {0}/{1}: {2}", toolName, presetName, ex.Message);
            }
        }

        /// <summary>
        /// 获取工具的所有预设名称
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>预设名称列表</returns>
        public static IEnumerable<string> GetPresetNames(string toolName)
        {
            if (_presets.TryGetValue(toolName, out var toolPresets))
            {
                return toolPresets.Keys;
            }
            return [];
        }

        /// <summary>
        /// 获取所有工具的预设信息
        /// </summary>
        /// <returns>工具名称到预设名称列表的映射</returns>
        public static Dictionary<string, List<string>> GetAllPresets()
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var kvp in _presets)
            {
                result[kvp.Key] = new List<string>(kvp.Value.Keys);
            }
            return result;
        }

        /// <summary>
        /// 检查预设是否存在
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="presetName">预设名称</param>
        /// <returns>是否存在</returns>
        public static bool PresetExists(string toolName, string presetName)
        {
            return _presets.TryGetValue(toolName, out var toolPresets) &&
                   toolPresets.ContainsKey(presetName);
        }

        private static void LoadAllPresets()
        {
            try
            {
                if (!Directory.Exists(_presetDirectory))
                    return;

                foreach (var toolDir in Directory.GetDirectories(_presetDirectory))
                {
                    var toolName = Path.GetFileName(toolDir);
                    foreach (var presetFile in Directory.GetFiles(toolDir, "*.json"))
                    {
                        var presetName = Path.GetFileNameWithoutExtension(presetFile);
                        LoadPreset(toolName, presetName);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[PresetManager] Failed to load presets: {0}", ex.Message);
            }
        }

        private static string GetPresetFilePath(string toolName, string presetName)
        {
            return Path.Combine(_presetDirectory, toolName, $"{presetName}.json");
        }

        private static Type? GetToolTypeFromName(string toolName)
        {
            // 根据工具名称找到对应的模块类型
            var moduleManager = App.Services.GetRequiredService<IModuleManager>();
            foreach (var module in moduleManager.GetAllModules())
            {
                if (module.ModuleName == toolName)
                {
                    return module.GetType();
                }
            }
            return null;
        }

        private static Type? GetOptionsTypeForTool(Type toolType)
        {
            // 根据工具类型推断选项类型
            // 这是一个简化的实现，实际可能需要更复杂的逻辑
            if (toolType.Name.Contains("N2NC"))
                return typeof(N2NCOptions);
            if (toolType.Name.Contains("DP"))
                return typeof(DPToolOptions);
            if (toolType.Name.Contains("KRRLN"))
                return typeof(KRRLNTransformerOptions);

            return null;
        }
    }
}