namespace krrTools.Configuration
{
    /// <summary>
    /// 转换器工具枚举
    /// </summary>
    public enum ConverterEnum
    {
        N2NC,
        DP,
        KRRLN
    }

    /// <summary>
    /// 其他模块枚举
    /// </summary>
    public enum ModuleEnum
    {
        LVCalculator,
        FilesManager,
        Listener
    }

    /// <summary>
    /// 文件来源状态枚举
    /// </summary>
    public enum FileSource
    {
        None,
        Dropped,
        Listened
    }
}