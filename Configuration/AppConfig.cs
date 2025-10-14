using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using krrTools.Bindable;

namespace krrTools.Configuration
{
    /// <summary>
    /// 统一的应用程序配置类，包含所有工具的设置
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 转换器工具设置
        /// </summary>
        public Dictionary<ConverterEnum, object?> Converters { get; set; } = new();

        /// <summary>
        /// 其他模块设置
        /// </summary>
        public Dictionary<ModuleEnum, object?> Modules { get; set; } = new();

        /// <summary>
        /// 工具预设，按工具名称分组
        /// </summary>
        public Dictionary<string, Dictionary<string, object?>> Presets { get; set; } = new();

        /// <summary>
        /// 管道预设
        /// </summary>
        public Dictionary<string, PipelineOptions> PipelinePresets { get; set; } = new();

        /// <summary>
        /// 全局设置
        /// </summary>
        public GlobalSettings GlobalSettings { get; set; } = new();
    }

    /// <summary>
    /// 全局应用程序设置类
    /// </summary>
    public class GlobalSettings
    {
        private bool _isSaving; // 防止递归保存的标志
        private CancellationTokenSource? _saveDelayCts; // 延迟保存的取消令牌

        /// <summary>
        /// 实时预览设置
        /// </summary>
        public Bindable<bool> MonitoringEnable { get; } = new();

        /// <summary>
        /// 应用程序主题设置
        /// </summary>
        public Bindable<string> ApplicationTheme { get; } = new();

        /// <summary>
        /// 窗口背景类型设置
        /// </summary>
        public Bindable<string> WindowBackdropType { get; } = new();

        /// <summary>
        /// 是否更新主题色设置
        /// </summary>
        public Bindable<bool> UpdateAccent { get; } = new();

        /// <summary>
        /// 是否强制中文设置
        /// </summary>
        public Bindable<bool> ForceChinese { get; } = new();

        /// <summary>
        /// osu! Songs文件夹路径
        /// </summary>
        public Bindable<string> SongsPath { get; set; } = new(string.Empty);

        /// <summary>
        /// N2NC转换快捷键
        /// </summary>
        public Bindable<string> N2NCHotkey { get; set; } = new("Ctrl+Shift+N");

        /// <summary>
        /// DP转换快捷键
        /// </summary>
        public Bindable<string> DPHotkey { get; set; } = new("Ctrl+Shift+D");

        /// <summary>
        /// KRRLN转换快捷键
        /// </summary>
        public Bindable<string> KRRLNHotkey { get; set; } = new("Ctrl+Shift+K");

        /// <summary>
        /// 最后一次预览路径
        /// </summary>
        public Bindable<string> LastPreviewPath { get; set; } = new(string.Empty);

        /// <summary>
        /// 构造函数，设置自动保存回调
        /// </summary>
        public GlobalSettings()
        {
            // 为所有 Bindable 设置自动保存回调
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
        }

        private void ScheduleSave()
        {
            // 取消之前的延迟保存
            _saveDelayCts?.Cancel();
            
            // 创建新的延迟保存任务
            _saveDelayCts = new CancellationTokenSource();
            var token = _saveDelayCts.Token;
            
            Task.Run(async () =>
            {
                try
                {
                    // 等待500ms
                    await Task.Delay(500, token);
                    
                    // 如果没有被取消，执行保存
                    if (!token.IsCancellationRequested)
                    {
                        await Task.Run(SaveSettingsImmediate, token);
                    }
                }
                catch (TaskCanceledException)
                {
                    // 延迟被取消，忽略
                }
            }, token);
        }

        private void SaveSettingsImmediate()
        {
            // 防止递归保存
            if (_isSaving) return;
            
            _isSaving = true;
            try
            {
                // 使用静默保存，避免触发事件循环
                BaseOptionsManager.SetGlobalSettingsSilent(this);
            }
            finally
            {
                _isSaving = false;
            }
        }
    }
}