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
        Width = 1024;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;

        var rootBorder = new Border
        {
            Background = UI.Brush("#121418"),
            BorderBrush = UI.Brush("#2A2E38"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true
        };

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) }); // Header
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(530) }); // Video
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Bottom bar
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
        backButton.Background = UI.Brush("#1C2028");
        backButton.BorderBrush = UI.Brush("#2A2E38");
        backButton.BorderThickness = new Thickness(1);
        backButton.FontSize = 13;
        backButton.FontWeight = FontWeights.Medium;
        header.Children.Add(backButton);

        var title = UI.Heading("Перезагрузка дня", 18);
        title.FontWeight = FontWeights.SemiBold;
        title.Foreground = UI.Brush("#F0EDE8");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);

        var closeButton = Icons.Button("x", "Закрыть", Close, false, 32);
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        closeButton.VerticalAlignment = VerticalAlignment.Center;
        closeButton.Background = Brushes.Transparent;
        header.Children.Add(closeButton);

        Grid.SetRow(header, 0);
        mainGrid.Children.Add(header);

        // Row 1: Video Container
        media = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Stop,
            Stretch = Stretch.UniformToFill,
            Height = 520,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var videoContainer = new Border
        {
            Height = 520,
            Margin = new Thickness(24, 0, 24, 0),
            CornerRadius = new CornerRadius(16),
            Background = UI.Brush("#0A0B0E"),
            BorderBrush = UI.Brush("#1E222B"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
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
            Background = UI.Brush("#22252E"),
            Foreground = UI.Brush("#E28743"),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 12)
        };
        bottomBar.Children.Add(progress);

        completeButton = UI.Button("✓ Засчитать выполнение", () => _ = CompleteAsync());
        completeButton.Height = 44;
        completeButton.FontSize = 14;
        completeButton.FontWeight = FontWeights.Bold;
        completeButton.IsEnabled = false; // Disabled during playback!
        completeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        bottomBar.Children.Add(completeButton);

        Grid.SetRow(bottomBar, 2);
        mainGrid.Children.Add(bottomBar);

        // Events
        media.MediaEnded += (_, _) =>
        {
            progress.Value = 1;
            completeButton.IsEnabled = true;
        };

        media.MediaFailed += (_, e) =>
        {
            app.Store.Log(e.ErrorException ?? new Exception("MediaElement failed: " + e.ToString()));
            completeButton.IsEnabled = true;
        };

        timer.Tick += (_, _) =>
        {
            if (media.NaturalDuration.HasTimeSpan && media.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                double ratio = media.Position.TotalSeconds / media.NaturalDuration.TimeSpan.TotalSeconds;
                progress.Value = Math.Clamp(ratio, 0, 1);
                if (ratio >= 0.995 || media.Position >= media.NaturalDuration.TimeSpan)
                {
                    completeButton.IsEnabled = true;
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
        };

        Closed += (_, _) =>
        {
            timer.Stop();
            media.Stop();
            media.Close();
        };
    }

    private static string? ResolveVideoFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Videos"),
            Path.Combine(Directory.GetCurrentDirectory(), "Videos"),
            "Videos",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Videos"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "showcase"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "showcase"),
            "Assets/showcase"
        };

        foreach (var dir in candidates)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.mp4");
                    if (files.Length > 0)
                        return files[Random.Shared.Next(files.Length)];
                }
            }
            catch {}
        }

        string targetDir = Path.Combine(Directory.GetCurrentDirectory(), "Videos");
        try
        {
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            string demoSrc = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "showcase", "nabyscape-demo.mp4");
            if (!File.Exists(demoSrc))
                demoSrc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "showcase", "nabyscape-demo.mp4");
            if (File.Exists(demoSrc))
            {
                string dest = Path.Combine(targetDir, "reset.mp4");
                if (!File.Exists(dest)) File.Copy(demoSrc, dest, true);
                return dest;
            }
        }
        catch {}

        return null;
    }

    private async Task CompleteAsync()
    {
        if (completed) return;
        completed = true;
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
