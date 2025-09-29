using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using krrTools.Localization;

namespace krrTools.tools.Shared
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

        // Set the ForceChinese flag and notify listeners
        private static void SetForceChinese(bool? forceChinese)
        {
            LocalizationManager.SetForceChinese(forceChinese);
        }

        // Toggle the language selection (used by simple toggle UIs)
        public static void ToggleLanguage()
        {
            LocalizationManager.ToggleLanguage();
        }

        // Set saved theme settings
        public static void SetSavedApplicationTheme(string theme)
        {
            OptionsManager.SetApplicationTheme(theme);
        }

        public static void SetSavedWindowBackdropType(string backdropType)
        {
            OptionsManager.SetWindowBackdropType(backdropType);
        }

        public static void SetSavedUpdateAccent(bool updateAccent)
        {
            OptionsManager.SetUpdateAccent(updateAccent);
        }

        // Get saved theme settings
        public static string? GetSavedApplicationTheme() => OptionsManager.GetApplicationTheme();
        public static string? GetSavedWindowBackdropType() => OptionsManager.GetWindowBackdropType();
        public static bool? GetSavedUpdateAccent() => OptionsManager.GetUpdateAccent();
        public static bool? GetForceChinese() => OptionsManager.GetForceChinese();

        // 统一UI样式相关常量，参考LN工具的字体风格
        // Text color used throughout the Fluent-like UI — exposed public so other parts of the app can reuse it
        public static readonly Brush UiTextBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21));
        public const double HeaderFontSize = 18.0;
        public const double ComFontSize = 16.0;
        // Fluent / acrylic visual tokens
        // Use a lighter translucent tint so the underlying acrylic blur is visible (less opaque)
        private static SolidColorBrush _panelBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)); // ~40% white tint
        public static Brush PanelBackgroundBrush
        {
            get => _panelBackgroundBrush;
            set
            {
                if (value is SolidColorBrush scb) _panelBackgroundBrush = new SolidColorBrush(scb.Color);
                else _panelBackgroundBrush = new SolidColorBrush(_panelBackgroundBrush.Color);
            }
        }

        public static void SetPanelBackgroundAlpha(byte alpha)
        {
            // Ensure brush is not frozen before modifying
            if (_panelBackgroundBrush.IsFrozen)
                _panelBackgroundBrush = _panelBackgroundBrush.Clone();
            var c = _panelBackgroundBrush.Color;
            _panelBackgroundBrush.Color = Color.FromArgb(alpha, c.R, c.G, c.B);
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
         public static readonly Brush PanelBorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00)); // subtle dark border ~20%
         public static readonly CornerRadius PanelCornerRadius = new CornerRadius(8);
         private static readonly Thickness PanelPadding = new Thickness(8);

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
         
         public static bool IsChineseLanguage()
         {
             return LocalizationManager.IsChineseLanguage();
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
         public static void SetLocalizedToolTip(FrameworkElement element, string? tooltipText)
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
                     var parts = text.Split('|', 2);
                     tb.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
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
                     var parts = labelText.Split('|', 2);
                     label.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
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
                     var parts = content.Split(['|'], 2);
                     tb.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
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
                     var parts = content.Split('|', 2);
                     tb.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
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
                 var parts = content.Split('|', 2);
                 var tb = new TextBlock { Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0], FontSize = ComFontSize, TextWrapping = TextWrapping.Wrap };
                  cb.Content = tb;
                 void UpdateText() { var p = content.Split('|', 2); tb.Text = IsChineseLanguage() && p.Length > 1 ? p[1] : p[0]; }
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

          
}
}
