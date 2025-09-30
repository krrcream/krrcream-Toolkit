using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

namespace krrTools.UI
{
    /// <summary>
    /// 统一的UI控件库，为所有工具提供一致的界面组件和样式
    /// </summary>
    public static class SharedUIComponents
    {
        // Event raised when the effective language selection changes (so UI can update)
        public static event Action? LanguageChanged
        {
            add => LocalizationManager.LanguageChanged += value;
            remove => LocalizationManager.LanguageChanged -= value;
        }

        // Toggle the language selection (used by simple toggle UIs)
        public static void ToggleLanguage()
        {
            LocalizationManager.ToggleLanguage();
        }

        // Set saved theme settings
        public static void SetSavedApplicationTheme(string theme)
        {
            BaseOptionsManager.SetApplicationTheme(theme);
        }

        public static void SetSavedWindowBackdropType(string backdropType)
        {
            BaseOptionsManager.SetWindowBackdropType(backdropType);
        }

        public static void SetSavedUpdateAccent(bool updateAccent)
        {
            BaseOptionsManager.SetUpdateAccent(updateAccent);
        }

        // Get saved theme settings
        public static string? GetSavedApplicationTheme() => BaseOptionsManager.GetApplicationTheme();
        public static string? GetSavedWindowBackdropType() => BaseOptionsManager.GetWindowBackdropType();
        public static bool? GetSavedUpdateAccent() => BaseOptionsManager.GetUpdateAccent();

        // 统一UI样式相关常量
        private static readonly Brush UiTextBrush = UIConstants.UiTextBrush;
        public const double HeaderFontSize = UIConstants.HeaderFontSize;
        private const double ComFontSize = UIConstants.CommonFontSize;
        
