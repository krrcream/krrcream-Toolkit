using System.Windows;

namespace krrTools.tools.Preview
{
    /// <summary>
    /// 预览实例接口
    /// </summary>
    public interface IPreviewProcessor
    {
        string ToolKey { get; } //标签

        FrameworkElement BuildOriginalVisual(string[] filePaths);

        FrameworkElement BuildConvertedVisual(string[] filePaths);
    }
}

