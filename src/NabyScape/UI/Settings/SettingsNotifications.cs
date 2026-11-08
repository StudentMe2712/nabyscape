using System;
using System.Windows.Controls;
namespace NabyScape;
public sealed partial class SettingsWindow
{
    private void NotificationSettings()
    {
        var s=app.Settings;CompletionSoundSettings();
        var deliveryKeys=new[]{"Notch","Windows","Both","Off"};
        var deliveryLabels=new[]{"Островок","Windows","Оба варианта","Откл."};
        var delivery=new ComboBox{ItemsSource=deliveryLabels,SelectedIndex=Math.Max(0,Array.IndexOf(deliveryKeys,s.NotificationDelivery))};UI.Named(delivery,"Доставка уведомлений");
        delivery.SelectionChanged+=(_,_)=>{if(delivery.SelectedIndex>=0){s.NotificationDelivery=deliveryKeys[delivery.SelectedIndex];Save();}};
        page.Children.Add(Section("Уведомления",
            delivery,
            Check("Таймер завершён",s.NotifyTimer,v=>s.NotifyTimer=v),Check("Фокус завершён",s.NotifyFocus,v=>s.NotifyFocus=v),Check("Загрузки завершены",s.NotifyDownloads,v=>s.NotifyDownloads=v),Check("Доступно обновление",s.NotifyUpdates,v=>s.NotifyUpdates=v),Check("Критические сбои приложения",s.NotifyFailures,v=>s.NotifyFailures=v),
            UI.Text("Островок показывает компактный индикатор. Наведите курсор или кликните для деталей. Центр уведомлений хранит последние 50 оповещений сессии.",11,UI.Muted),
            UI.Row(UI.CaptionButton("Тестовое уведомление",app.SendTestNotification,true),UI.CaptionButton("Центр уведомлений",app.OpenNotifications)),UI.Text("Открыть центр уведомлений можно через значок колокольчика на панели островка или горячей клавишей Ctrl+K.",11,UI.Muted)));
        ExternalNotificationSettings();
        NotificationPresentationSettings();
        var folder=UI.Text(s.DownloadsFolder.Length==0?"Стандартная папка «Загрузки» Windows":s.DownloadsFolder,11,UI.Muted);
        page.Children.Add(Section("Загрузки",Check("Отслеживать завершённые загрузки",s.DownloadsEnabled,v=>s.DownloadsEnabled=v),folder,
            UI.Button("Выбрать папку…",()=>{var dialog=new Microsoft.Win32.OpenFolderDialog{Title="Папка для отслеживания загрузок"};if(dialog.ShowDialog(this)==true){s.DownloadsFolder=dialog.FolderName;folder.Text=dialog.FolderName;Save();}}),
            UI.Button("Использовать папку «Загрузки» Windows",()=>{s.DownloadsFolder="";folder.Text="Стандартная папка «Загрузки» Windows";Save();}),
            UI.Text("Отслеживает появление готовых файлов в выбранной папке без вложенных каталогов. Исходные файлы остаются на месте.",11,UI.Muted),
            UI.Text("При отключении наблюдение останавливается. Путь к папке хранится локально на этом ПК.",11,UI.Muted)));
    }
}
