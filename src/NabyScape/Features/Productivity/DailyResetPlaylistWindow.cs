using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NabyScape;

internal sealed class DailyResetPlaylistWindow : Window
{
    private readonly StackPanel listContainer;
    private readonly TextBlock countLabel;

    internal DailyResetPlaylistWindow()
    {
        Title = "Плейлист Перезагрузки дня";
        Width = 780;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;

        var rootBorder = new Border
        {
            Background = UI.Brush("#101113"),
            BorderBrush = UI.Brush("#252321"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true
        };

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) }); // Header
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Top Controls
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // List
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Footer
        rootBorder.Child = mainGrid;
        Content = rootBorder;

        // Row 0: Header
        var header = new Grid { Margin = new Thickness(24, 0, 20, 0) };
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        var headerLeft = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var title = UI.Heading("Плейлист Перезагрузки дня", 17);
        title.FontWeight = FontWeights.SemiBold;
        title.Foreground = UI.Brush("#F2E9DF");
        headerLeft.Children.Add(title);

        header.Children.Add(headerLeft);

        var closeButton = Icons.Button("x", "Закрыть", Close, false, 32);
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        closeButton.VerticalAlignment = VerticalAlignment.Center;
        closeButton.Background = Brushes.Transparent;
        header.Children.Add(closeButton);

        Grid.SetRow(header, 0);
        mainGrid.Children.Add(header);

        // Row 1: Top Controls
        var controlsPanel = new Grid { Margin = new Thickness(24, 6, 24, 14) };
        controlsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controlsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var buttonsRow = new StackPanel { Orientation = Orientation.Horizontal };

        var folderBtn = UI.Button("📁 Выбрать папку с видео", SelectFolder);
        folderBtn.Height = 36;
        folderBtn.Padding = new Thickness(14, 0, 14, 0);
        folderBtn.Background = UI.Brush("#252321");
        folderBtn.BorderBrush = UI.Brush("#252321");
        folderBtn.BorderThickness = new Thickness(1);
        folderBtn.Foreground = UI.Brush("#F2E9DF");
        folderBtn.FontSize = 12.5;
        folderBtn.FontWeight = FontWeights.Medium;
        folderBtn.ToolTip = "Сканировать папку на наличие видеофайлов MP4 и MKV";
        buttonsRow.Children.Add(folderBtn);

        var filesBtn = UI.Button("+ Добавить видеофайлы (MP4/MKV)", AddFiles);
        filesBtn.Height = 36;
        filesBtn.Margin = new Thickness(10, 0, 0, 0);
        filesBtn.Padding = new Thickness(14, 0, 14, 0);
        filesBtn.Background = UI.Brush("#E58A43");
        filesBtn.BorderBrush = Brushes.Transparent;
        filesBtn.BorderThickness = new Thickness(0);
        filesBtn.Foreground = UI.Brush("#101113");
        filesBtn.FontSize = 12.5;
        filesBtn.FontWeight = FontWeights.SemiBold;
        filesBtn.ToolTip = "Выбрать видеофайлы MP4 или MKV";
        buttonsRow.Children.Add(filesBtn);

        Grid.SetColumn(buttonsRow, 0);
        controlsPanel.Children.Add(buttonsRow);

        countLabel = UI.Text("0 видео", 12, UI.Muted);
        countLabel.VerticalAlignment = VerticalAlignment.Center;
        var countBadge = new Border
        {
            Background = UI.Brush("#252321"),
            BorderBrush = UI.Brush("#252321"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4, 10, 4),
            Child = countLabel,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(countBadge, 1);
        controlsPanel.Children.Add(countBadge);

        Grid.SetRow(controlsPanel, 1);
        mainGrid.Children.Add(controlsPanel);

        // Separator line
        var separator = new Border
        {
            Height = 1,
            Background = UI.Brush("#252321"),
            Margin = new Thickness(24, 0, 24, 12)
        };

        // Row 2: List of videos
        listContainer = new StackPanel();
        var scrollViewer = new ScrollViewer
        {
            Content = listContainer,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(24, 0, 24, 12)
        };
        Grid.SetRow(scrollViewer, 2);
        mainGrid.Children.Add(scrollViewer);

        // Row 3: Footer
        var footer = new Border
        {
            Background = UI.Brush("#101113"),
            BorderBrush = UI.Brush("#252321"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 10, 24, 10)
        };
        var footerText = UI.Text("ℹ Случайное видео выбирается из этого плейлиста при запуске Перезагрузки дня", 11.5, UI.Muted);
        footer.Child = footerText;
        Grid.SetRow(footer, 3);
        mainGrid.Children.Add(footer);

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        Loaded += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        listContainer.Children.Clear();
        var playlist = DailyResetPlaylistService.GetPlaylist();

        int count = playlist.Count;
        countLabel.Text = count switch
        {
            1 => "1 видео в ротации",
            _ when count % 10 == 1 && count % 100 != 11 => $"{count} видео в ротации",
            _ when count % 10 >= 2 && count % 10 <= 4 && (count % 100 < 10 || count % 100 >= 20) => $"{count} видео в ротации",
            _ => $"{count} видео в ротации"
        };

        if (playlist.Count == 0)
        {
            var emptyPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 60, 0, 60)
            };

            var emptyIcon = Icons.Create("nav-media", 44, UI.Brush("#252321"));
            emptyIcon.HorizontalAlignment = HorizontalAlignment.Center;
            emptyPanel.Children.Add(emptyIcon);

            var emptyTitle = UI.Heading("Плейлист пуст", 15);
            emptyTitle.FontWeight = FontWeights.SemiBold;
            emptyTitle.Foreground = UI.Brush("#F2E9DF");
            emptyTitle.HorizontalAlignment = HorizontalAlignment.Center;
            emptyTitle.Margin = new Thickness(0, 12, 0, 0);
            emptyPanel.Children.Add(emptyTitle);

            var emptyHint = UI.Text("Выберите папку или добавьте файлы MP4/MKV для ротации Перезагрузки дня", 12.5, UI.Brush("#96918B"));
            emptyHint.HorizontalAlignment = HorizontalAlignment.Center;
            emptyHint.Margin = new Thickness(0, 6, 0, 0);
            emptyPanel.Children.Add(emptyHint);

            listContainer.Children.Add(emptyPanel);
            return;
        }

        foreach (var item in playlist)
        {
            var card = BuildVideoCard(item);
            listContainer.Children.Add(card);
        }
    }

    private FrameworkElement BuildVideoCard(DailyResetPlaylistItem item)
    {
        var card = new Border
        {
            Background = UI.Brush("#1B1B1E"),
            BorderBrush = UI.Brush("#252321"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(10)
        };

        // Hover highlight
        card.MouseEnter += (_, _) =>
        {
            card.Background = UI.Brush("#252321");
            card.BorderBrush = UI.Brush("#E58A43");
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = UI.Brush("#1B1B1E");
            card.BorderBrush = UI.Brush("#252321");
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Thumbnail
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Delete

        // Column 0: Thumbnail 16:9
        var thumbBorder = new Border
        {
            Width = 136,
            Height = 76.5,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true
        };
        var thumbGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        thumbGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#252321"), 0.0));
        thumbGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1B1B1E"), 1.0));
        thumbBorder.Background = thumbGradient;

        var thumbGrid = new Grid();

        // Fallback video icon
        var fallbackIcon = Icons.Create("play", 24, UI.Brush("#E58A43"));
        fallbackIcon.HorizontalAlignment = HorizontalAlignment.Center;
        fallbackIcon.VerticalAlignment = VerticalAlignment.Center;
        thumbGrid.Children.Add(fallbackIcon);

        var thumbImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        thumbGrid.Children.Add(thumbImage);

        // Duration badge in bottom-right corner (YouTube style)
        if (!string.IsNullOrWhiteSpace(item.Duration))
        {
            var durationBadge = new Border
            {
                Background = UI.Brush("#D0101113"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            var durationText = new TextBlock
            {
                Text = item.Duration,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = UI.Brush("#F2E9DF")
            };
            durationBadge.Child = durationText;
            thumbGrid.Children.Add(durationBadge);
        }

        thumbBorder.Child = thumbGrid;
        Grid.SetColumn(thumbBorder, 0);
        grid.Children.Add(thumbBorder);

        // Asynchronously load thumbnail preview
        _ = Task.Run(async () =>
        {
            try
            {
                var thumb = await DailyResetPlaylistService.GetVideoThumbnailAsync(item.Path);
                if (thumb != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        thumbImage.Source = thumb;
                        fallbackIcon.Visibility = Visibility.Collapsed;
                    });
                }
            }
            catch {}
        });

        // Column 1: Info (Title, Duration, Path)
        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 12, 0)
        };

        var titleBlock = UI.Heading(item.Title, 14);
        titleBlock.FontWeight = FontWeights.SemiBold;
        titleBlock.Foreground = UI.Brush("#F2E9DF");
        titleBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        titleBlock.ToolTip = item.Title;
        infoStack.Children.Add(titleBlock);

        var durationRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var durationLabel = UI.Text("Длительность: " + (string.IsNullOrWhiteSpace(item.Duration) ? "--:--" : item.Duration), 12, UI.Muted);
        durationRow.Children.Add(durationLabel);

        string ext = System.IO.Path.GetExtension(item.Path).TrimStart('.').ToUpperInvariant();
        if (!string.IsNullOrEmpty(ext))
        {
            var extBadge = new Border
            {
                Background = UI.Brush("#252321"),
                BorderBrush = UI.Brush("#252321"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(8, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = ext,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = UI.Brush("#96918B")
                }
            };
            durationRow.Children.Add(extBadge);
        }
        infoStack.Children.Add(durationRow);

        var pathBlock = UI.Text(item.Path, 11, UI.Brush("#96918B"));
        pathBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        pathBlock.ToolTip = item.Path;
        pathBlock.Margin = new Thickness(0, 3, 0, 0);
        infoStack.Children.Add(pathBlock);

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // Column 2: Delete Button
        var deleteBtn = Icons.Button("trash", "Удалить из плейлиста", () =>
        {
            DailyResetPlaylistService.Remove(item);
            RefreshList();
        }, false, 36);
        deleteBtn.VerticalAlignment = VerticalAlignment.Center;
        deleteBtn.Background = UI.Brush("#252321");
        deleteBtn.BorderBrush = UI.Brush("#252321");
        deleteBtn.BorderThickness = new Thickness(1);
        deleteBtn.ToolTip = "Удалить из плейлиста";

        // Red hover on delete button
        deleteBtn.MouseEnter += (_, _) =>
        {
            deleteBtn.Background = UI.Brush("#3A1E22");
            deleteBtn.BorderBrush = UI.Brush("#B96A38");
        };
        deleteBtn.MouseLeave += (_, _) =>
        {
            deleteBtn.Background = UI.Brush("#252321");
            deleteBtn.BorderBrush = UI.Brush("#252321");
        };

        Grid.SetColumn(deleteBtn, 2);
        grid.Children.Add(deleteBtn);

        card.Child = grid;
        return card;
    }

    private void SelectFolder()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Выбрать папку с видеофайлами (MP4/MKV)",
                Multiselect = false
            };
            if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                DailyResetPlaylistService.AddFolder(dialog.FolderName);
                RefreshList();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось открыть папку: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddFiles()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Добавить видеофайлы (MP4/MKV)",
                Filter = "Видео (*.mp4;*.mkv)|*.mp4;*.mkv|Все файлы (*.*)|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog(this) == true && dialog.FileNames.Length > 0)
            {
                DailyResetPlaylistService.AddFiles(dialog.FileNames);
                RefreshList();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось добавить файлы: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
