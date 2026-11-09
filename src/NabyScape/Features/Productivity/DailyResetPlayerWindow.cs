using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace NabyScape;

internal sealed class DailyResetPlayerWindow : Window
{
    private readonly App app;
    private readonly Action? onCompleted;
    private readonly MediaElement media;
    private readonly ProgressBar progress;
    private readonly Button completeButton;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private string? currentVideo;
    private bool completed;

    internal DailyResetPlayerWindow(App app, Action? onCompleted = null)
    {
        this.app = app;
        this.onCompleted = onCompleted;

        Title = "Перезагрузка дня";

        double screenWidth = SystemParameters.WorkArea.Width;
        double screenHeight = SystemParameters.WorkArea.Height;
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
            screenWidth = screen.WorkingArea.Width;
            screenHeight = screen.WorkingArea.Height;
        }

        if (screenWidth >= 1920 && screenHeight >= 1080)
        {
            Width = 1920;
            Height = 1080;
        }
        else
        {
            Width = Math.Min(1600, screenWidth > 0 ? screenWidth : 1600);
            Height = Math.Min(900, screenHeight > 0 ? screenHeight : 900);
        }

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
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Video
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bottom bar
        rootBorder.Child = mainGrid;
        Content = rootBorder;

        // Row 0: Header
        var header = new Grid { Margin = new Thickness(16, 0, 16, 0) };
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        var backButton = UI.Button("← Назад", Close);
        backButton.Padding = new Thickness(12, 6, 12, 6);
        backButton.HorizontalAlignment = HorizontalAlignment.Left;
        backButton.VerticalAlignment = VerticalAlignment.Center;
        backButton.Background = UI.Brush("#252321");
        backButton.Foreground = UI.Brush("#F2E9DF");
        backButton.BorderBrush = UI.Brush("#252321");
        backButton.BorderThickness = new Thickness(1);
        backButton.FontSize = 13;
        backButton.FontWeight = FontWeights.Medium;
        header.Children.Add(backButton);

