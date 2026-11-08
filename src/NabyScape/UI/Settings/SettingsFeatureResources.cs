using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace NabyScape;
public sealed partial class SettingsWindow
{
    private TextBlock? featureUsageSummary;
    private void RefreshFeatureUsage(){if(featureUsageSummary!=null)featureUsageSummary.Text=$"Включено виджетов: {app.Settings.Widgets.Count(w=>w.Enabled)} · выбирайте только нужное";}
    private void FeatureResourceSettings()
    {
        var s=app.Settings;var body=new StackPanel();
        var summary=UI.Text($"Включено виджетов: {s.Widgets.Count(w=>w.Enabled)} · выбирайте только нужное",14);featureUsageSummary=summary;body.Children.Add(summary);
        var actions=new WrapPanel{Margin=new(0,10,0,8)};
        actions.Children.Add(UI.CaptionButton("Основные виджеты",()=>
        {
            string[] keep={"media","clock","timer","focus"};
            foreach(var widget in s.Widgets)widget.Enabled=keep.Contains(widget.Id);
            foreach(var view in s.HoverViews)view.Enabled=keep.Contains(view.Id);
            s.HoverFollowsWidgets=true;Save();Navigate("Content");SetSaveFeedback("Выбраны основные: музыка, часы, таймер и фокус. Сохранённые данные сохранены.");
        }));
        actions.Children.Add(UI.CaptionButton("Настроить",()=>
        {
            var target=page.Children.OfType<Border>().FirstOrDefault(b=>b.Tag as string=="Enabled widgets & order");target?.BringIntoView();
        }));body.Children.Add(actions);
        body.Children.Add(UI.Text("Набор «Основные» оставляет музыку, часы, таймер и фокус. Остальные виджеты отключены до включения вручную.",12,UI.Muted));
        body.Children.Add(Check("Подключение к музыкальным приложениям",s.MediaIntegrationEnabled,v=>s.MediaIntegrationEnabled=v));
        body.Children.Add(UI.Text("При отключении сессии Windows Media отключаются, обложки треков выгружаются. Воспроизведение в плеере не прерывается.",12,UI.Muted));
        body.Children.Add(Check("Отслеживать завершённые загрузки",s.DownloadsEnabled,v=>s.DownloadsEnabled=v));
        var links=new WrapPanel{Margin=new(0,8,0,0)};links.Children.Add(UI.CaptionButton("Буфер обмена",()=>Navigate("Clipboard")));body.Children.Add(links);
        body.Children.Add(UI.Text("Отключение виджета сохраняет ваши заметки, файлы и таймеры. Неиспользуемые страницы приостанавливают работу в фоне.",12,UI.Muted));
        page.Children.Add(Section("Использование ресурсов",body));
    }
}
