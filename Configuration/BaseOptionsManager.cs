using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using krrTools.Tools.DPtool;
// using krrTools.Tools.LNTransformer;
using krrTools.Tools.N2NC;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Utilities;

namespace krrTools.Configuration
{
    public static class BaseOptionsManager
    {
        // 统一的配置文件名
        public const string ConfigFileName = "config.json";

        // base application folder under LocalAppData (用于预设和管道)
        public const string BaseAppFolderName = "krrTools";
        public const string PresetsFolderName = "presets";
        public const string PipelinesFolderName = "pipelines";

        // tool identifiers
        public const string N2NCToolName = "Converter";
        public const string DPToolName = "DPTool";
        // public const string YLsLNToolName = "YLsLNTransformer";
        public const string KRRsLNToolName = "KRRsLNTransformer";

        // Add tool identifiers for simple tabs
        public const string LVCalToolName = "LVCalculator";
        public const string FilesManagerToolName = "FilesManager";

        // DP specific constants
        public const string DPCreatorPrefix = "Krr DP. & ";
        public const string DPDefaultTag = "krrcream's converter DP";

        // KRR LN specific constants
        public const string KRRLNCreatorPrefix = "Krr LN. & ";
        public const string KRRLNDefaultTag = "krrcream's transformer LN";
        
        // 统一的配置文件路径 (exe 所在文件夹)
        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        // 缓存的配置实例
        private static AppConfig? _cachedConfig;
        private static readonly Lock _configLock = new Lock();

