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
            Background = Brushes.Transparent,
            Height = 32,
            BorderThickness = new Thickness(0),
            BorderBrush = SharedUIComponents.PanelBorderBrush,
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
            Content = "_",
            Width = 46,
            Height = 32,
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.Black,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Top,
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
                    if (window is { WindowState: WindowState.Normal, IsVisible: true })
                        window.DragMove();
                }
            }
        };

        // 为标题文本也添加拖拽支持
        _titleTextBlock.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                if (window is { WindowState: WindowState.Normal, IsVisible: true })
                    window.DragMove();
            }
        };

        // Ensure the entire title bar supports dragging
        _titleBar.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                if (window is { WindowState: WindowState.Normal, IsVisible: true })
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

    public static Border CreateStatusBar(Window window, ToggleSwitch? realTimeToggle = null, Button? listenerBtn = null)
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

        // 左侧：版权信息和GitHub链接
        var leftPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };

        var copyrightText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        void UpdateCopyrightText()
        {
            copyrightText.Text = SharedUIComponents.IsChineseLanguage() ? Strings.FooterCopyrightCN : Strings.FooterCopyright;
        }

        UpdateCopyrightText(); // 初始化文本
        SharedUIComponents.LanguageChanged += UpdateCopyrightText;
        copyrightText.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateCopyrightText;

        var githubLink = new Hyperlink(new Run(Strings.GitHubLinkText))
        {
            NavigateUri = new Uri(Strings.GitHubLinkUrl)
        };
        githubLink.RequestNavigate += Hyperlink_RequestNavigate;
        var githubTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0)
        };
        githubTextBlock.Inlines.Add(githubLink);

        leftPanel.Children.Add(copyrightText);
        leftPanel.Children.Add(githubTextBlock);

        Grid.SetColumn(leftPanel, 0);
        footerGrid.Children.Add(leftPanel);

        var themeComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(ApplicationTheme)),
            SelectedItem = SharedUIComponents.GetSavedApplicationTheme() != null && Enum.TryParse<ApplicationTheme>(SharedUIComponents.GetSavedApplicationTheme(), out var savedTheme) ? savedTheme : ApplicationTheme.Light,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var backdropComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(WindowBackdropType)),
            SelectedItem = SharedUIComponents.GetSavedWindowBackdropType() != null && Enum.TryParse<WindowBackdropType>(SharedUIComponents.GetSavedWindowBackdropType(), out var savedBackdrop) ? savedBackdrop : WindowBackdropType.Mica,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var accentSwitch = new ToggleSwitch
        {
            IsChecked = SharedUIComponents.GetSavedUpdateAccent() ?? false,
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
                window.InvalidateVisual();
            }
        }

        themeComboBox.SelectionChanged += (_, _) => { 
            ApplyThemeSettings(accentSwitch.IsChecked == true);
            if (themeComboBox.SelectedItem is ApplicationTheme theme) SharedUIComponents.SetSavedApplicationTheme(theme.ToString());
        };
        backdropComboBox.SelectionChanged += (_, _) => { 
            ApplyThemeSettings(accentSwitch.IsChecked == true);
            if (backdropComboBox.SelectedItem is WindowBackdropType backdrop) SharedUIComponents.SetSavedWindowBackdropType(backdrop.ToString());
        };
        accentSwitch.Checked += (_, _) => { 
            ApplyThemeSettings(updateAccent: true); 
            SharedUIComponents.SetSavedUpdateAccent(true);
        };
        accentSwitch.Unchecked += (_, _) => { 
            ApplyThemeSettings(updateAccent: false); 
            SharedUIComponents.SetSavedUpdateAccent(false);
        };

        var langSwitch = new ToggleSwitch
        {
            IsChecked = SharedUIComponents.IsChineseLanguage(),
            Margin = new Thickness(4, 0, 12, 0),
            MinWidth = 60
        };

        void UpdateLanguageSwitchText()
        {
            langSwitch.Content = Strings.Localize(Strings.SettingsMenuLanguage);
            langSwitch.InvalidateVisual();
        }

        UpdateLanguageSwitchText();
        SharedUIComponents.LanguageChanged += UpdateLanguageSwitchText;
        langSwitch.Unloaded += (_, _) => SharedUIComponents.LanguageChanged -= UpdateLanguageSwitchText;

        langSwitch.Checked += (_, _) => SharedUIComponents.ToggleLanguage();
        langSwitch.Unchecked += (_, _) => SharedUIComponents.ToggleLanguage();

        // 创建设置按钮（齿轮图标）
        var settingsButton = new Button
        {
            Content = "⚙",
            Width = Double.NaN,
            Height = 32,
            Margin = new Thickness(0, 0, 12, 0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16
        };

        // 创建设置菜单
        var settingsMenu = new ContextMenu();

        // 主题子菜单
        var themeMenuItem = new System.Windows.Controls.MenuItem { Header = Strings.Localize(Strings.SettingsMenuTheme) };
        foreach (ApplicationTheme theme in Enum.GetValues(typeof(ApplicationTheme)))
        {
            string themeHeader = theme switch
            {
                ApplicationTheme.Light => Strings.Localize(Strings.ThemeLight),
                ApplicationTheme.Dark => Strings.Localize(Strings.ThemeDark),
                ApplicationTheme.HighContrast => Strings.Localize(Strings.ThemeHighContrast),
                _ => theme.ToString()
            };
            var themeItem = new System.Windows.Controls.MenuItem
            {
                Header = themeHeader, IsCheckable = true,
                IsChecked = theme == (themeComboBox.SelectedItem as ApplicationTheme? ?? ApplicationTheme.Light)
            };
            themeItem.Click += (s, e) => {
                // 取消其他主题项的选中
                foreach (var item in themeMenuItem.Items)
                {
                    if (item is System.Windows.Controls.MenuItem mi)
                        mi.IsChecked = false;
                }
                themeItem.IsChecked = true;
                themeComboBox.SelectedItem = theme;
            };
            themeMenuItem.Items.Add(themeItem);
        }
        settingsMenu.Items.Add(themeMenuItem);

        // 背景效果子菜单
        var backdropMenuItem = new System.Windows.Controls.MenuItem { Header = Strings.Localize(Strings.SettingsMenuBackdrop) };
        foreach (WindowBackdropType backdrop in Enum.GetValues(typeof(WindowBackdropType)))
        {
            string backdropHeader = backdrop switch
            {
                WindowBackdropType.None => Strings.Localize(Strings.BackdropNone),
                WindowBackdropType.Mica => Strings.Localize(Strings.BackdropMica),
                WindowBackdropType.Acrylic => Strings.Localize(Strings.BackdropAcrylic),
                WindowBackdropType.Tabbed => Strings.Localize(Strings.BackdropTabbed),
                _ => backdrop.ToString()
            };
            var backdropItem = new System.Windows.Controls.MenuItem
            {
                Header = backdropHeader, IsCheckable = true,
                IsChecked = backdrop == (backdropComboBox.SelectedItem as WindowBackdropType? ?? WindowBackdropType.Mica)
            };
            backdropItem.Click += (s, e) => {
                // 取消其他背景效果项的选中
                foreach (var item in backdropMenuItem.Items)
                {
                    if (item is System.Windows.Controls.MenuItem mi)
                        mi.IsChecked = false;
                }
                backdropItem.IsChecked = true;
                backdropComboBox.SelectedItem = backdrop;
            };
            backdropMenuItem.Items.Add(backdropItem);
        }
        settingsMenu.Items.Add(backdropMenuItem);

        // 强调色开关
        var accentMenuItem = new System.Windows.Controls.MenuItem
        {
            Header = Strings.Localize(Strings.UpdateAccent), IsCheckable = true,
            IsChecked = accentSwitch.IsChecked == true
        };
        accentMenuItem.Click += (s, e) => {
            accentSwitch.IsChecked = !accentSwitch.IsChecked;
        };
        settingsMenu.Items.Add(accentMenuItem);

        // 语言开关
        var langMenuItem = new System.Windows.Controls.MenuItem { Header = Strings.Localize(Strings.SettingsMenuLanguage), IsCheckable = false };
        langMenuItem.Click += (s, e) => {
            SharedUIComponents.ToggleLanguage();
            langMenuItem.Header = Strings.Localize(Strings.SettingsMenuLanguage);
        };
        settingsMenu.Items.Add(langMenuItem);

        // 语言改变时更新菜单项文本
        void UpdateMenuItemTexts()
        {
            themeMenuItem.Header = Strings.Localize(Strings.SettingsMenuTheme);
            backdropMenuItem.Header = Strings.Localize(Strings.SettingsMenuBackdrop);
            accentMenuItem.Header = Strings.Localize(Strings.UpdateAccent);
            langMenuItem.Header = Strings.Localize(Strings.SettingsMenuLanguage);
            
            // 更新主题子项
            int themeIndex = 0;
            foreach (ApplicationTheme theme in Enum.GetValues(typeof(ApplicationTheme)))
            {
                if (themeIndex < themeMenuItem.Items.Count)
                {
                    string themeHeader = theme switch
                    {
                        ApplicationTheme.Light => Strings.Localize(Strings.ThemeLight),
                        ApplicationTheme.Dark => Strings.Localize(Strings.ThemeDark),
                        ApplicationTheme.HighContrast => Strings.Localize(Strings.ThemeHighContrast),
                        _ => theme.ToString()
                    };
                    ((System.Windows.Controls.MenuItem)themeMenuItem.Items[themeIndex]).Header = themeHeader;
                }
                themeIndex++;
            }
            
            // 更新背景效果子项
            int backdropIndex = 0;
            foreach (WindowBackdropType backdrop in Enum.GetValues(typeof(WindowBackdropType)))
            {
                if (backdropIndex < backdropMenuItem.Items.Count)
                {
                    string backdropHeader = backdrop switch
                    {
                        WindowBackdropType.None => Strings.Localize(Strings.BackdropNone),
                        WindowBackdropType.Mica => Strings.Localize(Strings.BackdropMica),
                        WindowBackdropType.Acrylic => Strings.Localize(Strings.BackdropAcrylic),
                        WindowBackdropType.Tabbed => Strings.Localize(Strings.BackdropTabbed),
                        _ => backdrop.ToString()
                    };
                    ((System.Windows.Controls.MenuItem)backdropMenuItem.Items[backdropIndex]).Header = backdropHeader;
                }
                backdropIndex++;
            }
        }

        SharedUIComponents.LanguageChanged += UpdateMenuItemTexts;

        settingsButton.ContextMenu = settingsMenu;
        settingsButton.Click += (s, e) => settingsButton.ContextMenu.IsOpen = true;

        var settingsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        // 如果提供了实时预览开关，添加到面板
        if (realTimeToggle != null)
        {
            realTimeToggle.Margin = new Thickness(0, 0, 8, 0);
            settingsPanel.Children.Add(realTimeToggle);
        }
        
        // 如果提供了监听按钮，添加到面板
        if (listenerBtn != null)
        {
            listenerBtn.Width = 110;
            listenerBtn.Height = 32;
            listenerBtn.Margin = new Thickness(0, 0, 8, 0);
            settingsPanel.Children.Add(listenerBtn);
        }
        
        settingsPanel.Children.Add(settingsButton);

        Grid.SetColumn(settingsPanel, 2);
        footerGrid.Children.Add(settingsPanel);

        footer.Child = footerGrid;
        
        // 注册语言改变事件
        SharedUIComponents.LanguageChanged += UpdateMenuItemTexts;
        
        // 清理事件（当footer卸载时）
        footer.Unloaded += (s, e) => SharedUIComponents.LanguageChanged -= UpdateMenuItemTexts;
        
        // 应用初始主题设置
        ApplyThemeSettings(accentSwitch.IsChecked == true);
        
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