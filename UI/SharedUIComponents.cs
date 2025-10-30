using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Localization;
using Border = Wpf.Ui.Controls.Border;
using Button = Wpf.Ui.Controls.Button;
using StackPanel = Wpf.Ui.Controls.StackPanel;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace krrTools.UI
{
    /// <summary>
    /// 统一的UI控件库，为所有工具提供一致的界面组件和样式
    /// </summary>
    public static class SharedUIComponents
    {
        // 统一UI样式相关常量
        private const double com_font_size = UIConstants.COMMON_FONT_SIZE;
        private static SolidColorBrush panelBackgroundBrush = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255));
        private static readonly Brush ui_text_brush = UIConstants.UI_TEXT_BRUSH;
        private static readonly Thickness panel_padding = UIConstants.PANEL_PADDING;

        public static readonly Brush PANEL_BORDER_BRUSH = UIConstants.PANEL_BORDER_BRUSH;
        public static readonly CornerRadius PANEL_CORNER_RADIUS = UIConstants.PANEL_CORNER_RADIUS;
        public const double HEADER_FONT_SIZE = UIConstants.HEADER_FONT_SIZE;

        public static event Action? LanguageChanged
        {
            add => LocalizationService.LanguageChanged += value;
            remove => LocalizationService.LanguageChanged -= value;
        }

        public static Brush PanelBackgroundBrush
        {
            get => panelBackgroundBrush;
            set
            {
                if (value is SolidColorBrush scb) panelBackgroundBrush = new SolidColorBrush(scb.Color);
                else panelBackgroundBrush = new SolidColorBrush(panelBackgroundBrush.Color);
            }
        }

        public static Border CreateStandardPanel(UIElement inner, Thickness? margin = null, Thickness? padding = null)
        {
            var border = new Border
            {
                Background = PanelBackgroundBrush,
                BorderBrush = PANEL_BORDER_BRUSH,
                BorderThickness = new Thickness(1),
                CornerRadius = PANEL_CORNER_RADIUS,
                Padding = panel_padding,
                Child = inner
            };

            if (margin.HasValue) border.Margin = margin.Value;
            if (padding.HasValue) border.Padding = padding.Value;
            return border;
        }

        public static TextBlock CreateHeaderLabel(string text)
        {
            var tb = new TextBlock
            {
                FontSize = HEADER_FONT_SIZE,
                FontWeight = FontWeights.Bold,
                Foreground = ui_text_brush
            };

            if (!string.IsNullOrEmpty(text) && text.Contains('|'))
            {
                tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                              new Binding("Value") { Source = text.GetLocalizedString() });
            }
            else
                tb.Text = text;

            return tb;
        }

        public static FrameworkElement CreateLabeledRow(string labelText, UIElement control, Thickness rowMargin)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
            TextBlock label = CreateHeaderLabel(labelText);
            label.Margin = new Thickness(0, 0, 0, 4);
            panel.Children.Add(label);
            if (control is FrameworkElement fe) panel.Children.Add(fe);
            return panel;
        }

        public static TextBox CreateStandardTextBox()
        {
            return new TextBox
            {
                FontSize = com_font_size,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderBrush = PANEL_BORDER_BRUSH,
                Padding = new Thickness(6)
            };
        }

        public static Button CreateStandardButton(string content, string? tooltip = null)
        {
            var tb = new TextBlock { FontSize = com_font_size, TextTrimming = TextTrimming.CharacterEllipsis };

            if (!string.IsNullOrEmpty(content) && content.Contains('|'))
            {
                tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                              new Binding("Value") { Source = content.GetLocalizedString() });
            }
            else
                tb.Text = content;

            var btn = new Button
            {
                Content = tb,
                // cap padding so distance from text to button edge is at most 5
                Padding = new Thickness(5),
                // small default margin to match app style
                Margin = new Thickness(5, 0, 0, 0),
                FontSize = com_font_size,
                Background = PanelBackgroundBrush,
                BorderBrush = PANEL_BORDER_BRUSH,
                BorderThickness = new Thickness(1),
                Foreground = ui_text_brush
            };

            LocalizationService.SetLocalizedToolTip(btn, tooltip);

            return btn;
        }

        public static CheckBox CreateStandardCheckBox(string content, string? tooltip = null)
        {
            var cb = new CheckBox
                { FontSize = com_font_size, Margin = new Thickness(2, 0, 10, 0), Background = Brushes.Transparent };
            var tb = new TextBlock { FontSize = com_font_size, TextWrapping = TextWrapping.Wrap };

            if (!string.IsNullOrEmpty(content) && content.Contains('|'))
            {
                tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                              new Binding("Value") { Source = content.GetLocalizedString() });
            }
            else
                tb.Text = content;

            cb.Content = tb;

            LocalizationService.SetLocalizedToolTip(cb, tooltip);

            return cb;
        }
    }
}
