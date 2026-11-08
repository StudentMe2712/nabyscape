using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace NabyScape;

internal sealed class DailyResetView : StackPanel
{
    private readonly App app;
    private readonly MediaElement media;
    private readonly ProgressBar progress;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private string? currentVideo;

    internal DailyResetView(App app)
    {
        this.app = app;
        Margin = new Thickness(0);

        var header = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 8) };
        var title = UI.Heading("Перезагрузка дня", 15);
        title.FontWeight = FontWeights.SemiBold;
        header.Children.Add(title);
        Children.Add(header);

        media = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Stop,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var videoContainer = new Border
        {
            Height = 175,
            CornerRadius = new CornerRadius(12),
            Background = UI.Brush("#16FFFFFF"),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 8),
            Child = media
        };
        videoContainer.SizeChanged += (_, _) =>
        {
            if (videoContainer.ActualWidth > 0 && videoContainer.ActualHeight > 0)
                videoContainer.Clip = new RectangleGeometry(new Rect(0, 0, videoContainer.ActualWidth, videoContainer.ActualHeight), 12, 12);
        };
        Children.Add(videoContainer);

        progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Height = 4,
            IsHitTestVisible = false,
            Background = UI.Brush("#34363D"),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 10)
        };
        Children.Add(progress);

        var completeButton = UI.Button("✓ Засчитать выполнение", () => _ = CompleteAsync());
        completeButton.Height = 34;
        completeButton.FontSize = 12;
        completeButton.FontWeight = FontWeights.SemiBold;
        completeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        Children.Add(completeButton);

        media.MediaEnded += (_, _) =>
        {
            media.Position = TimeSpan.Zero;
            media.Play();
        };

        media.MediaFailed += (_, e) =>
        {
            app.Store.Log(e.ErrorException ?? new Exception("MediaElement failed: " + e.ToString()));
        };

        timer.Tick += (_, _) =>
        {
            if (media.NaturalDuration.HasTimeSpan && media.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                progress.Value = Math.Clamp(media.Position.TotalSeconds / media.NaturalDuration.TimeSpan.TotalSeconds, 0, 1);
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

        Unloaded += (_, _) =>
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

            app.PostNotice(new InternalNotice("reset", "✓ Засчитано!", "Выполнение перезагрузки дня успешно сохранено."));
        }
        catch (Exception ex)
        {
            app.Store.Log(ex);
        }
    }
}
