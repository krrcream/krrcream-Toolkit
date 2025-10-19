using System;
using System.Collections.Generic;
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
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace krrTools.Utilities
{
    public class StatusBarControl : UserControl
    {
        private static readonly Brush PanelBackgroundBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255));
        private static readonly Brush PanelBorderBrush = new SolidColorBrush(Color.FromArgb(32, 0, 0, 0));
        private static readonly CornerRadius PanelCornerRadius = new(4);

        public ToggleButton TopmostToggle { get; private set; } = null!;
        public ToggleButton RealTimeToggle { get; private set; } = null!;

        private readonly StateBarManager _stateBarManager = null!;

        // 菜单项引用，用于语言切换时更新
        private MenuItem _themeMenuItem = null!;
        private MenuItem _backdropMenuItem = null!;
        private MenuItem _accentMenuItem = null!;
        private MenuItem _langMenuItem = null!;
        private readonly List<MenuItem> _themeSubItems = new();
        private readonly List<MenuItem> _backdropSubItems = new();

        public StatusBarControl()
        {
            InitializeComponent();
            LocalizationService.LanguageChanged += OnLanguageChanged;
        }

        public StatusBarControl(StateBarManager stateBarManager)
        {
            _stateBarManager = stateBarManager;
            InitializeComponent();
            LocalizationService.LanguageChanged += OnLanguageChanged;
            
            // 设置实时预览开关的初始状态和点击事件
            RealTimeToggle.IsChecked = stateBarManager.IsMonitoringEnable;
            RealTimeToggle.Click += (_, _) => 
            {
                stateBarManager.IsMonitoringEnable = RealTimeToggle.IsChecked ?? false;
            };
        }

        private void InitializeComponent()
        {
            // 主容器：Grid，包含进度条行和状态栏行
            var rootGrid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto }, // 进度条行
                    new RowDefinition { Height = GridLength.Auto }  // 状态栏行
                }
            };

            // 进度条
            var progressBar = BuildProgressBar();
            System.Windows.Controls.Grid.SetRow(progressBar, 0);
            rootGrid.Children.Add(progressBar);

            // 状态栏Border
            var footer = new Border
            {
                Background = PanelBackgroundBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                Height = double.NaN, // 动态高度
                MinHeight = 32,
                Padding = new Thickness(0, 4, 0, 4),
                Margin = new Thickness(0, 0, 0, 0),
                CornerRadius = PanelCornerRadius
            };
            System.Windows.Controls.Grid.SetRow(footer, 1);

            // 用 Grid 实现6列分布，右侧按钮分别贴右
            var mainGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto }, // copyrightButton
                    new ColumnDefinition { Width = GridLength.Auto }, // githubLink
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // 占位
                    new ColumnDefinition { Width = GridLength.Auto }, // TopmostToggle
                    new ColumnDefinition { Width = GridLength.Auto }, // RealTimeToggle
                    new ColumnDefinition { Width = GridLength.Auto } // settingsButton
                }
            };

            // 左侧内容
            var hyperlinkButton = new HyperlinkButton
            {
                NavigateUri = Strings.KrrcreamUrl,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            hyperlinkButton.SetBinding(ContentProperty,
                new Binding("Value") { Source = Strings.FooterCopyright.GetLocalizedString() });
            System.Windows.Controls.Grid.SetColumn(hyperlinkButton, 0);

            var githubLink = new HyperlinkButton
            {
                Content = Strings.GitHubLinkText,
                NavigateUri = Strings.GitHubLinkUrl,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            System.Windows.Controls.Grid.SetColumn(githubLink, 1);

            // 右侧内容
            TopmostToggle = new ToggleButton
            {
                Content = new SymbolIcon { Symbol = SymbolRegular.PinOff20 },
                Width = 60,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "置顶|Topmost",
            };
            System.Windows.Controls.Grid.SetColumn(TopmostToggle, 3);

            RealTimeToggle = new ToggleButton
            {
                Width = 200,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            System.Windows.Controls.Grid.SetColumn(RealTimeToggle, 4);

            var settingsButton = CreateSettingsButton();
            settingsButton.HorizontalAlignment = HorizontalAlignment.Right;
            settingsButton.VerticalAlignment = VerticalAlignment.Center;
            System.Windows.Controls.Grid.SetColumn(settingsButton, 5);

            mainGrid.Children.Add(hyperlinkButton);
            mainGrid.Children.Add(githubLink);
            mainGrid.Children.Add(TopmostToggle);
            mainGrid.Children.Add(RealTimeToggle);
            mainGrid.Children.Add(settingsButton);

            footer.Child = mainGrid;
            rootGrid.Children.Add(footer);

            Content = rootGrid;
        }

        private ProgressBar BuildProgressBar()
        {
            var progressBar = new ProgressBar
            {
                Height = 4,
                Width = Double.NaN,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0),
                Minimum = 0,
                Maximum = 100,
                Background = Brushes.Transparent,
                Foreground = Brushes.Coral,
                BorderThickness = new Thickness(0),
            };

            progressBar.SetBinding(RangeBase.ValueProperty, new Binding(nameof(StateBarManager.ProgressValue) + ".Value") { Source = _stateBarManager });
            progressBar.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(StateBarManager.ProgressVisible) + ".Value") { Source = _stateBarManager, Converter = new BooleanToVisibilityConverter() });

            return progressBar;
        }

        private Button CreateSettingsButton()
        {
            var settingsButton = new Button
            {
                Content = new SymbolIcon { Symbol = SymbolRegular.Settings20 },
                Width = 40,
                Height = 32,
                Margin = new Thickness(0, 0, 12, 0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };

            var settingsMenu = CreateSettingsMenu();
            settingsButton.ContextMenu = settingsMenu;
            settingsButton.Click += (_, _) => settingsButton.ContextMenu.IsOpen = true;

            return settingsButton;
        }

        private ContextMenu CreateSettingsMenu()
        {
            var settingsMenu = new ContextMenu();

            _themeMenuItem = CreateThemeMenuItem();
            settingsMenu.Items.Add(_themeMenuItem);

            _backdropMenuItem = CreateBackdropMenuItem();
            settingsMenu.Items.Add(_backdropMenuItem);

            _accentMenuItem = CreateAccentMenuItem();
            settingsMenu.Items.Add(_accentMenuItem);

            _langMenuItem = CreateLanguageMenuItem();
            settingsMenu.Items.Add(_langMenuItem);

            return settingsMenu;
        }

        private MenuItem CreateThemeMenuItem()
        {
            var themeMenuItem = new MenuItem
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
                var themeItem = new MenuItem
                {
                    Header = themeHeader,
                    IsCheckable = true,
                    IsChecked = theme == GetSavedApplicationTheme(),
                    Tag = theme
                };
                themeItem.Click += (_, _) =>
                {
                    foreach (var item in themeMenuItem.Items)
                        if (item is MenuItem mi)
                            mi.IsChecked = false;
                    themeItem.IsChecked = true;
                    BaseOptionsManager.SetApplicationTheme(theme.ToString());
                    ApplyThemeSettings();
                };
                themeMenuItem.Items.Add(themeItem);
                _themeSubItems.Add(themeItem);
            }

            return themeMenuItem;
        }

        private MenuItem CreateBackdropMenuItem()
        {
            var backdropMenuItem = new MenuItem
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
                var backdropItem = new MenuItem
                {
                    Header = backdropHeader,
                    IsCheckable = true,
                    IsChecked = backdrop == GetSavedWindowBackdropType(),
                    Tag = backdrop
                };
                backdropItem.Click += (_, _) =>
                {
                    foreach (var item in backdropMenuItem.Items)
                        if (item is MenuItem mi)
                            mi.IsChecked = false;
                    backdropItem.IsChecked = true;
                    BaseOptionsManager.SetWindowBackdropType(backdrop.ToString());
                    ApplyThemeSettings();
                };
                backdropMenuItem.Items.Add(backdropItem);
                _backdropSubItems.Add(backdropItem);
            }

            return backdropMenuItem;
        }

        private static MenuItem CreateAccentMenuItem()
        {
            var accentMenuItem = new MenuItem
            {
                Header = Strings.Localize(Strings.UpdateAccent), IsCheckable = true,
                IsChecked = GetSavedUpdateAccent() ?? false
            };
            accentMenuItem.Click += (_, _) =>
            {
                accentMenuItem.IsChecked = accentMenuItem.IsChecked;
                BaseOptionsManager.SetUpdateAccent(accentMenuItem.IsChecked);
                ApplyThemeSettings();
            };
            return accentMenuItem;
        }

        private static MenuItem CreateLanguageMenuItem()
        {
            var langMenuItem = new MenuItem
                { Header = Strings.Localize(Strings.SettingsMenuLanguage), IsCheckable = false };
            langMenuItem.Click += (_, _) =>
            {
                LocalizationService.ToggleLanguage();
                langMenuItem.Header = Strings.Localize(Strings.SettingsMenuLanguage);
            };
            return langMenuItem;
        }

        public static void ApplyThemeSettings()
        {
            var theme = GetSavedApplicationTheme();
            var backdrop = GetSavedWindowBackdropType();
            var updateAccent = GetSavedUpdateAccent() ?? false;
            ApplicationThemeManager.Apply(theme, backdrop, updateAccent);
        }

        private static ApplicationTheme GetSavedApplicationTheme() =>
            Enum.TryParse<ApplicationTheme>(BaseOptionsManager.GetApplicationTheme(), out var theme) ? theme : ApplicationTheme.Light;

        private static WindowBackdropType GetSavedWindowBackdropType() =>
            Enum.TryParse<WindowBackdropType>(BaseOptionsManager.GetWindowBackdropType(), out var backdrop) ? backdrop : WindowBackdropType.Acrylic;

        private static bool? GetSavedUpdateAccent() => BaseOptionsManager.GetUpdateAccent();

        private void OnLanguageChanged()
        {
            _themeMenuItem.Header = Strings.Localize(Strings.SettingsMenuTheme);
            _backdropMenuItem.Header = Strings.Localize(Strings.SettingsMenuBackdrop);
            _accentMenuItem.Header = Strings.Localize(Strings.UpdateAccent);
            _langMenuItem.Header = Strings.Localize(Strings.SettingsMenuLanguage);

            // 更新主题子项
            foreach (var item in _themeSubItems)
            {
                if (item.Tag is ApplicationTheme theme)
                {
                    item.Header = theme switch
                    {
                        ApplicationTheme.Light => Strings.Localize(Strings.ThemeLight),
                        ApplicationTheme.Dark => Strings.Localize(Strings.ThemeDark),
                        ApplicationTheme.HighContrast => Strings.Localize(Strings.ThemeHighContrast),
                        _ => theme.ToString()
                    };
                }
            }

            // 更新背景子项
            foreach (var item in _backdropSubItems)
            {
                if (item.Tag is WindowBackdropType backdrop)
                {
                    item.Header = backdrop switch
                    {
                        WindowBackdropType.None => Strings.Localize(Strings.BackdropNone),
                        WindowBackdropType.Mica => Strings.Localize(Strings.BackdropMica),
                        WindowBackdropType.Acrylic => Strings.Localize(Strings.BackdropAcrylic),
                        WindowBackdropType.Tabbed => Strings.Localize(Strings.BackdropTabbed),
                        _ => backdrop.ToString()
                    };
                }
            }
        }
    }
}