using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Configuration;
using krrTools.Localization;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Border = Wpf.Ui.Controls.Border;
using Button = Wpf.Ui.Controls.Button;
using Grid = Wpf.Ui.Controls.Grid;
using StackPanel = Wpf.Ui.Controls.StackPanel;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;
using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;

namespace krrTools.UI;

/// <summary>
/// 统一的UI控件库，为所有工具提供一致的界面组件和样式
/// </summary>
public static class SharedUIComponents
{
    public static event Action? LanguageChanged
    {
        add => LocalizationService.LanguageChanged += value;
        remove => LocalizationService.LanguageChanged -= value;
    }

    public static string? GetSavedApplicationTheme()
    {
        return BaseOptionsManager.GetApplicationTheme();
    }

    public static string? GetSavedWindowBackdropType()
    {
        return BaseOptionsManager.GetWindowBackdropType();
    }

    public static bool? GetSavedUpdateAccent()
    {
        return BaseOptionsManager.GetUpdateAccent();
    }

    // 统一UI样式相关常量
    private static readonly Brush UiTextBrush = UIConstants.UiTextBrush;
    public const double HeaderFontSize = UIConstants.HeaderFontSize;
    private const double ComFontSize = UIConstants.CommonFontSize;

    private static SolidColorBrush _panelBackgroundBrush = new(Color.FromArgb(102, 255, 255, 255));

    public static Brush PanelBackgroundBrush
    {
        get => _panelBackgroundBrush;
        set
        {
            if (value is SolidColorBrush scb) _panelBackgroundBrush = new SolidColorBrush(scb.Color);
            else _panelBackgroundBrush = new SolidColorBrush(_panelBackgroundBrush.Color);
        }
    }

    // Preview background settings
    private static double _previewBackgroundOpacity = 1.0;
    private static double _previewBackgroundBlurRadius = 2.0;

    public static double PreviewBackgroundOpacity
    {
        get => _previewBackgroundOpacity;
        set => _previewBackgroundOpacity = Math.Clamp(value, 0.0, 1.0);
    }

    public static double PreviewBackgroundBlurRadius
    {
        get => _previewBackgroundBlurRadius;
        set => _previewBackgroundBlurRadius = Math.Max(0.0, value);
    }

    public static readonly Brush PanelBorderBrush = UIConstants.PanelBorderBrush;
    public static readonly CornerRadius PanelCornerRadius = UIConstants.PanelCornerRadius;
    private static readonly Thickness PanelPadding = UIConstants.PanelPadding;

    public static Border CreateStandardPanel(UIElement inner, Thickness? margin = null, Thickness? padding = null)
    {
        var border = new Border
        {
            Background = PanelBackgroundBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = PanelCornerRadius,
            Padding = PanelPadding,
            Child = inner
        };

        if (margin.HasValue) border.Margin = margin.Value;
        if (padding.HasValue) border.Padding = padding.Value;
        return border;
    }

    public static string GetLocalizedEnumDisplayName<T>(T enumValue) where T : Enum
    {
        return LocalizationService.GetLocalizedEnumDisplayName(enumValue);
    }

    private static void SetLocalizedToolTip(FrameworkElement element, string? tooltipText)
    {
        LocalizationService.SetLocalizedToolTip(element, tooltipText);
    }

    public static TextBlock CreateStandardTextBlock()
    {
        return new TextBlock
        {
            FontSize = ComFontSize,
            Foreground = UiTextBrush
        };
    }

