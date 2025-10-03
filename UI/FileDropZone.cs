using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Beatmaps;
using krrTools.Localization;
using OsuParsers.Decoders;
using Border = Wpf.Ui.Controls.Border;
using Button = System.Windows.Controls.Button;
using Grid = Wpf.Ui.Controls.Grid;
using TextBlock = System.Windows.Controls.TextBlock;

namespace krrTools.UI
{
    public sealed class FileDropZone : Border
    {
        public event EventHandler<string[]>? FilesDropped;

        private readonly TextBlock DropHint;
        private readonly Button StartConversionButton;
        private string[]? _stagedPaths;

        // 本地化字符串对象
        private readonly DynamicLocalizedString _dropHintLocalized = new(Strings.DropHint);
        private readonly DynamicLocalizedString _dropFilesHintLocalized = new(Strings.DropFilesHint);
        private readonly DynamicLocalizedString _startButtonTextLocalized = new(Strings.StartButtonText);

        public event EventHandler<string[]?>? StartConversionRequested;

        public FileDropZone()
        {
            AllowDrop = true;
            Background = new SolidColorBrush(Color.FromArgb(160, 245, 248, 255));
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 175, 200, 255));
            BorderThickness = new Thickness(2);
            CornerRadius = new CornerRadius(6);
            Margin = new Thickness(8, 2, 8, 5);
            Padding = new Thickness(12);
            Height = 60;

            DropHint = new TextBlock
            {
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x67, 0xB5)),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            DropHint.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = _dropHintLocalized });

            StartConversionButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 6, 8, 6),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(8, 0, 6, 0),
                MinWidth = 92,
            };
            StartConversionButton.SetBinding(ContentControl.ContentProperty, new Binding("Value") { Source = _startButtonTextLocalized });
            InitializeUI();
            UpdateTexts();
            Unloaded += FileDropZone_Unloaded;
        }

        private void InitializeUI()
        {
            Child = new Grid()
            {
                Children = { DropHint, StartConversionButton }
            };

            Drop += OnDrop;
            StartConversionButton.Click += StartConversionButton_Click;
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            if (_stagedPaths == null || _stagedPaths.Length == 0)
            {
                // 清除绑定并设置默认文本
                DropHint.ClearValue(TextBlock.TextProperty);
                DropHint.Text = _dropHintLocalized.Value;
            }
            else
            {
                DropHint.Text = string.Format(_dropFilesHintLocalized.Value, _stagedPaths.Length);
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;

            var osuFiles = CollectOsuFiles(files);
            if (osuFiles.Count == 0) return;
            
            _stagedPaths = osuFiles.ToArray();
            FilesDropped?.Invoke(this, _stagedPaths);

            // 如果有谱面文件，预览第一个
            if (_stagedPaths.Length >= 1)
            {
                LoadPreviewForDroppedFile(_stagedPaths[0]);
            }

            UpdateTexts();
            StartConversionButton.Visibility = _stagedPaths is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadPreviewForDroppedFile(string filePath)
        {
            var mainWindow = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow is { PreviewDualControl: not null })
            {
                var beatmap = BeatmapDecoder.Decode(filePath).GetManiaBeatmap();
                mainWindow.PreviewDualControl.LoadPreview(beatmap);
            }
        }
        
        private List<string> CollectOsuFiles(string[] items)
        {
            var osuFiles = new List<string>();
            foreach (var item in items)
            {
                if (System.IO.File.Exists(item) && System.IO.Path.GetExtension(item).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                {
                    osuFiles.Add(item);
                }
                else if (System.IO.Directory.Exists(item))
                {
                    try
                    {
                        var found = System.IO.Directory.GetFiles(item, "*.osu", System.IO.SearchOption.AllDirectories);
                        osuFiles.AddRange(found);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error accessing directory {item}: {ex.Message}");
                    }
                }
            }
            return osuFiles;
        }

        private void StartConversionButton_Click(object sender, RoutedEventArgs e)
        {
            StartConversionRequested?.Invoke(this, _stagedPaths);
        }

        private void FileDropZone_Unloaded(object sender, RoutedEventArgs e)
        {
            SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }
    }
}