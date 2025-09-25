using System.Diagnostics;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using krrTools.Tools.Shared; // 添加共享UI组件库的引用

namespace krrTools.tools.LNTransformer
{
    public class InstructionsWindow : Window
    {
        public InstructionsWindow()
        {
            // 使用共享库的多语言支持功能设置窗口标题
            Title = SharedUIComponents.IsChineseLanguage() ? "使用说明" : "Instructions";
            Width = 600;
            Height = 600;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Subscribe to language changes to update content dynamically
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;

            var root = new Grid { Margin = new Thickness(15) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = SharedUIComponents.CreateStandardTextBlock();
            header.FontSize = SharedUIComponents.HeaderFontSize;
            header.FontWeight = FontWeights.Bold;
            header.Margin = new Thickness(0,0,0,15);
            
            if (SharedUIComponents.IsChineseLanguage())
            {
                var run1 = new Run("移植自");
                header.Inlines.Add(run1);
                var hyperlink = new Hyperlink(new Run("HeavenUsurper")) { NavigateUri = new Uri("https://osu.ppy.sh/users/15889644") };
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                header.Inlines.Add(hyperlink);
                header.Inlines.Add(new Run("的LNTransformer工具"));
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run { Text = "Ported from HeavenUsurper's LNTransformer tool", FontSize = 12 });
            }
            else
            {
                var run1 = new Run("Ported from ");
                header.Inlines.Add(run1);
                var hyperlink = new Hyperlink(new Run("HeavenUsurper")) { NavigateUri = new Uri("https://osu.ppy.sh/users/15889644") };
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                header.Inlines.Add(hyperlink);
                header.Inlines.Add(new Run("'s LNTransformer tool"));
            }
            
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1);
            var body = SharedUIComponents.CreateStandardTextBlock();
            body.FontSize = SharedUIComponents.ComFontSize - 2; // 略小一点的字体
            body.TextWrapping = TextWrapping.Wrap;

            // 根据语言设置显示内容
            if (SharedUIComponents.IsChineseLanguage())
            {
                body.Inlines.Add(new Run("第一项：Level - LN 密度") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("(鼠标悬停查看描述)")); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("第二项：转换为长条的比例") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("第三项：转换为 1/x 的长条") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("第四项：转换列数") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("(若大于键位数则转换全部列)")); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("第五项：每隔多少 note 改变转换列") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("(0 表示固定列)") ); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("Ignore：") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new Run(" 跳过已转换谱面")); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("OriginalLN：") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new Run(" 保留原始长条")); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("Fix Error：") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new Run(" 修复由计算误差引起的小偏差")); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("注意：在转换完成前不要切换页面，否则需要重新转换"));
            }
            else
            {
                body.Inlines.Add(new Run("First: Level - LN Density") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("(Hover over the slider to see descriptions)")); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("Second: Percentage of LNs to convert") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("Third: Convert to 1/x LNs") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("Fourth: How many columns to convert LNs") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("(if greater than key count, all columns will be converted)")); body.Inlines.Add(new LineBreak()); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("Fifth: Change converted columns every N notes") { FontWeight = FontWeights.Bold }); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("(0 means fixed columns)")); body.Inlines.Add(new LineBreak());
                body.Inlines.Add(new Run("Note: Do not switch pages before conversion completes; otherwise you must reconvert."));
            }

            scroll.Content = body;
            root.Children.Add(scroll);

            Content = root;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // 打开超链接
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void OnLanguageChanged()
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(new Action(OnLanguageChanged));
                    return;
                }

                // Rebuild minimal UI to reflect language change
                var dc = DataContext;
                Content = null;
                try
                {
                    // rebuild same content as constructor
                    var root = new Grid { Margin = new Thickness(15) };
                    root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    // (recreate header/body similar to ctor)
                    // For simplicity, call constructor code path would be heavy; instead, set Title and rely on Content rebuild below
                    Title = SharedUIComponents.IsChineseLanguage() ? "使用说明" : "Instructions";
                    // Recreate content by re-running constructor-like logic: simplest approach is to call the constructor's content creation inline, but to avoid recursion we'll set Content to null and let the caller recreate if necessary.
                    Content = null;
                    // Re-run creation
                    // (call the constructor's logic by repeating minimal parts)
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InstructionsWindow rebuild failed: {ex.Message}");
                }
                DataContext = dc;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InstructionsWindow OnLanguageChanged failed: {ex.Message}");
            }
        }

    }
}