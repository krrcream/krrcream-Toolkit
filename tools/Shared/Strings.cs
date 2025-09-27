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
        public const string TabN2NC = "NtoN Converter|NtoN转换器";
        public const string TabLNTransformer = "YLS LN Transformer|凉雨转面器";
        public const string TabDPTool = "DP Tool|DP 工具";
        public const string TabKrrLV = "KrrLV Calculation|KrrLV 计算";
        public const string TabFilesManager = "Files Manager|文件管理";

        // Footer
        public const string FooterCopyright = "© 2025 krrcream. All rights reserved.";
        public const string FooterCopyrightCN = "© 2025 krrcream. 保留所有权利。";
        public const string GitHubLinkText = "github";
        public const string GitHubLinkUrl = "https://github.com/krrcream/krrcream-Toolkit";
        
        // Settings menu
        public const string SettingsMenuTheme = "Theme|主题";
        public const string SettingsMenuBackdrop = "Backdrop|背景效果";
        public const string UpdateAccent = "Update Accent|系统调色";
        public const string SettingsMenuLanguage = "切换到中文|Switch to English";
        
        // Theme options
        public const string ThemeLight = "Light|浅色";
        public const string ThemeDark = "Dark|深色";
        public const string ThemeHighContrast = "HighContrast|高对比度";
        
        // Backdrop options
        public const string BackdropNone = "None|无";
        public const string BackdropMica = "Mica|云母";
        public const string BackdropAcrylic = "Acrylic|亚克力";
        public const string BackdropTabbed = "Tabbed|标签页";
        
        // Listener
        public const string OSUListenerButton = "OSU Listener|osu 侦听";
        public const string ListenerTitlePrefix = "osu!Listener";

        public const string CreateMapLabel = "Create Map|创建谱面";
        public const string SetHotkeyLabel = "Set Hotkey|设置快捷键";
        public const string BrowseLabel = "Browse...|设置路径...";
        public const string SongsFolderPathHeader = "Songs folder path|歌曲文件夹路径";
        public const string RealTimePreviewLabel = "Real-time preview|实时预览";
        public const string MonitoringInformationHeader = "Monitoring Information|谱面信息";

        // N2NC
        public const string N2NCTargetKeysTemplate = "Target Keys: {0}|目标键数: {0}";
        public const string N2NCMaxKeysTemplate = "Max Keys: {0}|上限权重: {0}";
        public const string N2NCMinKeysTemplate = "Min Keys: {0}|下限权重: {0}";
        public const string N2NCTransformSpeedTemplate = "Transform Speed: {0}|转换速度: {0}";
        public const string N2NCGenerateSeedLabel = "Generate|随机生成";
        public const string N2NCGenerateSeedTooltip = "Generate a random seed value|生成一个随机种子值";
        
        // LN Transformer
        public const string LevelTooltip = "Density Level (hover to see detailed description)\n-3: Very Low 0: Low 3: Medium 6: High 10: Very High|密度等级（鼠标悬停查看详细说明）\n-3: 极低 0: 低 3: 中等 6: 高 10: 极高";

        public const string LevelLabel = "Level {0}|强度 {0}";
        public const string LNPercentageLabel = "LN {0}%|LN占比 {0}%";
        public const string DivideLabel = "Divide 1/{0}|面尾量化 1/{0}";
        public const string ColumnLabel = "Column {0}|列量级 {0}";
        public const string GapLabel = "Gap {0}|间隔 {0}";
        public const string OverallDifficultyHeader = "Over Diffclut|判定难度OD";
        public const string IgnoreCheckbox = "Ignore|忽略";
        public const string IgnoreTooltip = "Skip already converted beatmaps|忽略已转换的谱面";
        public const string FixErrorsCheckbox = "Fix Errors|修复误差";
        public const string FixErrorsTooltip = "Fix timing errors|修复时间误差";
        public const string OriginalLNsCheckbox = "Original LNs|原始长条";
        public const string OriginalLNsTooltip = "Keep original LNs|保留原始长条";
        public const string InstructionsLink = "Instructions|使用说明";

        // DP Tool texts
        public const string DPKeysTemplate = "Keys: {0}|键数: {0}";
        public const string DPLeftMaxKeysTemplate = "Max Keys: {0}|最大键数: {0}";
        public const string DPLeftMinKeysTemplate = "Min Keys: {0}|最小键数: {0}";
        public const string DPRightMaxKeysTemplate = "Max Keys: {0}|最大键数: {0}";
        public const string DPRightMinKeysTemplate = "Min Keys: {0}|最小键数: {0}";
        public const string DPModifyKeysCheckbox = "Enable Modify keys|启用修改键数";
        public const string DPModifyKeysTooltip = "Enable modification of single-side key count|启用键位修改";
        public const string DPKeysTooltip = "Keys explanation|键数说明";
        public const string DPLeftLabel = "Left|左手";
        public const string DPRightLabel = "Right|右手";
        public const string DPMirrorLabel = "Mirror|镜像";
        public const string DPMirrorTooltipLeft = "Enable left-hand mirroring|启用左手镜像";
        public const string DPDensityLabel = "Density|密度";
        public const string DPDensityTooltipLeft = "Enable left-hand density adjustments|启用左手密度调整";

        public const string DPMirrorTooltipRight = "Enable right-hand mirroring|启用右手镜像";
        public const string DPDensityTooltipRight = "Enable right-hand density adjustments|启用右手密度调整";

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

        /// <summary>
        /// Format a localized template string with parameters
        /// </summary>
        /// <param name="template">Template string in format "EN {0}|中文 {0}"</param>
        /// <param name="args">Arguments to format</param>
        /// <returns>Formatted localized string</returns>
        public static string FormatLocalized(string template, params object[] args)
        {
            string localized = template.Localize();
            return string.Format(localized, args);
        }
    }
    
    public static class LocalizationExtensions
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string[]> _cache = new();

        /// <summary>
        /// 拓展方法，xxx.Localize()
        /// </summary>
        public static string Localize(this string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            
            if (!_cache.TryGetValue(s, out var parts))
            {
                parts = s.Split(['|'], 2);
                _cache[s] = parts;
            }
            
            // Use centralized Strings management; no try-catch for localization
            return SharedUIComponents.IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
        }
    }
}