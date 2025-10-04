using System.Windows;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.Preview
{
    /// <summary>
    /// 预览实例接口
    /// </summary>
    public interface IPreviewProcessor
    {
        string? CurrentTool { get; set; }
        
        FrameworkElement BuildOriginalVisual(Beatmap input);
        
        FrameworkElement BuildConvertedVisual(Beatmap input);
    }
}

