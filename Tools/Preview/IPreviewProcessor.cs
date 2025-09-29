using System.Windows;

namespace krrTools.Tools.Preview
{
    /// <summary>
    /// 预览实例接口
    /// </summary>
    public interface IPreviewProcessor
    {
        string ToolKey { get; } //标签

        string? CurrentTool { get; set; }

        FrameworkElement BuildOriginalVisual(string[] filePaths);

        FrameworkElement BuildConvertedVisual(string[] filePaths);
    }
}