        // 预设和管道使用的文件夹路径
        private static string BaseFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), BaseAppFolderName);

        private static string GetToolFolder(string toolName)
        {
            var folder = Path.Combine(BaseFolder, toolName);
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// 加载统一的应用程序配置
        /// </summary>
        private static AppConfig LoadConfig()
        {
            lock (_configLock)
            {
                if (_cachedConfig != null) return _cachedConfig;

                string path = ConfigFilePath;
                if (!File.Exists(path))
                {
                    _cachedConfig = new AppConfig();
                    return _cachedConfig;
                }

                try
                {
                    string json = File.ReadAllText(path);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _cachedConfig = JsonSerializer.Deserialize<AppConfig>(json, opts) ?? new AppConfig();
                    
                    return _cachedConfig;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load config file '{path}': {ex.Message}");
                    _cachedConfig = new AppConfig();
                    return _cachedConfig;
                }
            }
        }

        /// <summary>
        /// 保存统一的应用程序配置
        /// </summary>
        private static void SaveConfig()
        {
            lock (_configLock)
            {
                if (_cachedConfig == null) return;

                string path = ConfigFilePath;
                try
                {
                    var opts = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
                    string json = JsonSerializer.Serialize(_cachedConfig, opts);
                    File.WriteAllText(path, json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to save config file '{path}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取指定工具的选项
        /// </summary>
        public static T? LoadOptions<T>(string toolName, string filename)
        {
            var config = LoadConfig();

            object? options = toolName switch
            {
                N2NCToolName => config.N2NC,
                DPToolName => config.DP,
                // YLsLNToolName => config.LNTransformer,
                KRRsLNToolName => config.KRRLNTransformer,
                _ => null
            };

            return options is T typedOptions ? typedOptions : default;
        }

        /// <summary>
        /// 保存指定工具的选项
        /// </summary>
        public static void SaveOptions<T>(string toolName, string filename, T options)
        {
            var config = LoadConfig();

            switch (toolName)
            {
                case N2NCToolName:
                    config.N2NC = options as N2NCOptions;
                    break;
                case DPToolName:
                    config.DP = options as DPToolOptions;
                    break;
                // case YLsLNToolName:
                //     config.LNTransformer = options as YLsLNTransformerOptions;
                //     break;
                case KRRsLNToolName:
                    config.KRRLNTransformer = options as KRRLNTransformerOptions;
                    break;
            }

            SaveConfig();
        }

        /// <summary>
        /// 获取实时预览设置
        /// </summary>
        public static bool GetRealTimePreview() => LoadConfig().RealTimePreview;

        /// <summary>
        /// 保存实时预览设置
        /// </summary>
        public static void SetRealTimePreview(bool value)
        {
            var config = LoadConfig();
            config.RealTimePreview = value;
            SaveConfig();
        }

        /// <summary>
        /// 获取应用程序主题设置
        /// </summary>
        public static string? GetApplicationTheme() => LoadConfig().ApplicationTheme;

        /// <summary>
        /// 保存应用程序主题设置
        /// </summary>
        public static void SetApplicationTheme(string? theme)
        {
            var config = LoadConfig();
            config.ApplicationTheme = theme;
            SaveConfig();
        }

        /// <summary>
        /// 获取窗口背景类型设置
        /// </summary>
        public static string? GetWindowBackdropType() => LoadConfig().WindowBackdropType;

        /// <summary>
        /// 保存窗口背景类型设置
        /// </summary>
        public static void SetWindowBackdropType(string? backdropType)
        {
            var config = LoadConfig();
            config.WindowBackdropType = backdropType;
            SaveConfig();
        }

        /// <summary>
        /// 获取是否更新主题色设置
        /// </summary>
        public static bool? GetUpdateAccent() => LoadConfig().UpdateAccent;

        /// <summary>
        /// 保存是否更新主题色设置
        /// </summary>
        public static void SetUpdateAccent(bool? updateAccent)
        {
            var config = LoadConfig();
            config.UpdateAccent = updateAccent;
            SaveConfig();
        }

        /// <summary>
        /// 获取是否强制中文设置
        /// </summary>
        public static bool? GetForceChinese() => LoadConfig().ForceChinese;

        /// <summary>
        /// 保存是否强制中文设置
        /// </summary>
        public static void SetForceChinese(bool? forceChinese)
        {
            var config = LoadConfig();
            config.ForceChinese = forceChinese;
            SaveConfig();
        }

        // Preset helpers: save preset by name (filename-safe) and list available presets
        public static void SavePreset<T>(string toolName, string presetName, T options)
        {
            string folder = GetToolFolder(Path.Combine(toolName, PresetsFolderName));
            Directory.CreateDirectory(folder);
            string safe = MakeSafeFilename(presetName) + ".json";
            string path = Path.Combine(folder, safe);
            var opts = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            string json = JsonSerializer.Serialize(options, opts);
            File.WriteAllText(path, json);
        }

        public static IEnumerable<(string Name, T? Options)> LoadPresets<T>(string toolName)
        {
            string folder = GetToolFolder(Path.Combine(toolName, PresetsFolderName));
            if (!Directory.Exists(folder)) yield break;
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name)) continue; // Skip presets with empty names
                T? opt = default;
                try
                {
                    string json = File.ReadAllText(file);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    opt = JsonSerializer.Deserialize<T>(json, opts);
                }
                catch (Exception ex)
                {
                    // Swallow malformed preset but log for diagnostics
                    Debug.WriteLine($"Failed to load preset '{file}': {ex.Message}");
                }
                yield return (name, opt);
            }
        }

        private static string MakeSafeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        // Pipeline preset helpers
        public static void SavePipelinePreset(string presetName, PipelineOptions pipelineOptions)
        {
            string folder = GetToolFolder(PipelinesFolderName);
            Directory.CreateDirectory(folder);
            string safe = MakeSafeFilename(presetName) + ".json";
            string path = Path.Combine(folder, safe);
            var opts = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            string json = JsonSerializer.Serialize(pipelineOptions, opts);
            File.WriteAllText(path, json);
        }

        public static IEnumerable<(string Name, PipelineOptions? Options)> LoadPipelinePresets()
        {
            string folder = GetToolFolder(PipelinesFolderName);
            if (!Directory.Exists(folder)) yield break;
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name)) continue;
                PipelineOptions? opt = null;
                try
                {
                    string json = File.ReadAllText(file);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    opt = JsonSerializer.Deserialize<PipelineOptions>(json, opts);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load pipeline preset '{file}': {ex.Message}");
                }
                yield return (name, opt);
            }
        }

        public static void DeletePipelinePreset(string presetName)
        {
            string folder = GetToolFolder(PipelinesFolderName);
            string safe = MakeSafeFilename(presetName) + ".json";
            string path = Path.Combine(folder, safe);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}