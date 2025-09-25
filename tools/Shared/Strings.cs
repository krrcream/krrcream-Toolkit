using krrTools.Tools.Shared;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// Centralized string constants for UI text to avoid duplication and simplify maintenance.
    /// </summary>
    public static class Strings
    {
        public const string WindowTitle = "krrcream's Toolkit V2.3.1";

        // Tab headers
        public const string TabConverter = "NtoN Converter|NtoN转换器";
        public const string TabLNTransformer = "LN Transformer|LN 转换";
        public const string TabDPTool = "DP Tool|DP 工具";
        public const string TabLV = "LV Calculation|LV 计算";
        public const string TabGetFiles = "Files Manager|文件管理";

        // Footer
        public const string FooterCopyright = "© 2025 krrcream. All rights reserved.";
        public const string FooterCopyrightCN = "© 2025 krrcream. 保留所有权利。";
        public const string GitHubLinkText = "github";
        public const string GitHubLinkUrl = "https://github.com/krrcream/krrcream-Toolkit";
        
        // Language button labels
        public const string UpdateAccent = "Update Accent|系统调色";
        
        // Listener
        public const string OSUListenerButton = "OSU Listener|osu 侦听";
        public const string ListenerTitlePrefix = "osu!Listener";

        public const string CreateMapLabel = "Create Map|创建谱面";
        public const string SetHotkeyLabel = "Set Hotkey|设置快捷键";
        public const string BrowseLabel = "Browse...|设置路径...";
        public const string SongsFolderPathHeader = "Songs folder path|歌曲文件夹路径";
        public const string RealTimePreviewLabel = "Real-time preview|实时预览";
        public const string MonitoringInformationHeader = "Monitoring Information|谱面信息";

        // N2NC texts
        public const string N2NCTargetKeysLabel = "Target keys|目标键数";
        public const string N2NCMaxKeysLabel = "Max keys|上限权重";
        public const string N2NCMinKeysLabel = "Min keys|下限权重";
        public const string N2NCTransformSpeedLabel = "Transform speed|转换速度";
        public const string N2NCGenerateSeedLabel = "Generate|随机生成";
        public const string N2NCGenerateSeedTooltip = "Generate a random seed value|生成一个随机种子值";
        
        // LN Transformer
        public const string LevelHeader = "Level|等级";
        public const string LevelTooltip = "Density Level (hover to see detailed description)\n-3: Very Low 0: Low 3: Medium 6: High 10: Very High|密度等级（鼠标悬停查看详细说明）\n-3: 极低 0: 低 3: 中等 6: 高 10: 极高";
        public const string LNPercentageHeader = "LN Percentage|长条比例";
        public const string DivideHeader = "Divide|分割";
        public const string ColumnsHeader = "Columns|列数";
        public const string GapHeader = "Gap|间隔";
        public const string OverallDifficultyHeader = "Overall Difficulty (OD)|总体难度 (OD)";
        public const string IgnoreCheckbox = "Ignore|忽略";
        public const string IgnoreTooltip = "Skip already converted beatmaps|忽略已转换的谱面";
        public const string FixErrorsCheckbox = "Fix Errors|修复误差";
        public const string FixErrorsTooltip = "Fix timing errors|修复时间误差";
        public const string OriginalLNsCheckbox = "Original LNs|原始长条";
        public const string OriginalLNsTooltip = "Keep original LNs|保留原始长条";
        public const string InstructionsLink = "Instructions|使用说明";

        // DP Tool texts
        public const string DPModifyKeysCheckbox = "Enable Modify keys|启用修改键数";
        public const string DPModifyKeysTooltip = "Enable modification of single-side key count|启用键位修改";
        public const string DPKeysLabel = "Keys|键数";
        public const string DPKeysTooltip = "Keys explanation|键数说明";
        public const string DPLeftLabel = "Left|左手";
        public const string DPMirrorLabel = "Mirror|镜像";
        public const string DPMirrorTooltipLeft = "Enable left-hand mirroring|启用左手镜像";
        public const string DPDensityLabel = "Density|密度";
        public const string DPDensityTooltipLeft = "Enable left-hand density adjustments|启用左手密度调整";
        public const string DPLeftMaxKeysLabel = "Left max keys|左手最大键数";
        public const string DPLeftMinKeysLabel = "Left min keys|左手最小键数";
        public const string DPRightLabel = "Right|右手";
        public const string DPMirrorTooltipRight = "Enable right-hand mirroring|启用右手镜像";
        public const string DPDensityTooltipRight = "Enable right-hand density adjustments|启用右手密度调整";
        public const string DPRightMaxKeysLabel = "Right max keys|右手最大键数";
        public const string DPRightMinKeysLabel = "Right min keys|右手最小键数";
        
        // Presets
        public const string PresetsLabel = "Presets|预设";
        public const string FilterLabel = "Filter|过滤";

        // Main window and conversion messages
        public const string NoOsuFilesFound = "No .osu files found in dropped items.|未在拖放项目中找到.osu文件。";
        public const string ConversionResultTitle = "Conversion Result";
        public const string ConversionFinishedHeader = "Conversion finished. Created files:";
        public const string ConversionFailedTitle = "Conversion Failed";
        public const string ConversionFailedMessage = "Conversion failed for the selected files. The staged files remain so you can retry.";
        public const string ConversionNoOutputMessage = "Conversion did not produce any output.";
        public const string DPSettingsLoadFallback = "DP settings failed to load here — showing fallback. If this persists, try reopening the DP tool.";
        public const string LVSettingsLoadFallback = "LV Calculator failed to load here — showing fallback.";
        public const string GetFilesSettingsLoadFallback = "osu! file manager failed to load here — showing fallback.\nTry opening it in a separate window if the issue persists.";
        
        /// <summary>
        /// Localize a string with format "EN|中文" based on current language setting
        /// </summary>
        /// <param name="s">String in format "EN|中文"</param>
        /// <returns>Strings.Localize(Strings.xxx), 自动切换语言拓展</returns>
        public static string Localize(string s)
        {
            return s.Localize();
        }
    }
    
    public static class LocalizationExtensions
    {
        /// <summary>
        /// 拓展方法，xxx.Localize()
        /// </summary>
        public static string Localize(this string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var parts = s.Split(['|'], 2);
            if (parts.Length == 1) return s;

            // Use centralized Strings management; no try-catch for localization
            return SharedUIComponents.IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
        }
    }
}