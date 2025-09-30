using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using krrTools.Core;
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

        // 工具映射 - 自动注册
        private static readonly Dictionary<object, Type> _toolMappings = new();

        // 静态构造函数 - 自动注册工具
        static BaseOptionsManager()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var type in assemblies.SelectMany(a => a.GetTypes())
                .Where(t => typeof(IToolModule).IsAssignableFrom(t) && !t.IsAbstract))
            {
                try
                {
                    var instance = (IToolModule)Activator.CreateInstance(type);
                    _toolMappings[instance.EnumValue] = instance.OptionsType;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to register tool module {type.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取控件类型 - 直接映射
        /// </summary>
        public static Type? GetControlType(string toolName)
        {
            return toolName switch
            {
                "N2NC" => typeof(Tools.N2NC.N2NCControl),
                "DP" => typeof(Tools.DPtool.DPToolControl),
                "KRRLN" => typeof(Tools.KRRLNTransformer.KRRLNTransformerControl),
                _ => null
            };
        }

        /// <summary>
        /// 获取源ID - 从DI容器获取
        /// </summary>
        public static int GetSourceId(object toolEnum)
        {
            if (toolEnum is string toolName)
            {
                return toolName switch
                {
                    "N2NC" => 1,
                    "DP" => 3,
                    "KRRLN" => 4,
                    _ => 0
                };
            }
            else if (toolEnum is ConverterEnum converter)
            {
                return converter switch
                {
                    ConverterEnum.N2NC => 1,
                    ConverterEnum.DP => 3,
                    ConverterEnum.KRRLN => 4,
                    _ => 0
                };
            }
            return 0;
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
                    Debug.WriteLine(
                        $"Failed to load config file '{path}': {ex.Message}. Creating default config and overwriting file.");
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
                    Debug.WriteLine($"Failed to save config file '{path}': {ex.Message}");
                    throw new IOException($"Unable to save configuration to '{path}': {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 获取指定工具的选项
        /// </summary>
        public static T? LoadOptions<T>(object toolEnum)
        {
            var config = LoadConfig();

            if (toolEnum is ConverterEnum converter)
            {
                return (T?)config.Converters.GetValueOrDefault(converter);
            }
            else if (toolEnum is ModuleEnum module)
            {
                return (T?)config.Modules.GetValueOrDefault(module);
            }

            return default;
        }

        /// <summary>
        /// 保存指定工具的选项
        /// </summary>
        public static void SaveOptions<T>(object toolEnum, T options)
        {
            var config = LoadConfig();

            if (toolEnum is ConverterEnum converter)
            {
                config.Converters[converter] = options;
            }
            else if (toolEnum is ModuleEnum module)
            {
                config.Modules[module] = options;
            }

            SaveConfig();
        }

        /// <summary>
        /// 获取应用设置
        /// </summary>
        public static T GetAppSetting<T>(Func<AppConfig, T> getter)
        {
            return getter(LoadConfig());
        }

        /// <summary>
        /// 设置应用设置
        /// </summary>
        public static void SetAppSetting(Action<AppConfig> setter)
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
        public static bool? GetForceChinese() => GetAppSetting(c => c.ForceChinese);

        /// <summary>
        /// 保存是否强制中文设置
        /// </summary>
        public static void SetForceChinese(bool? forceChinese) => SetAppSetting(c => c.ForceChinese = forceChinese);

        // Preset helpers: save preset by name (filename-safe) and list available presets
        public static void SavePreset<T>(string toolName, string presetName, T options)
        {
            string folder = GetToolFolder(Path.Combine(toolName, PresetsFolderName));
            Directory.CreateDirectory(folder);
            string safe = MakeSafeFilename(presetName) + ".json";
            string path = Path.Combine(folder, safe);
            var opts = new JsonSerializerOptions
                { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
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
                    Debug.WriteLine($"Failed to load pipeline preset '{file}': {ex.Message}");
                }

                yield return (name, opt);
            }
        }
    }
}