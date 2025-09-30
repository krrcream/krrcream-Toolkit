namespace krrTools.Tools.FilesManager;

public class FilesManagerInfo
{
    // TODO: 优化属性访问
    public string Title { get; set; } = string.Empty;
    public string Diff { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Keys { get; set; }
    public double OD { get; set; }
    public double HP { get; set; }
    public int BeatmapID { get; set; }
    public int BeatmapSetID { get; set; }
}