        var title = UI.Heading("Перезагрузка дня", 18);
        title.FontWeight = FontWeights.SemiBold;
        title.Foreground = UI.Brush("#F2E9DF");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);

        var settingsButton = Icons.Button("settings", "Настройки видео / Плейлист", () =>
        {
            var playlistWin = new DailyResetPlaylistWindow { Owner = this };
            playlistWin.Show();
        }, false, 32);
        settingsButton.Content = Icons.Create("settings", 16, UI.Brush("#F2E9DF"));
        settingsButton.HorizontalAlignment = HorizontalAlignment.Right;
        settingsButton.VerticalAlignment = VerticalAlignment.Center;
        settingsButton.Background = UI.Brush("#252321");
        settingsButton.Foreground = UI.Brush("#F2E9DF");
        settingsButton.BorderBrush = UI.Brush("#252321");
        settingsButton.BorderThickness = new Thickness(1);
        settingsButton.Margin = new Thickness(0, 0, 40, 0);
        header.Children.Add(settingsButton);

        var closeButton = Icons.Button("x", "Закрыть", Close, false, 32);
        closeButton.Content = Icons.Create("x", 16, UI.Brush("#F2E9DF"));
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        closeButton.VerticalAlignment = VerticalAlignment.Center;
        closeButton.Background = UI.Brush("#252321");
        closeButton.Foreground = UI.Brush("#F2E9DF");
        closeButton.BorderBrush = UI.Brush("#252321");
        closeButton.BorderThickness = new Thickness(1);
        header.Children.Add(closeButton);

        Grid.SetRow(header, 0);
        mainGrid.Children.Add(header);

        // Row 1: Video Container
        media = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Stop,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var videoContainer = new Border
        {
            Margin = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(16),
            Background = UI.Brush("#101113"),
            BorderBrush = UI.Brush("#252321"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = media
        };
        videoContainer.SizeChanged += (_, _) =>
        {
            if (videoContainer.ActualWidth > 0 && videoContainer.ActualHeight > 0)
                videoContainer.Clip = new RectangleGeometry(new Rect(0, 0, videoContainer.ActualWidth, videoContainer.ActualHeight), 16, 16);
        };
        Grid.SetRow(videoContainer, 1);
        mainGrid.Children.Add(videoContainer);

        // Row 2: Bottom bar (Progress + Complete Button)
        var bottomBar = new StackPanel
        {
            Margin = new Thickness(24, 10, 24, 16),
            VerticalAlignment = VerticalAlignment.Center
        };

        progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 6,
            IsHitTestVisible = false,
            Background = UI.Brush("#252321"),
            Foreground = UI.Brush("#E58A43"),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 12)
        };
        bottomBar.Children.Add(progress);

        completeButton = UI.Button("✓ Засчитать выполнение", () => _ = CompleteAsync());
        completeButton.Height = 44;
        completeButton.FontSize = 14;
        completeButton.FontWeight = FontWeights.Normal;
        completeButton.IsEnabled = false; // Disabled during playback!
        completeButton.Background = UI.Brush("#252321");
        completeButton.Foreground = UI.Brush("#96918B");
        completeButton.BorderThickness = new Thickness(0);
        completeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        bottomBar.Children.Add(completeButton);

        Grid.SetRow(bottomBar, 2);
        mainGrid.Children.Add(bottomBar);

        // Events
        media.MediaEnded += (_, _) =>
        {
            progress.Value = 1;
            EnableCompletion();
        };

        media.MediaFailed += (_, e) =>
        {
            app.Store.Log(e.ErrorException ?? new Exception("MediaElement failed: " + e.ToString()));
            EnableCompletion();
        };

        timer.Tick += (_, _) =>
        {
            if (media.NaturalDuration.HasTimeSpan && media.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                double ratio = media.Position.TotalSeconds / media.NaturalDuration.TimeSpan.TotalSeconds;
                progress.Value = Math.Clamp(ratio, 0, 1);
                if (ratio >= 0.995 || media.Position >= media.NaturalDuration.TimeSpan)
                {
                    progress.Value = 1;
                    EnableCompletion();
                }
            }
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        Loaded += (_, _) =>
        {
            currentVideo = ResolveVideoFile();
            if (!string.IsNullOrEmpty(currentVideo) && File.Exists(currentVideo))
            {
                media.Source = new Uri(currentVideo, UriKind.RelativeOrAbsolute);
                media.Play();
                timer.Start();
            }
            else
            {
                EnableCompletion();
            }
        };

        Closed += (_, _) =>
        {
            timer.Stop();
            media.Stop();
            media.Close();
        };
    }

    private void EnableCompletion()
    {
        if (completed || completeButton.IsEnabled) return;
        completeButton.IsEnabled = true;
        completeButton.Background = UI.Brush("#E58A43");
        completeButton.Foreground = UI.Brush("#101113");
        completeButton.FontWeight = FontWeights.Bold;
    }

    private static string? ResolveVideoFile()
    {
        return DailyResetPlaylistService.GetRandomVideo();
    }

    private async Task CompleteAsync()
    {
        if (completed) return;
        completed = true;
        completeButton.IsEnabled = false;
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbDir = Path.Combine(appData, "NabyScape");
            if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);
            string dbPath = Path.Combine(dbDir, "daily_reset.db");
            string connectionString = $"Data Source={dbPath}";

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                var createCmd = connection.CreateCommand();
                createCmd.CommandText = "CREATE TABLE IF NOT EXISTS completions (id INTEGER PRIMARY KEY AUTOINCREMENT, completed_at TEXT, video_name TEXT);";
                await createCmd.ExecuteNonQueryAsync();

                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = "INSERT INTO completions (completed_at, video_name) VALUES ($completed_at, $video_name);";
                insertCmd.Parameters.AddWithValue("$completed_at", DateTimeOffset.UtcNow.ToString("O"));
                insertCmd.Parameters.AddWithValue("$video_name", string.IsNullOrEmpty(currentVideo) ? "unknown" : Path.GetFileName(currentVideo));
                await insertCmd.ExecuteNonQueryAsync();
            }

            app.PostNotice(new InternalNotice("reset", "✓ Засчитано!", "Выполнение перезагрузки дня сохранено."));
            onCompleted?.Invoke();
            Close();
        }
        catch (Exception ex)
        {
            app.Store.Log(ex);
        }
    }
}