    public static TextBlock CreateHeaderLabel(string text)
    {
        var tb = new TextBlock { FontSize = HeaderFontSize, FontWeight = FontWeights.Bold };
        if (!string.IsNullOrEmpty(text) && text.Contains('|'))
            tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                new Binding("Value") { Source = text.GetLocalizedString() });
        else
            tb.Text = text;
        return tb;
    }

    public static FrameworkElement CreateLabeledRow(string labelText, UIElement control, Thickness rowMargin)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
        var label = CreateHeaderLabel(labelText);
        label.Margin = new Thickness(0, 0, 0, 4);
        panel.Children.Add(label);
        if (control is FrameworkElement fe) panel.Children.Add(fe);
        return panel;
    }

    public static Slider CreateStandardSlider(double minimum, double maximum, double height, bool isSnapToTickEnabled)
    {
        return new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Height = double.IsNaN(height) ? double.NaN : height,
            IsSnapToTickEnabled = isSnapToTickEnabled,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    public static TextBox CreateStandardTextBox()
    {
        return new TextBox
        {
            FontSize = ComFontSize,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderBrush = PanelBorderBrush,
            Padding = new Thickness(6)
        };
    }

    public static Button CreateStandardButton(string content, string? tooltip = null)
    {
        // Use a TextBlock as content so we can measure and shrink the font when needed.
        var tb = new TextBlock { FontSize = ComFontSize, TextTrimming = TextTrimming.CharacterEllipsis };
        // Bilingual support: if content contains '|', use binding
        if (!string.IsNullOrEmpty(content) && content.Contains('|'))
            tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                new Binding("Value") { Source = content.GetLocalizedString() });
        else
            tb.Text = content;
        var btn = new Button
        {
            Content = tb,
            // cap padding so distance from text to button edge is at most 5
            Padding = new Thickness(5),
            // small default margin to match app style
            Margin = new Thickness(5, 0, 0, 0),
            FontSize = ComFontSize,
            Background = PanelBackgroundBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = UiTextBrush
        };

        SetLocalizedToolTip(btn, tooltip);

        return btn;
    }

    public static CheckBox CreateStandardCheckBox(string content, string? tooltip = null)
    {
        var cb = new CheckBox
            { FontSize = ComFontSize, Margin = new Thickness(2, 0, 10, 0), Background = Brushes.Transparent };
        var tb = new TextBlock { FontSize = ComFontSize, TextWrapping = TextWrapping.Wrap };
        if (!string.IsNullOrEmpty(content) && content.Contains('|'))
            tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                new Binding("Value") { Source = content.GetLocalizedString() });
        else
            tb.Text = content;
        cb.Content = tb;
        SetLocalizedToolTip(cb, tooltip);
        return cb;
    }


    public static Border CreateStatusBar(Window window, ToggleSwitch? realTimeToggle = null,
        ToggleButton? listenerBtn = null)
    {
        var footer = CreateFooterBorder();
        var footerGrid = CreateFooterGrid();

        var leftPanel = CreateLeftPanel();
        System.Windows.Controls.Grid.SetColumn(leftPanel, 0);
        footerGrid.Children.Add(leftPanel);

        var settingsPanel = CreateSettingsPanel(realTimeToggle, listenerBtn);
        System.Windows.Controls.Grid.SetColumn(settingsPanel, 2);
        footerGrid.Children.Add(settingsPanel);

        footer.Child = footerGrid;

        ApplyThemeSettings();

        return footer;
    }

    private static Border CreateFooterBorder()
    {
        return new Border
        {
            Background = PanelBackgroundBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            Height = double.NaN, // 动态高度
            MinHeight = UIConstants.StatusBarMinHeight,
            Padding = new Thickness(0, 4, 0, 4),
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = PanelCornerRadius
        };
    }

    private static Grid CreateFooterGrid()
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        return grid;
    }

    private static StackPanel CreateLeftPanel()
    {
        var leftPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var copyrightButton = new HyperlinkButton
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            NavigateUri = Strings.KrrcreamUrl
        };
        copyrightButton.SetBinding(ContentControl.ContentProperty,
            new Binding("Value") { Source = Strings.FooterCopyright.GetLocalizedString() });

        var githubLink = new HyperlinkButton
        {
            Content = Strings.GitHubLinkText,
            NavigateUri = Strings.GitHubLinkUrl
        };
        githubLink.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Strings.GitHubLinkUrl,
            UseShellExecute = true
        });
        var githubTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        githubTextBlock.Inlines.Add(githubLink);

        leftPanel.Children.Add(copyrightButton);
        leftPanel.Children.Add(githubTextBlock);

        return leftPanel;
    }

    private static StackPanel CreateSettingsPanel(ToggleSwitch? realTimeToggle, ToggleButton? listenerBtn)
    {
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

        var settingsButton = CreateSettingsButton();
        settingsPanel.Children.Add(settingsButton);

        return settingsPanel;
    }

    private static Button CreateSettingsButton()
    {
        var settingsButton = new Button
        {
            Content = "⚙",
            Width = UIConstants.SettingsButtonWidth,
            Height = UIConstants.SettingsButtonHeight,
            Margin = new Thickness(0, 0, 12, 0),
            Background = UIConstants.TransparentBrush,
            BorderBrush = UIConstants.TransparentBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16
        };

        var settingsMenu = CreateSettingsMenu();
        settingsButton.ContextMenu = settingsMenu;
        settingsButton.Click += (_, _) => settingsButton.ContextMenu.IsOpen = true;

        return settingsButton;
    }

    private static ContextMenu CreateSettingsMenu()
    {
        var settingsMenu = new ContextMenu();

        var themeMenuItem = CreateThemeMenuItem();
        settingsMenu.Items.Add(themeMenuItem);

        var backdropMenuItem = CreateBackdropMenuItem();
        settingsMenu.Items.Add(backdropMenuItem);

        var accentMenuItem = CreateAccentMenuItem();
        settingsMenu.Items.Add(accentMenuItem);

        var langMenuItem = CreateLanguageMenuItem();
        settingsMenu.Items.Add(langMenuItem);

        return settingsMenu;
    }

    private static System.Windows.Controls.MenuItem CreateThemeMenuItem()
    {
        var themeMenuItem = new System.Windows.Controls.MenuItem
            { Header = Strings.Localize(Strings.SettingsMenuTheme) };
        foreach (ApplicationTheme theme in Enum.GetValues(typeof(ApplicationTheme)))
        {
            var themeHeader = theme switch
            {
                ApplicationTheme.Light => Strings.Localize(Strings.ThemeLight),
                ApplicationTheme.Dark => Strings.Localize(Strings.ThemeDark),
                ApplicationTheme.HighContrast => Strings.Localize(Strings.ThemeHighContrast),
                _ => theme.ToString()
            };
            var themeItem = new System.Windows.Controls.MenuItem
            {
                Header = themeHeader, IsCheckable = true,
                IsChecked = theme ==
                            (GetSavedApplicationTheme() != null &&
                             Enum.TryParse<ApplicationTheme>(GetSavedApplicationTheme(), out var savedTheme)
                                ? savedTheme
                                : ApplicationTheme.Light)
            };
            themeItem.Click += (_, _) =>
            {
                foreach (var item in themeMenuItem.Items)
                    if (item is System.Windows.Controls.MenuItem mi)
                        mi.IsChecked = false;
                themeItem.IsChecked = true;
                BaseOptionsManager.SetApplicationTheme(theme.ToString());
                ApplyThemeSettings();
            };
            themeMenuItem.Items.Add(themeItem);
        }

        return themeMenuItem;
    }

    private static System.Windows.Controls.MenuItem CreateBackdropMenuItem()
    {
        var backdropMenuItem = new System.Windows.Controls.MenuItem
            { Header = Strings.Localize(Strings.SettingsMenuBackdrop) };
        foreach (WindowBackdropType backdrop in Enum.GetValues(typeof(WindowBackdropType)))
        {
            var backdropHeader = backdrop switch
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
                IsChecked = backdrop ==
                            (GetSavedWindowBackdropType() != null &&
                             Enum.TryParse<WindowBackdropType>(GetSavedWindowBackdropType(), out var savedBackdrop)
                                ? savedBackdrop
                                : WindowBackdropType.Mica)
            };
            backdropItem.Click += (_, _) =>
            {
                foreach (var item in backdropMenuItem.Items)
                    if (item is System.Windows.Controls.MenuItem mi)
                        mi.IsChecked = false;
                backdropItem.IsChecked = true;
                BaseOptionsManager.SetWindowBackdropType(backdrop.ToString());
                ApplyThemeSettings();
            };
            backdropMenuItem.Items.Add(backdropItem);
        }

        return backdropMenuItem;
    }

    private static System.Windows.Controls.MenuItem CreateAccentMenuItem()
    {
        var accentMenuItem = new System.Windows.Controls.MenuItem
        {
            Header = Strings.Localize(Strings.UpdateAccent), IsCheckable = true,
            IsChecked = GetSavedUpdateAccent() ?? false
        };
        accentMenuItem.Click += (_, _) =>
        {
            accentMenuItem.IsChecked = !accentMenuItem.IsChecked;
            BaseOptionsManager.SetUpdateAccent(accentMenuItem.IsChecked);
            ApplyThemeSettings();
        };
        return accentMenuItem;
    }

    private static System.Windows.Controls.MenuItem CreateLanguageMenuItem()
    {
        var langMenuItem = new System.Windows.Controls.MenuItem
            { Header = Strings.Localize(Strings.SettingsMenuLanguage), IsCheckable = false };
        langMenuItem.Click += (_, _) =>
        {
            LocalizationService.ToggleLanguage();
            langMenuItem.Header = Strings.Localize(Strings.SettingsMenuLanguage);
        };
        return langMenuItem;
    }

    private static void ApplyThemeSettings()
    {
        var theme = GetSavedApplicationTheme() != null &&
                    Enum.TryParse<ApplicationTheme>(GetSavedApplicationTheme(), out var selectedTheme)
            ? selectedTheme
            : ApplicationTheme.Light;
        var backdrop =
            GetSavedWindowBackdropType() != null &&
            Enum.TryParse<WindowBackdropType>(GetSavedWindowBackdropType(), out var selectedBackdrop)
                ? selectedBackdrop
                : WindowBackdropType.Mica;
        var updateAccent = GetSavedUpdateAccent() ?? false;
        ApplicationThemeManager.Apply(theme, backdrop, updateAccent);
        // window.InvalidateVisual(); 需要window引用
    }
}