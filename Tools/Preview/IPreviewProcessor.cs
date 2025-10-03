using System.Windows;
using krrTools.Beatmaps;

namespace krrTools.Tools.Preview
{
    /// <summary>
    /// 预览实例接口
    /// </summary>
    public interface IPreviewProcessor
    {
        string? CurrentTool { get; set; }

        FrameworkElement BuildConvertedVisual(ManiaBeatmap input);
        FrameworkElement BuildOriginalVisual(ManiaBeatmap input);
    }
}

