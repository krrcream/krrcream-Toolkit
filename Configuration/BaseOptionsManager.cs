using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace krrTools.Configuration
{
    public static class BaseOptionsManager
    {
        // 统一的配置文件名
        private const string ConfigFileName = "config.json";

        // base application folder under LocalAppData (用于预设和管道)
        public const string BaseAppFolderName = "krrTools";
        public const string PresetsFolderName = "presets";
        public const string PipelinesFolderName = "pipelines";

        // DP specific constants
        public const string DPDefaultTag = "krrcream's converter DP";

        // LN specific constants
        public const string KRRLNDefaultTag = "krrcream's converter LN";

        // 统一的配置文件路径 (exe 所在文件夹)
        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        // 缓存的配置实例
        private static AppConfig? _cachedConfig;
        private static readonly Lock _configLock = new();

        // JSON 序列化选项缓存
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // 辅助方法：序列化
        private static string SerializeToJson<T>(T obj) => JsonSerializer.Serialize(obj, _jsonOpts);

        // 辅助方法：反序列化
        private static T? DeserializeFromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, _jsonOpts);

        // 控件类型映射缓存
        private static readonly Dictionary<string, Type?> _controlTypeCache = new();

        /// <summary>
        /// 获取控件类型 - 使用反射自动发现
        /// </summary>
        public static Type? GetControlType(string toolName)
        {
            if (_controlTypeCache.TryGetValue(toolName, out var cachedType))
            {
                return cachedType;
            }

            // 通过反射查找控件类型
            var assembly = typeof(BaseOptionsManager).Assembly;
            var controlType = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == $"{toolName}View" && typeof(System.Windows.Controls.Control).IsAssignableFrom(t));

            _controlTypeCache[toolName] = controlType;
            return controlType;
        }

        /// <summary>
        /// 获取源ID - 从枚举获取
        /// </summary>
        public static int GetSourceId(ConverterEnum converter)
        {
            return converter switch
            {
                ConverterEnum.N2NC => 1,
                ConverterEnum.DP => 3,
                ConverterEnum.KRRLN => 4,
                _ => 0
            };
        }

        // 预设和管道使用的文件夹路径
        private static string BaseFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), BaseAppFolderName);

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
                    Logger.WriteLine(LogLevel.Debug,
                        $"[BaseOptionsManager]Failed to load config file '{path}': {ex.Message}. Creating default config and overwriting file.");
                    _cachedConfig = new AppConfig();
                    SaveConfig(); // 覆盖损坏的文件
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
                    var opts = new JsonSerializerOptions
                    { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
                    string json = JsonSerializer.Serialize(_cachedConfig, opts);
                    File.WriteAllText(path, json);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error,$"[BaseOptionsManager]Failed to save config file '{path}': {ex.Message}");
                    throw new IOException($"Unable to save configuration to '{path}': {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 获取指定工具的选项
        /// </summary>
        public static T? LoadOptions<T>(ConverterEnum converter)
        {
            var config = LoadConfig();
            object? value = config.Converters.GetValueOrDefault(converter);
            if (value is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>();
            }
            return (T?)value;
        }

        /// <summary>
        /// 获取指定模块的选项
        /// </summary>
        public static T? LoadOptions<T>(ModuleEnum module)
        {
            var config = LoadConfig();
            object? value = config.Modules.GetValueOrDefault(module);
            if (value is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>();
            }
            return (T?)value;
        }

        /// <summary>
        /// 保存指定工具的选项
        /// </summary>
        public static void SaveOptions<T>(ConverterEnum converter, T options)
        {
            var config = LoadConfig();
            config.Converters[converter] = options;
            SaveConfig();
        }

        /// <summary>
        /// 保存指定模块的选项
        /// </summary>
        public static void SaveOptions<T>(ModuleEnum module, T options)
        {
            var config = LoadConfig();
            config.Modules[module] = options;
            SaveConfig();
        }

        /// <summary>
        /// 获取应用设置
        /// </summary>
        private static T GetAppSetting<T>(Func<AppConfig, T> getter)
        {
            return getter(LoadConfig());
        }

        /// <summary>
        /// 设置应用设置
        /// </summary>
        private static void SetAppSetting(Action<AppConfig> setter)
        {
            var config = LoadConfig();
            setter(config);
            SaveConfig();
        }

        /// <summary>
        /// 获取实时预览设置
        /// </summary>
        public static bool GetRealTimePreview() => GetAppSetting(c => c.RealTimePreview);

        /// <summary>
        /// 保存实时预览设置
        /// </summary>
        public static void SetRealTimePreview(bool value) => SetAppSetting(c => c.RealTimePreview = value);

        /// <summary>
        /// 获取应用程序主题设置
        /// </summary>
        public static string? GetApplicationTheme() => GetAppSetting(c => c.ApplicationTheme);

        /// <summary>
        /// 保存应用程序主题设置
        /// </summary>
        public static void SetApplicationTheme(string? theme) => SetAppSetting(c => c.ApplicationTheme = theme);

        /// <summary>
        /// 获取窗口背景类型设置
        /// </summary>
        public static string? GetWindowBackdropType() => GetAppSetting(c => c.WindowBackdropType);

        /// <summary>
        /// 保存窗口背景类型设置
        /// </summary>
        public static void SetWindowBackdropType(string? backdropType) => SetAppSetting(c => c.WindowBackdropType = backdropType);

        /// <summary>
        /// 获取是否更新主题色设置
        /// </summary>
        public static bool? GetUpdateAccent() => GetAppSetting(c => c.UpdateAccent);

        /// <summary>
        /// 保存是否更新主题色设置
        /// </summary>
        public static void SetUpdateAccent(bool? updateAccent) => SetAppSetting(c => c.UpdateAccent = updateAccent);

        /// <summary>
        /// 获取是否强制中文设置
        /// </summary>
        public static bool GetForceChinese() => GetAppSetting(c => c.ForceChinese);

        /// <summary>
        /// 保存是否强制中文设置
        /// </summary>
        public static void SetForceChinese(bool forceChinese) => SetAppSetting(c => c.ForceChinese = forceChinese);

        public static void SavePreset<T>(string toolName, string presetName, T options)
        {
            string folder = GetToolFolder(Path.Combine(toolName, PresetsFolderName));
            Directory.CreateDirectory(folder);
            string safe = MakeSafeFilename(presetName) + ".json";
            string path = Path.Combine(folder, safe);
            string json = SerializeToJson(options);
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
                    opt = DeserializeFromJson<T>(json);
                }
                catch (Exception ex)
                {
                    // Swallow malformed preset but log for diagnostics
                    Logger.WriteLine(LogLevel.Error,$"[BaseOptionsManager]Failed to load preset '{file}': {ex.Message}");
                }

                yield return (name, opt);
            }
        }

        private static string MakeSafeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        public static void SavePipelinePreset(string presetName, PipelineOptions pipelineOptions)
        {
            string folder = GetToolFolder(PipelinesFolderName);
            Directory.CreateDirectory(folder);
            string safe = MakeSafeFilename(presetName) + ".json";
            string path = Path.Combine(folder, safe);
            var opts = new JsonSerializerOptions
            { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
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
                    Logger.WriteLine(LogLevel.Error,$"[BaseOptionsManager]Failed to load pipeline preset '{file}': {ex.Message}");
                }

                yield return (name, opt);
            }
        }
    }
}