        private static SolidColorBrush _panelBackgroundBrush = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255)); // ~40% white tint
        public static Brush PanelBackgroundBrush
        {
            get => _panelBackgroundBrush;
            set
            {
                if (value is SolidColorBrush scb) _panelBackgroundBrush = new SolidColorBrush(scb.Color);
                else _panelBackgroundBrush = new SolidColorBrush(_panelBackgroundBrush.Color);
            }
        }

        // 设置程序透明度，但是滑条空间没添加，先留着
        public static void SetPanelBackgroundAlpha(float alpha)
        {
            if (_panelBackgroundBrush.IsFrozen)
                _panelBackgroundBrush = _panelBackgroundBrush.Clone();
            var c = _panelBackgroundBrush.Color;
            _panelBackgroundBrush.Color = Color.FromScRgb(alpha, c.R, c.G, c.B);
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
         // Softer border for the frosted look
         public static readonly Brush PanelBorderBrush = UIConstants.PanelBorderBrush;
         public static readonly CornerRadius PanelCornerRadius = UIConstants.PanelCornerRadius;
         private static readonly Thickness PanelPadding = UIConstants.PanelPadding;

        /// <summary>
        /// Helper to create a rounded acrylic panel with common padding and border.
        /// </summary>
        private static Border CreateAcrylicPanel(UIElement inner)
        {
            return new Border
            {
                Background = PanelBackgroundBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = PanelCornerRadius,
                Padding = PanelPadding,
                Child = inner
            };
        }
        
        /// <summary>
        /// Creates an acrylic panel with common background, border, and corner radius, allowing optional margin and padding.
        /// </summary>
        public static Border CreateStandardPanel(UIElement inner, Thickness? margin = null, Thickness? padding = null)
        {
            var border = CreateAcrylicPanel(inner);
            if (margin.HasValue) border.Margin = margin.Value;
            if (padding.HasValue) border.Padding = padding.Value;
            return border;
        }
         /// <summary>
         /// 根据枚举的Description特性获取本地化显示名称
         /// </summary>
         public static string GetLocalizedEnumDisplayName<T>(T enumValue) where T : Enum
         {
             return LocalizationManager.GetLocalizedEnumDisplayName(enumValue);
         }

         /// <summary>
         /// 设置本地化的ToolTip
         /// </summary>
         private static void SetLocalizedToolTip(FrameworkElement element, string? tooltipText)
         {
             LocalizationManager.SetLocalizedToolTip(element, tooltipText);
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
             {
                 void Update()
                 {
                     tb.Dispatcher.Invoke(() =>
                     {
                         tb.Text = text.Localize();
                     });
                 }
                 Update();
                 LanguageChanged += Update;
                 tb.Unloaded += (_, _) => LanguageChanged -= Update;
             }
             else
             {
                 tb.Text = text;
             }
             return tb;
         }
  
         public static FrameworkElement CreateLabeledRow(string labelText, UIElement control, Thickness rowMargin)
         {
             var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
             var label = CreateHeaderLabel(labelText);
             if (!string.IsNullOrEmpty(labelText) && labelText.Contains('|'))
             {
                 void Update()
                 {
                     label.Dispatcher.Invoke(() =>
                     {
                         label.Text = labelText.Localize();
                     });
                 }
                 Update();
                 LanguageChanged += Update;
                 label.Unloaded += (_, _) => LanguageChanged -= Update;
             }
             else
             {
                 label.Text = labelText;
             }
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
             // Bilingual support: if content contains '|', split and update on language change
             if (!string.IsNullOrEmpty(content) && content.Contains('|'))
             {
                 void Update()
                 {
                     tb.Dispatcher.Invoke(() =>
                     {
                         tb.Text = content.Localize();
                     });
                 }
                 Update();
                 LanguageChanged += Update;
                 // remove handler when button unloads
             }
             else
             {
                 tb.Text = content;
             }
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

             // Attach auto-shrink behavior so long text reduces font to fit available width
             var adjustFont = AttachAutoShrinkBehavior(btn, tb);

             // Set tooltip
             SetLocalizedToolTip(btn, tooltip);

             // Handle language change unloading
             if (!string.IsNullOrEmpty(content) && content.Contains('|'))
             {
                 void UpdateText()
                 {
                     tb.Text = content.Localize();
                     adjustFont(); // Adjust font after text change
                 }
                 LanguageChanged += UpdateText;
                 btn.Unloaded += (_, _) => LanguageChanged -= UpdateText;
             }

             return btn;
         }

         private static Action AttachAutoShrinkBehavior(Button btn, TextBlock tb)
         {
             // btn and tb are non-nullable parameters
              double minFont = 8.0;
              double maxFont = tb.FontSize;
  
              void Adjust()
              {
                     if (btn.ActualWidth <= 0) return;
                     // available width inside the button minus horizontal padding
                     var leftPad = Math.Min(btn.Padding.Left, 5.0);
                     var rightPad = Math.Min(btn.Padding.Right, 5.0);
                     var pad = leftPad + rightPad + 2.0; // 2px for border/measure slack
                     double available = Math.Max(0, btn.ActualWidth - pad);

                     tb.FontSize = maxFont;
                     tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                     double desired = tb.DesiredSize.Width;
                     while (desired > available && tb.FontSize > minFont)
                     {
                         tb.FontSize = Math.Max(minFont, tb.FontSize - 0.5);
                         tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                         desired = tb.DesiredSize.Width;
                     }
              }
  
             btn.SizeChanged += (_, _) => Adjust();
             tb.TargetUpdated += (_, _) => Adjust();
             btn.Loaded += (_, _) => Adjust();

             // Also respond to language changes (text may change) so we re-measure and shrink if needed
             return Adjust;
         }

          public static CheckBox CreateStandardCheckBox(string content, string? tooltip = null)
          {
              var cb = new CheckBox { FontSize = ComFontSize, Margin = new Thickness(2, 0, 10, 0), Background = Brushes.Transparent };
              if (!string.IsNullOrEmpty(content) && content.Contains('|'))
              {
                 var tb = new TextBlock { Text = content.Localize(), FontSize = ComFontSize, TextWrapping = TextWrapping.Wrap };
                  cb.Content = tb;
                 void UpdateText() { tb.Text = content.Localize(); }
                 LanguageChanged += UpdateText;
                 cb.Unloaded += (_, _) => LanguageChanged -= UpdateText;
              }
              else
              {
                  cb.Content = new TextBlock { Text = content, FontSize = ComFontSize, TextWrapping = TextWrapping.Wrap };
              }
              SetLocalizedToolTip(cb, tooltip);
              return cb;
          }


    public static Border CreateStatusBar(Window window, ToggleSwitch? realTimeToggle = null, ToggleButton? listenerBtn = null)
    {
        var footer = new Border
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
            // Margin = new Thickness(12, 0, 0, 0)
        };

        var copyrightButton = new HyperlinkButton
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            NavigateUri = Strings.KrrcreamUrl
        };

        void UpdateCopyrightText()
        {
            copyrightButton.Content = Strings.FooterCopyright.Localize();
        }

        UpdateCopyrightText(); // 初始化文本
        LanguageChanged += UpdateCopyrightText;
        copyrightButton.Unloaded += (_, _) => LanguageChanged -= UpdateCopyrightText;

        var githubLink = new HyperlinkButton()
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

            System.Windows.Controls.Grid.SetColumn(leftPanel, 0);
        footerGrid.Children.Add(leftPanel);

        var themeComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(ApplicationTheme)),
            SelectedItem = GetSavedApplicationTheme() != null && Enum.TryParse<ApplicationTheme>(GetSavedApplicationTheme(), out var savedTheme) ? savedTheme : ApplicationTheme.Light,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var backdropComboBox = new ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(WindowBackdropType)),
            SelectedItem = GetSavedWindowBackdropType() != null && Enum.TryParse<WindowBackdropType>(GetSavedWindowBackdropType(), out var savedBackdrop) ? savedBackdrop : WindowBackdropType.Mica,
            Margin = new Thickness(4, 0, 4, 0)
        };
        var accentSwitch = new ToggleSwitch
        {
            IsChecked = GetSavedUpdateAccent() ?? false,
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
            if (themeComboBox.SelectedItem is ApplicationTheme theme) SetSavedApplicationTheme(theme.ToString());
        };
        backdropComboBox.SelectionChanged += (_, _) => { 
            ApplyThemeSettings(accentSwitch.IsChecked == true);
            if (backdropComboBox.SelectedItem is WindowBackdropType backdrop) SetSavedWindowBackdropType(backdrop.ToString());
        };
        accentSwitch.Checked += (_, _) => { 
            ApplyThemeSettings(updateAccent: true); 
            SetSavedUpdateAccent(true);
        };
        accentSwitch.Unchecked += (_, _) => { 
            ApplyThemeSettings(updateAccent: false); 
            SetSavedUpdateAccent(false);
        };

        var langSwitch = new ToggleSwitch
        {
            IsChecked = LocalizationManager.IsChineseLanguage(),
            Margin = new Thickness(4, 0, 12, 0),
            MinWidth = 60
        };

        void UpdateLanguageSwitchText()
        {
            window.Dispatcher.Invoke(() =>
            {
                langSwitch.Content = Strings.Localize(Strings.SettingsMenuLanguage);
                langSwitch.InvalidateVisual();
            });
        }

        UpdateLanguageSwitchText();
        LanguageChanged += UpdateLanguageSwitchText;
        langSwitch.Unloaded += (_, _) => LanguageChanged -= UpdateLanguageSwitchText;

        langSwitch.Checked += (_, _) => ToggleLanguage();
        langSwitch.Unchecked += (_, _) => ToggleLanguage();

        // 创建设置按钮（齿轮图标）
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
            themeItem.Click += (_, e) => {
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
            ToggleLanguage();
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

        LanguageChanged += UpdateMenuItemTexts;

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

            System.Windows.Controls.Grid.SetColumn(settingsPanel, 2);
        footerGrid.Children.Add(settingsPanel);

        footer.Child = footerGrid;
        
        // 注册语言改变事件
        LanguageChanged += UpdateMenuItemTexts;
        
        // 清理事件（当footer卸载时）
        footer.Unloaded += (s, e) => LanguageChanged -= UpdateMenuItemTexts;
        
        // 应用初始主题设置
        ApplyThemeSettings(accentSwitch.IsChecked == true);
        
        return footer;
    }

          
}
}
