using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using krrTools.Bindable;
using krrTools.Core;
using Microsoft.Extensions.Logging;

namespace krrTools.Configuration
{
    /// <summary>
    /// 统一的应用程序配置类，包含所有工具的设置
    /// </summary>
    internal class ToolsConfig
    {
        /// <summary>
        /// 转换器工具设置
        /// </summary>
        public Dictionary<ConverterEnum, object?> Converters { get; set; } = new Dictionary<ConverterEnum, object?>();

        /// <summary>
        /// 其他模块设置
        /// </summary>
        public Dictionary<ModuleEnum, object?> Modules { get; set; } = new Dictionary<ModuleEnum, object?>();

        /// <summary>
        /// 工具预设，按工具名称分组
        /// </summary>
        public Dictionary<string, Dictionary<string, object?>> Presets { get; set; } = new Dictionary<string, Dictionary<string, object?>>();

        /// <summary>
        /// 管道预设
        /// </summary>
        public Dictionary<string, PipelineOptions> PipelinePresets { get; set; } = new Dictionary<string, PipelineOptions>();
    }

    /// <summary>
    /// 完整的应用程序配置类，包含工具设置和全局设置
    /// </summary>
    internal class FullAppConfig
    {
        /// <summary>
        /// 工具配置
        /// </summary>
        public ToolsConfig ToolsConfig { get; set; } = new ToolsConfig();

        /// <summary>
        /// 全局设置
        /// </summary>
        public GlobalSettings GlobalSettings { get; set; } = new GlobalSettings();
    }

    /// <summary>
    /// 全局应用程序设置类
    /// </summary>
    public class GlobalSettings
    {
        private CancellationTokenSource? _saveDelayCts;

        /// <summary>
        /// 实时预览设置
        /// </summary>
        public Bindable<bool> MonitoringEnable { get; set; } = new Bindable<bool>();

        /// <summary>
        /// 应用程序主题设置
        /// </summary>
        public Bindable<string> ApplicationTheme { get; set; } = new Bindable<string>();

        /// <summary>
        /// 窗口背景类型设置
        /// </summary>
        public Bindable<string> WindowBackdropType { get; set; } = new Bindable<string>();

        /// <summary>
        /// 是否更新主题色设置
        /// </summary>
        public Bindable<bool> UpdateAccent { get; set; } = new Bindable<bool>();

        /// <summary>
        /// 是否强制中文设置
        /// </summary>
        public Bindable<bool> ForceChinese { get; set; } = new Bindable<bool>();

        /// <summary>
        /// osu! Songs文件夹路径
        /// </summary>
        public Bindable<string> SongsPath { get; set; } = new Bindable<string>(string.Empty);

        /// <summary>
        /// N2NC转换快捷键
        /// </summary>
        public Bindable<string> N2NCHotkey { get; set; } = new Bindable<string>("Ctrl+Shift+N");

        /// <summary>
        /// DP转换快捷键
        /// </summary>
        public Bindable<string> DPHotkey { get; set; } = new Bindable<string>("Ctrl+Shift+D");

        /// <summary>
        /// KRRLN转换快捷键
        /// </summary>
        public Bindable<string> KRRLNHotkey { get; set; } = new Bindable<string>("Ctrl+Shift+K");

        /// <summary>
        /// 最后一次预览路径
        /// </summary>
        public Bindable<string> LastPreviewPath { get; set; } = new Bindable<string>(string.Empty);

        /// <summary>
        /// 数据表列顺序，按工具名称分组
        /// </summary>
        public Bindable<Dictionary<string, List<int>>> DataGridColumnOrders { get; set; } = new Bindable<Dictionary<string, List<int>>>(new Dictionary<string, List<int>>());

        /// <summary>
        /// 构造函数，设置自动保存回调
        /// </summary>
        public GlobalSettings()
        {
            SetupAutoSave();
        }

        private void SetupAutoSave()
        {
            MonitoringEnable.OnValueChanged(_ => ScheduleSave());
            ApplicationTheme.OnValueChanged(_ => ScheduleSave());
            WindowBackdropType.OnValueChanged(_ => ScheduleSave());
            UpdateAccent.OnValueChanged(_ => ScheduleSave());
            ForceChinese.OnValueChanged(_ => ScheduleSave());
            SongsPath.OnValueChanged(_ => ScheduleSave());
            N2NCHotkey.OnValueChanged(_ => ScheduleSave());
            DPHotkey.OnValueChanged(_ => ScheduleSave());
            KRRLNHotkey.OnValueChanged(_ => ScheduleSave());
            LastPreviewPath.OnValueChanged(_ => ScheduleSave());
            DataGridColumnOrders.OnValueChanged(_ => ScheduleSave());
        }

