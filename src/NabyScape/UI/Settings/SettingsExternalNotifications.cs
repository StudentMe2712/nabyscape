using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace NabyScape;

public sealed partial class SettingsWindow
{
    private void ExternalNotificationSettings()
    {
        var s=app.Settings;
        // Live state, not help text: a distinct brush keeps it visible in the essentials view.
        var status=UI.Text("",11,SettingsTheme.Quiet);
        var apps=new StackPanel{Margin=new(0,4,0,0)};
        var appsLabel=UI.Caption("Приложения",12,UI.Muted);appsLabel.Margin=new(0,10,0,2);
        var privacy=UI.CaptionButton("Открыть доступ к уведомлениям Windows",()=>ShortcutService.Open(new(){Target="ms-settings:privacy-notifications"}));
        var options=new StackPanel();
        void Refresh()
        {
            string watcher=app.ExternalNotifications?.Status??"Off";
            status.Text=!s.ExternalNotifications?Localization.Text("Откл. · уведомления Windows не считываются."):watcher switch
            {
                "On"=>Localization.Text("Вкл. · новые уведомления от других программ появляются в центре уведомлений."),
                "Blocked by Windows"=>Localization.Text("Windows блокирует доступ. Разрешите доступ к уведомлениям для приложений и включите снова."),
                "Unavailable"=>Localization.Text("Не удалось прочитать уведомления Windows. Попробуйте выключить и включить переключатель."),
                _=>Localization.Text("Запуск…")
            };
            privacy.Visibility=watcher=="Blocked by Windows" && s.ExternalNotifications?Visibility.Visible:Visibility.Collapsed;
            options.Visibility=s.ExternalNotifications?Visibility.Visible:Visibility.Collapsed;
        }
        async void LoadApps()
        {
            apps.Children.Clear();
            string[] seen=Array.Empty<string>();
            try{seen=await ExternalNotificationWatcher.CurrentAppsAsync();}catch(Exception ex){app.Store.Log(ex);}
            var names=seen.Concat(s.ExternalNotificationMuted).Concat(app.Notifications.History.Where(e=>e.Notice.Kind=="external").Select(e=>e.Notice.Source??"")).Where(n=>n.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n,StringComparer.CurrentCultureIgnoreCase).ToArray();
            if(names.Length==0){apps.Children.Add(UI.Caption("Приложения появятся здесь после показа первого уведомления Windows.",11,UI.Muted));return;}
            foreach(string name in names)
            {
                var appName=name;
                var toggle=new CheckBox{Content=UI.Text(appName,12),IsChecked=ExternalNotificationPolicy.Allowed(s,appName)};UI.Named(toggle,appName);
                toggle.Click+=(_,_)=>{s.ExternalNotificationMuted.RemoveAll(n=>string.Equals(n,appName,StringComparison.OrdinalIgnoreCase));if(toggle.IsChecked!=true)s.ExternalNotificationMuted.Add(appName);Save();};
                apps.Children.Add(toggle);
            }
        }
        var enable=Check("Уведомления от других приложений",s.ExternalNotifications,v=>{s.ExternalNotifications=v;Dispatcher.BeginInvoke(()=>{Refresh();if(v)LoadApps();});});
        options.Children.Add(Check("Всплывать на островке",s.ExternalNotificationPopup,v=>s.ExternalNotificationPopup=v));
        options.Children.Add(Check("Показывать текст сообщения во всплывающем окне",s.ExternalNotificationText,v=>s.ExternalNotificationText=v));
        options.Children.Add(Check("Показывать в часы тишины",s.ExternalNotificationQuietHours,v=>s.ExternalNotificationQuietHours=v));
        options.Children.Add(appsLabel);options.Children.Add(apps);
        page.Children.Add(Section("Сторонние приложения",enable,status,privacy,options,
            UI.Text("Desktopia считывает системные уведомления от мессенджеров и других приложений Windows, проверяя новые каждые 1.5 сек. Уведомления хранятся только в течение текущей сессии.",11,UI.Muted)));
        void Changed()=>Dispatcher.BeginInvoke(Refresh);
        if(app.ExternalNotifications is {} watcher){watcher.StatusChanged+=Changed;status.Unloaded+=(_,_)=>watcher.StatusChanged-=Changed;}
        Refresh();if(s.ExternalNotifications)LoadApps();
    }
}
