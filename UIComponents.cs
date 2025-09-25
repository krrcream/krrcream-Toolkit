using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;
using krrTools.Tools.Shared;
using krrTools.tools.Shared;

namespace krrTools;

public static class UIComponents
{
    // 标题栏控件
    private static Button _minimizeButton = null!;
    private static Button _maximizeButton = null!;
    private static Button _closeButton = null!;
    private static Border _titleBar = null!;
    private static TextBlock? _titleTextBlock;

    public static Border CreateTitleBar(Window window, string title)
    {
        // 创建标题栏容器
        _titleBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
            Height = 32,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            Margin = new Thickness(0, 0, 0, 0)
        };
        // 确保标题栏在左侧和右侧留出系统按钮的空间
        _titleBar.SetValue(System.Windows.Shell.WindowChrome.IsHitTestVisibleInChromeProperty, true);

        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 标题文本
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 按钮区域

        // 标题文本
        _titleTextBlock = new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0),
            FontWeight = FontWeights.Medium
        };
        Grid.SetColumn(_titleTextBlock, 0);
        titleGrid.Children.Add(_titleTextBlock);

        // 按钮容器
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(buttonPanel, 1);

        // 最小化按钮
        _minimizeButton = new Button
        {
            Content = "—",
            Width = 46,
            Height = 32,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.Black,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        // 确保按钮在标题栏区域内可点击
        _minimizeButton.SetValue(System.Windows.Shell.WindowChrome.IsHitTestVisibleInChromeProperty, true);
        _minimizeButton.Click += (_, _) => window.WindowState = WindowState.Minimized;
        buttonPanel.Children.Add(_minimizeButton);

        // 最大化/还原按钮
        _maximizeButton = new Button
        {
            Content = "□",
            Width = 46,
            Height = 32,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.Black,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        // 确保按钮在标题栏区域内可点击
        _maximizeButton.SetValue(System.Windows.Shell.WindowChrome.IsHitTestVisibleInChromeProperty, true);
        _maximizeButton.Click += (_, _) =>
        {
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            _maximizeButton.Content = window.WindowState == WindowState.Maximized ? "❐" : "□";
        };
        buttonPanel.Children.Add(_maximizeButton);

        // 关闭按钮
        _closeButton = new Button
        {
            Content = "×",
            Width = 46,
            Height = 32,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.Black,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        // 确保按钮在标题栏区域内可点击
        _closeButton.SetValue(System.Windows.Shell.WindowChrome.IsHitTestVisibleInChromeProperty, true);

        // 为关闭按钮添加悬停效果
        _closeButton.MouseEnter += delegate { _closeButton.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35)); };
        _closeButton.MouseLeave += delegate { _closeButton.Background = Brushes.Transparent; };
        _closeButton.Foreground = new SolidColorBrush(Colors.Black);

        // 为最小化和最大化按钮添加悬停效果
        _minimizeButton.MouseEnter += delegate { _minimizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)); };
        _minimizeButton.MouseLeave += delegate { _minimizeButton.Background = Brushes.Transparent; };

        _maximizeButton.MouseEnter += delegate { _maximizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)); };
        _maximizeButton.MouseLeave += delegate { _maximizeButton.Background = Brushes.Transparent; };
        _closeButton.Click += delegate { window.Close(); };

        buttonPanel.Children.Add(_closeButton);

        // 添加按钮的按下效果
        _minimizeButton.PreviewMouseDown += delegate { _minimizeButton.Background = new SolidColorBrush(Color.FromRgb(180, 180, 180)); };
        _minimizeButton.PreviewMouseUp += delegate { _minimizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)); };

        _maximizeButton.PreviewMouseDown += delegate { _maximizeButton.Background = new SolidColorBrush(Color.FromRgb(180, 180, 180)); };
        _maximizeButton.PreviewMouseUp += delegate { _maximizeButton.Background = new SolidColorBrush(Color.FromRgb(220, 220, 220)); };

        _closeButton.PreviewMouseDown += delegate { _closeButton.Background = new SolidColorBrush(Color.FromRgb(200, 15, 30)); };
        _closeButton.PreviewMouseUp += delegate { _closeButton.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35)); };

        titleGrid.Children.Add(buttonPanel);

        // 添加鼠标事件以支持拖拽窗口
        titleGrid.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // 双击标题栏切换最大化状态
                    window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    _maximizeButton.Content = window.WindowState == WindowState.Maximized ? "❐" : "□";
                }
                else
                {
                    // 拖拽窗口
                    if (window.WindowState == WindowState.Normal && window.IsVisible)
                        window.DragMove();
                }
            }
        };

        // 为标题文本也添加拖拽支持
        _titleTextBlock.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                if (window.WindowState == WindowState.Normal && window.IsVisible)
                    window.DragMove();
            }
        };

        // Ensure the entire title bar supports dragging
        _titleBar.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                if (window.WindowState == WindowState.Normal && window.IsVisible)
                    window.DragMove();
            }
        };

        _titleBar.Child = titleGrid;

        // 当窗口状态改变时更新按钮内容
        window.StateChanged += (s, e) =>
        {
            _maximizeButton.Content = window.WindowState == WindowState.Maximized ? "❐" : "□";
        };

        // 初始化按钮内容
        _maximizeButton.Content = window.WindowState == WindowState.Maximized ? "❐" : "□";

        return _titleBar;
    }

    public static Border CreateStatusBar()
    {
        var footer = new Border
        {
            Background = SharedUIComponents.PanelBackgroundBrush,
            BorderBrush = SharedUIComponents.PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Height = double.NaN, // 动态高度
            MinHeight = 24,
            Padding = new Thickness(0, 4, 0, 4),
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = SharedUIComponents.PanelCornerRadius
        };

        var footerGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 左侧：版权信息
        var copyrightText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };

        void UpdateCopyrightText()
        {
            copyrightText.Text = SharedUIComponents.IsChineseLanguage() ? Strings.FooterCopyrightCN : Strings.FooterCopyright;
        }

        UpdateCopyrightText(); // 初始化文本
        SharedUIComponents.LanguageChanged += UpdateCopyrightText;
        copyrightText.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateCopyrightText;

        Grid.SetColumn(copyrightText, 0);
        footerGrid.Children.Add(copyrightText);

        // 中间：GitHub链接
        var githubLink = new Hyperlink(new Run(Strings.GitHubLinkText))
        {
            NavigateUri = new Uri(Strings.GitHubLinkUrl)
        };
        githubLink.RequestNavigate += Hyperlink_RequestNavigate;
        var githubTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 150, 0)
        };
        githubTextBlock.Inlines.Add(githubLink);
        Grid.SetColumn(githubTextBlock, 1);
        footerGrid.Children.Add(githubTextBlock);

        var themeComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(ApplicationTheme)),
            SelectedItem = ApplicationTheme.Light,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var backdropComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(WindowBackdropType)),
            SelectedItem = WindowBackdropType.Mica,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var accentSwitch = new ToggleSwitch
        {
            IsChecked = false,
            Content = Strings.Localize(Strings.UpdateAccent),
            Margin = new Thickness(4, 0, 4, 0)
        };

        // 统一应用主题和背景效果的方法
        void ApplyThemeSettings(bool updateAccent = false)
        {
            if (themeComboBox.SelectedItem is ApplicationTheme selectedTheme &&
                backdropComboBox.SelectedItem is WindowBackdropType selectedBackdrop)
            {
                ApplicationThemeManager.Apply(selectedTheme, selectedBackdrop, updateAccent);
            }
        }

        themeComboBox.SelectionChanged += (_, _) => { ApplyThemeSettings(); };
        backdropComboBox.SelectionChanged += (_, _) => { ApplyThemeSettings(); };
        accentSwitch.Checked += (_, _) => { ApplyThemeSettings(updateAccent: true); };
        accentSwitch.Unchecked += (_, _) => { ApplyThemeSettings(updateAccent: false); };

        var langSwitch = new ToggleSwitch
        {
            IsChecked = SharedUIComponents.IsChineseLanguage(),
            Margin = new Thickness(4, 0, 12, 0),
            MinWidth = 60
        };

        void UpdateLanguageSwitchText()
        {
            langSwitch.Content = SharedUIComponents.IsChineseLanguage() ? "EN" : "中文";
        }

        UpdateLanguageSwitchText();
        SharedUIComponents.LanguageChanged += UpdateLanguageSwitchText;
        langSwitch.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateLanguageSwitchText;

        langSwitch.Checked += (_, _) => SharedUIComponents.ToggleLanguage();
        langSwitch.Unchecked += (_, _) => SharedUIComponents.ToggleLanguage();

        var settingsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        settingsPanel.Children.Add(themeComboBox);
        settingsPanel.Children.Add(backdropComboBox);
        settingsPanel.Children.Add(accentSwitch);
        settingsPanel.Children.Add(langSwitch);

        Grid.SetColumn(settingsPanel, 2);
        footerGrid.Children.Add(settingsPanel);

        footer.Child = footerGrid;
        return footer;
    }

    private static void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}