        private void ScheduleSave()
        {
            _saveDelayCts?.Cancel();
            _saveDelayCts = new CancellationTokenSource();
            CancellationToken token = _saveDelayCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(500, token);
                    if (!token.IsCancellationRequested)
                        ConfigManager.SaveConfig();
                }
                catch (TaskCanceledException) { }
            }, token);
        }

        /// <summary>
        /// 立即保存设置，取消任何延迟保存
        /// </summary>
        public void Flush()
        {
            _saveDelayCts?.Cancel();
            ConfigManager.SaveConfig();
        }
    }

    /// <summary>
    /// 配置管理器，提供统一的配置访问接口
    /// </summary>
    public static class ConfigManager
    {
        private const string ConfigFileName = "config.json";
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        private static FullAppConfig? _cachedConfig;
        private static readonly Lock _configLock = new Lock();

        // 全局缓存的序列化选项
        private static readonly JsonSerializerOptions _deserializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new BindableJsonConverter<bool>(), new BindableJsonConverter<string>(), new BindableJsonConverter<Dictionary<string, List<int>>>() }
        };

        private static readonly JsonSerializerOptions _serializeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new BindableJsonConverter<string>(), new BindableJsonConverter<Dictionary<string, List<int>>>() }
        };

        private static bool _isDeserializing;

        /// <summary>
        /// 获取是否正在反序列化
        /// </summary>
        public static bool IsDeserializing
        {
            get => _isDeserializing;
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private static FullAppConfig LoadConfig()
        {
            lock (_configLock)
            {
                if (_cachedConfig != null) return _cachedConfig;

                if (!File.Exists(ConfigFilePath))
                {
                    _cachedConfig = new FullAppConfig();
                    return _cachedConfig;
                }

                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    _cachedConfig = JsonSerializer.Deserialize<FullAppConfig>(json, _deserializeOptions) ?? new FullAppConfig();
                    return _cachedConfig;
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, $"[ConfigManager] Failed to load config: {ex.Message}. Using default config.");
                    _cachedConfig = new FullAppConfig();
                    SaveConfig();
                    return _cachedConfig;
                }
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public static void SaveConfig()
        {
            lock (_configLock)
            {
                if (_cachedConfig == null) return;

                try
                {
                    string json = JsonSerializer.Serialize(_cachedConfig, _serializeOptions);
                    File.WriteAllText(ConfigFilePath, json);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, $"[ConfigManager] Failed to save config: {ex.Message}");
                    throw new IOException($"Unable to save configuration: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 获取工具选项
        /// </summary>
        public static T? GetOptions<T>(ConverterEnum converter)
        {
            ToolsConfig config = LoadConfig().ToolsConfig;
            object? value = config.Converters.GetValueOrDefault(converter);

            if (value is JsonElement jsonElement)
            {
                _isDeserializing = true;
                var result = jsonElement.Deserialize<T>();
                _isDeserializing = false;
                if (result is ToolOptionsBase toolOptions) toolOptions.IsLoading = true;
                config.Converters[converter] = result;
                return result;
            }

            return (T?)value;
        }

        /// <summary>
        /// 设置工具选项
        /// </summary>
        public static void SetOptions<T>(ConverterEnum converter, T options)
        {
            LoadConfig().ToolsConfig.Converters[converter] = options;
            SaveConfig();
        }

        /// <summary>
        /// 获取模块选项
        /// </summary>
        public static T? GetModuleOptions<T>(ModuleEnum module)
        {
            ToolsConfig config = LoadConfig().ToolsConfig;
            object? value = config.Modules.GetValueOrDefault(module);

            if (value is JsonElement jsonElement)
            {
                _isDeserializing = true;
                var result = jsonElement.Deserialize<T>();
                _isDeserializing = false;
                if (result is ToolOptionsBase toolOptions) toolOptions.IsLoading = true;
                config.Modules[module] = result;
                return result;
            }

            return (T?)value;
        }

        /// <summary>
        /// 设置模块选项
        /// </summary>
        public static void SetModuleOptions<T>(ModuleEnum module, T options)
        {
            LoadConfig().ToolsConfig.Modules[module] = options;
            SaveConfig();
        }

        /// <summary>
        /// 获取全局设置
        /// </summary>
        public static GlobalSettings GetGlobalSettings()
        {
            return LoadConfig().GlobalSettings;
        }

        /// <summary>
        /// 获取全局设置的属性
        /// </summary>
        public static Bindable<T> GetSetting<T>(Func<GlobalSettings, Bindable<T>> selector)
        {
            return selector(GetGlobalSettings());
        }

        /// <summary>
        /// 保存预设
        /// </summary>
        public static void SavePreset<T>(string toolName, string presetName, T options) where T : class
        {
            ToolsConfig config = LoadConfig().ToolsConfig;
            if (!config.Presets.ContainsKey(toolName)) config.Presets[toolName] = new Dictionary<string, object?>();
            config.Presets[toolName][presetName] = options;
            SaveConfig();
        }

        /// <summary>
        /// 加载预设
        /// </summary>
        public static IEnumerable<(string Name, T? Options)> LoadPresets<T>(string toolName) where T : class
        {
            ToolsConfig config = LoadConfig().ToolsConfig;

            if (config.Presets.TryGetValue(toolName, out Dictionary<string, object?>? toolPresets))
            {
                foreach (KeyValuePair<string, object?> kvp in toolPresets)
                {
                    T? opt = default;

                    try
                    {
                        if (kvp.Value is JsonElement jsonElement)
                            opt = jsonElement.Deserialize<T>();
                        else
                            opt = kvp.Value as T;
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLine(LogLevel.Error, $"[ConfigManager] Failed to deserialize preset '{kvp.Key}': {ex.Message}");
                    }

                    yield return (kvp.Key, opt);
                }
            }
        }

        /// <summary>
        /// 删除预设
        /// </summary>
        public static void DeletePreset(string toolName, string presetName)
        {
            ToolsConfig config = LoadConfig().ToolsConfig;

            if (config.Presets.TryGetValue(toolName, out Dictionary<string, object?>? toolPresets))
            {
                toolPresets.Remove(presetName);
                if (toolPresets.Count == 0) config.Presets.Remove(toolName);
                SaveConfig();
            }
        }

        /// <summary>
        /// 保存管道预设
        /// </summary>
        public static void SavePipelinePreset(string presetName, PipelineOptions options)
        {
            LoadConfig().ToolsConfig.PipelinePresets[presetName] = options;
            SaveConfig();
        }

        /// <summary>
        /// 加载管道预设
        /// </summary>
        public static IEnumerable<(string Name, PipelineOptions Options)> LoadPipelinePresets()
        {
            foreach (KeyValuePair<string, PipelineOptions> kvp in LoadConfig().ToolsConfig.PipelinePresets)
                yield return (kvp.Key, kvp.Value);
        }

        // 向后兼容性方法

        /// <summary>
        /// 获取指定工具的选项（兼容性方法）
        /// </summary>
        public static T? LoadOptions<T>(ConverterEnum converter)
        {
            return GetOptions<T>(converter);
        }

        /// <summary>
        /// 保存指定工具的选项（兼容性方法）
        /// </summary>
        public static void SaveOptions<T>(ConverterEnum converter, T options)
        {
            SetOptions(converter, options);
        }

        /// <summary>
        /// 获取指定模块的选项（兼容性方法）
        /// </summary>
        public static T? LoadModuleOptions<T>(ModuleEnum module)
        {
            return GetModuleOptions<T>(module);
        }

        /// <summary>
        /// 保存指定模块的选项（兼容性方法）
        /// </summary>
        public static void SaveModuleOptions<T>(ModuleEnum module, T options)
        {
            SetModuleOptions(module, options);
        }

        /// <summary>
        /// 设置变化事件
        /// </summary>
        public static event Action<ConverterEnum>? SettingsChanged;

        /// <summary>
        /// 全局设置变化事件
        /// </summary>
        public static event Action? GlobalSettingsChanged;

        /// <summary>
        /// 设置全局EventBus引用
        /// </summary>
        public static void SetEventBus(IEventBus eventBus)
        {
            // 简化实现，移除复杂的事件处理
        }

        /// <summary>
        /// 更新全局设置（兼容性方法）
        /// </summary>
        public static void UpdateGlobalSettings(Action<GlobalSettings> updater)
        {
            updater(GetGlobalSettings());
            // 自动保存由 GlobalSettings 内部处理
        }

        /// <summary>
        /// 保存全局设置（静默保存，兼容性方法）
        /// </summary>
        public static void SetGlobalSettingsSilent(GlobalSettings settings)
        {
            // 静默保存不再需要，自动保存由内部处理
        }
    }
}
