using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace NabyScape;
public sealed partial class SettingsWindow
{
    private readonly List<Action> homeStatusUpdates=new();
    private void InitializeHomeStatuses()
    {
        app.Updated+=HomeStatusChanged;app.Lyrics.Changed+=RefreshHomeStatuses;
        Closed+=(_,_)=>{app.Updated-=HomeStatusChanged;app.Lyrics.Changed-=RefreshHomeStatuses;homeStatusUpdates.Clear();};
    }
    private void HomeStatusChanged(){if((app.CurrentUpdate&(RuntimeChange.Media|RuntimeChange.Settings))!=0)RefreshHomeStatuses();}
    private void RefreshHomeStatuses()
    {
        if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(RefreshHomeStatuses));return;}
        if(current!="Home" || !IsVisible)return;foreach(var update in homeStatusUpdates.ToArray())update();
    }
    private void HomeFeatureStatuses()
    {
        homeStatusUpdates.Clear();var group=new StackPanel();
        void Row(string title,Func<(string State,string Detail,string Action)> assess,Action open)
        {
            var row=new DockPanel{Margin=new(0,0,0,16)};var action=UI.CaptionButton("Открыть",open);action.MinWidth=116;action.VerticalAlignment=VerticalAlignment.Center;action.Margin=new(16,0,0,0);DockPanel.SetDock(action,Dock.Right);row.Children.Add(action);
            var text=new StackPanel();var state=UI.Text("",14);state.FontWeight=FontWeights.SemiBold;text.Children.Add(state);var detail=UI.Text("",12,UI.Muted);detail.Margin=new(0,5,0,0);text.Children.Add(detail);row.Children.Add(text);group.Children.Add(row);
            void Update(){var value=assess();Motion.Text(state,title+" · "+value.State);Motion.Text(detail,value.Detail);action.Content=value.Action;UI.Named(action,value.Action);}
            homeStatusUpdates.Add(Update);Update();
        }
        Row("Музыка",()=>!app.Settings.MediaIntegrationEnabled?("Отключено","Интеграция с медиаплеерами выключена.","Включить музыку"):
            !app.Settings.Widgets.Any(w=>w.Enabled && w.Id=="media")?("Скрыто","Включите виджет музыки для отображения управления треками.","Выбрать виджеты"):
            !app.Media.Available?("Ожидание воспроизведения","Запустите трек в вашем плеере (Spotify, браузер и др.).","Настройки звука"):("Готово","Управление воспроизведением активно.","Настройки звука"),()=>
            {if(!app.Settings.MediaIntegrationEnabled){app.Settings.MediaIntegrationEnabled=true;app.ChangeSettings();}else Navigate(app.Settings.Widgets.Any(w=>w.Enabled && w.Id=="media")?"Audio":"Content");});
        Row("Текст песен",()=>!app.Settings.LyricsEnabled && app.Settings.ClosedMediaContent!="Lyrics"?("Отключено","Включите отображение текста песен в панели или на островке.","Настройки текста"):
            !app.Media.Available?("Ожидание воспроизведения","Включите музыку для поиска синхронизированного текста.","Настройки текста"):
            !LyricTiming.CanUse(app.Media)?("Недоступно","Плеер не передает информацию о треке или позицию.","Настройки текста"):
            app.Lyrics.Result.Lines.Length>0?("Готово","Синхронизированный текст найден для текущего трека.","Настройки текста"):
            app.Lyrics.Result.Status.StartsWith("Finding",StringComparison.Ordinal)?("Поиск текста","Идет поиск синхронизированного текста онлайн...","Настройки текста"):
            app.Lyrics.Result.Status.StartsWith("No synced",StringComparison.Ordinal) || app.Lyrics.Result.Status.StartsWith("Instrumental",StringComparison.Ordinal)?("Не найден","Синхронизированный текст для этого трека не найден.","Настройки текста"):
            ("Ожидание текста","Откройте панель текста для повторного поиска.","Настройки текста"),()=>Navigate("Audio"));
        page.Children.Add(Section("Ваши функции",group));
    }
}
