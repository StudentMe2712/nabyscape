using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.Sqlite;

namespace NabyScape;

internal sealed class DailyResetView : Border
{
    private readonly App app;
    private readonly TextBlock statusText;

    internal DailyResetView(App app)
    {
        this.app = app;
        CornerRadius = new CornerRadius(14);
        BorderThickness = new Thickness(1);
        BorderBrush = UI.Brush("#252321");
        Background = UI.Brush("#1B1B1E");
        Padding = new Thickness(16);
        Margin = new Thickness(0, 0, 0, 8);

        MouseEnter += (_, _) =>
        {
            Background = UI.Brush("#252321");
            BorderBrush = UI.Brush("#E58A43");
        };
        MouseLeave += (_, _) =>
        {
            Background = UI.Brush("#1B1B1E");
            BorderBrush = UI.Brush("#252321");
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var title = UI.Heading("Перезагрузка дня", 16);
        title.FontWeight = FontWeights.Bold;
        title.Foreground = UI.Brush("#F2E9DF");
        headerRow.Children.Add(title);

        var settingsBtn = Icons.Button("settings", "Настройки видео / Плейлист", OpenPlaylist, false, 28);
        settingsBtn.Margin = new Thickness(8, 0, 0, 0);
        settingsBtn.Background = UI.Brush("#252321");
        settingsBtn.BorderBrush = UI.Brush("#252321");
        settingsBtn.BorderThickness = new Thickness(1);
        settingsBtn.VerticalAlignment = VerticalAlignment.Center;
        settingsBtn.ToolTip = "Настройки видео / Плейлист";
        settingsBtn.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            OpenPlaylist();
        };
        headerRow.Children.Add(settingsBtn);

        info.Children.Add(headerRow);

        statusText = UI.Text("Готово к просмотру", 13);
        statusText.Margin = new Thickness(0, 6, 0, 0);
        statusText.FontWeight = FontWeights.Medium;
        info.Children.Add(statusText);

        var hint = UI.Text("Короткая медитативная пауза для восстановления сил", 11, UI.Muted);
        hint.Margin = new Thickness(0, 4, 0, 0);
        info.Children.Add(hint);

        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var playIcon = Icons.Create("play", 20, UI.Brush("#101113"));
        playIcon.Margin = new Thickness(2, 0, 0, 0);
        var playBtnBorder = new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(23),
            Background = UI.Brush("#E58A43"),
            Child = playIcon,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(playBtnBorder, 1);
        grid.Children.Add(playBtnBorder);

        Child = grid;
        Cursor = Cursors.Hand;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var cur = dep;
                while (cur != null && !ReferenceEquals(cur, this))
                {
                    if (cur is Button) return;
                    cur = VisualTreeHelper.GetParent(cur);
                }
            }
            OpenPlayer();
            e.Handled = true;
        };

        Loaded += (_, _) => RefreshStatus();
    }

    private void OpenPlaylist()
    {
        var playlistWindow = new DailyResetPlaylistWindow();
        playlistWindow.Show();
    }

    internal void RefreshStatus()
    {
        bool done = IsCompletedToday();
        if (done)
        {
            statusText.Text = "✓ Выполнено сегодня";
            statusText.Foreground = UI.Brush("#E58A43");
        }
        else
        {
            statusText.Text = "Готово к просмотру";
            statusText.Foreground = UI.Brush("#96918B");
        }
    }

    private static bool IsCompletedToday()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbPath = Path.Combine(appData, "NabyScape", "daily_reset.db");
            if (!File.Exists(dbPath)) return false;
            string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='completions';";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            if (count == 0) return false;

            cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM completions WHERE completed_at LIKE $today;";
            cmd.Parameters.AddWithValue("$today", todayUtc + "%");
            var completedCount = Convert.ToInt64(cmd.ExecuteScalar());
            return completedCount > 0;
        }
        catch
        {
            return false;
        }
    }

    private void OpenPlayer()
    {
        var player = new DailyResetPlayerWindow(app, RefreshStatus);
        player.Show();
    }
}
