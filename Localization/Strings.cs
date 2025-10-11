namespace krrTools.Localization
{
    /// <summary>
    /// Centralized string constants for UI text to avoid duplication and simplify maintenance.
    /// </summary>
    public static class Strings
    {
        public const string WindowTitle = "krrcream's Toolkit V2.3.1";

        // Tab headers
        public const string TabN2NC = "NtoN Converter|NtoN转换器";
        // public const string TabYLsLN = "YLS LN Transformer|凉雨转面器";
        public const string TabKRRsLN = "KRR LN Transformer|KRR转面器";
        public const string TabDPTool = "DP Tool|DP 工具";
        public const string TabKrrLV = "KRR LV Analysis|KRR LV分析器";
        public const string TabFilesManager = "Files Manager|文件管理";

        // Footer
        public const string FooterCopyright = "© 2025 krrcream. All rights reserved.|© 2025 krrcream. 保留所有权利。";
        public const string KrrcreamUrl = "https://github.com/krrcream";
        public const string GitHubLinkText = "github";
        public const string GitHubLinkUrl = "https://github.com/krrcream/krrcream-Toolkit";

        // Settings menu
        public const string SettingsMenuTheme = "Theme|主题";
        public const string SettingsMenuBackdrop = "Backdrop|背景效果";
        public const string UpdateAccent = "Update Accent|系统调色";
        public const string SettingsMenuLanguage = "中文|English";

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
        public const string OSUListener = "OSU Listener|osu 侦听";

        public const string CreateMapLabel = "Create Map|创建谱面";
        public const string SetHotkeyLabel = "Set Hotkey|设置快捷键";
        public const string BrowseLabel = "Browse...|设置路径...";
        public const string SongsFolderPathHeader = "Songs folder path|歌曲文件夹路径";
        public const string RealTimePreviewLabel = "Real-time preview|实时预览";
        public const string MonitoringInformationHeader = "Monitoring Information|谱面信息";

        // N2NC
        public const string N2NCMaxKeysTemplate = "Max Keys: {0}|上限权重: {0}";
        public const string N2NCMinKeysTemplate = "Min Keys: {0}|下限权重: {0}";
        public const string N2NCTransformSpeedTemplate = "Transform Speed: {0}|转换速度: {0}";

        // LN Transformer
        public const string LevelTooltip = "Density Level (hover to see detailed description)\n-3: Very Low 0: Low 3: Medium 6: High 10: Very High|密度等级（鼠标悬停查看详细说明）\n-3: 极低 0: 低 3: 中等 6: 高 10: 极高";
        public const string LevelLabel = "Level {0}|强度 {0}";
        public const string LNPercentageLabel = "LN {0}%|LN占比 {0}%";
        public const string ColumnLabel = "Column {0}|列量级 {0}";
        public const string GapLabel = "Gap {0}|间隔 {0}";
        public const string IgnoreCheckbox = "Ignore|忽略";
        public const string IgnoreTooltip = "Skip already converted beatmaps|忽略已转换的谱面";
        public const string FixErrorsCheckbox = "Fix Errors|修复误差";
        public const string FixErrorsTooltip = "Fix timing errors|修复时间误差";
        public const string OriginalLNsCheckbox = "Original LNs|原始长条";
        public const string OriginalLNsTooltip = "Keep original LNs|保留原始长条";

        public const string InstructionsLink = "Instructions|使用说明";

        // KRR LN Transformer texts
        public const string KRRShortLNHeader = "Short LN|短面";
        public const string KRRShortPercentageLabel = "Short LN {0}%|短面占比 {0}%";
        public const string KRRShortLevelLabel = "Short Level {0}|短面强度 {0}";
        public const string KRRShortLimitLabel = "Short Limit {0}|短面限制 {0}";
        public const string KRRShortRandomLabel = "Short Random {0}|短面随机 {0}";
        public const string KRRLongLNHeader = "Long LN|长面条";
        public const string KRRLongPercentageLabel = "Long LN {0}%|长面占比 {0}%";
        public const string KRRLongLevelLabel = "Long Level {0}|长面强度 {0}";
        public const string KRRLongLimitLabel = "Long Limit {0}|长面限制 {0}";
        public const string KRRLongRandomLabel = "Long Random {0}|长面随机 {0}";
        public const string KRRAlignLabel = "Align {0}|对齐 {0}";

        public const string LengthThresholdLabel = "Length Threshold {0}|长度阈值 {0}";
        public const string KRRLNAlignLabel = "Long&Short Align {0}|LN长短对齐 {0}";

        // 通用控件名称
        public const string ODSliderLabel = "OD {0}|判定难度 {0}";
        public const string KeysSliderLabel = "Keys: {0}|键数: {0}";
        public const string BeatDivideLabel = "Divide 1/{0}|节拍量化 1/{0}";
        public const string SeedButtonLabel = "Seed|种子";
        public const string SeedGenerateLabel = "Generate|随机生成";
        public const string SeedGenerateTooltip = "Generate a random seed value|生成一个随机种子值";


        // DP Tool专用
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
        public const string RemoveLabel = "Remove|去除";
        public const string RemoveTooltip = "Force remove half-zone|强制去除半区";

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
        public const string GetFilesSettingsLoadFallback = "osu! file manager failed to load here — showing fallback.\nTry opening it in a separate window if the issue persists.|osu! 文件管理器加载失败 — 显示备用方案。\n如果问题持续，请尝试在单独窗口中打开。";

        // Error messages
        public const string ErrorProcessingFile = "Error processing file|处理文件时出错";
        public const string ProcessingError = "Processing Error|处理错误";
        public const string PackagingAddingBeatmapFailed = "Packaging/adding beatmap failed|打包/添加谱面失败";
        public const string Error = "Error|错误";
        public const string ErrorDeletingFiles = "Error deleting files|删除文件时出错";
        public const string FilesDeletedSuccessfully = "{0} file(s) deleted successfully.|成功删除了 {0} 个文件。";

        // Hotkey window
        public const string SetHotkeyTitle = "Set Hotkey|设置热键";
        public const string PressDesiredKeyCombination = "Press your desired key combination:|按下您想要的按键组合：";
        public const string Save = "Save|保存";
        public const string Cancel = "Cancel|取消";

        // Processing window
        public const string ProcessingTitle = "Processing...|处理中...";

        // Success messages
        public const string FileProcessedSuccessfully = "File processed successfully!|文件处理成功！";
        public const string Success = "Success|成功";

        // Listener
        public const string CannotConvert = "Cannot Convert|无法转换";
        public const string NoActiveTabSelected = "No active tab selected.|未选择活动标签页。";
        public const string HotkeyError = "Hotkey Error|热键错误";
        public const string FailedToRegisterHotkey = "Failed to register hotkey|注册热键失败";

        // Files Manager
        public const string NoItemsSelected = "No items selected.|未选择项目。";
        public const string Delete = "Delete|删除";
        public const string DeleteError = "Delete Error|删除错误";
        public const string FilesDeletedSuccessfullyTemplate = "{0} file(s) deleted successfully.|成功删除了 {0} 个文件。";

        // OsuAnalyze errors
        public const string InvalidBeatmapFilePath = "Invalid beatmap file path|无效的谱面文件路径";
        public const string UnableToDetermineParentDirectory = "Unable to determine parent directory|无法确定父目录";
        public const string SourceSongFolderDoesNotExist = "Source song folder does not exist|源歌曲文件夹不存在";
        public const string FailedToCreateOutputPath = "Failed to create {0}|创建 {0} 失败";
        public const string FailedToAddBeatmapToArchive = "Failed to add beatmap to archive|添加谱面到存档失败";
        public const string FailedToDeleteTemporaryBeatmapFile = "Failed to delete the temporary beatmap file|删除临时谱面文件失败";
        public const string Warning = "Warning|警告";

        // Conversion messages
        public const string ConversionFailedAllFiles = "Conversion failed, all files could not be converted successfully.|转换失败，所有文件都未能成功转换。";
        public const string ConversionNoOutput = "Conversion did not produce any output.|转换未产生任何输出。";

        // PreviewViewDual
        public const string PreviewTitle = "Preview|预览";
        public const string OriginalHint = "Original|原始";
        public const string ConvertedHint = "Converted|结果";
        public const string DropHint = "Drag & Drop .osu files in here|将 .osu 文件拖到此区域";
        public const string StartButtonText = "Start|开始转换";
        public const string DropFilesHint = "{0} file(s) staged. Click Start to convert.|已暂存 {0} 个文件，点击开始转换。";
        public const string NoDataAvailable = "No data available|无可用数据";
        public const string NoProcessorSet = "No processor set|Processor为空";
        public const string PreviewError = "Preview error: {0}";
        public const string PreviewBuildFailed = "Preview build failed: {0}";
        public const string DirectoryEnumerateFailed = "Directory enumerate failed for '{0}': {1}";
        public const string AutoLoadSampleFailed = "Auto-load sample failed: {0}";
        public const string BroadcastStagedPathsFailed = "公开暂存路径失败: {0}";

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

}