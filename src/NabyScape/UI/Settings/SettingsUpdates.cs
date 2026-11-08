using System;
using System.Windows;
using System.Windows.Controls;
namespace NabyScape;
public sealed partial class SettingsWindow
{
    private TextBlock? updateStatus;
    private Button? downloadUpdate,installUpdate,restoreUpdate;
    private void UpdateControls()=>Dispatcher.BeginInvoke(()=>
    {
        if(downloadUpdate!=null)downloadUpdate.IsEnabled=!app.Updates.Busy && app.Updates.Available!=null;
        if(installUpdate!=null)installUpdate.IsEnabled=!app.Updates.Busy && app.Updates.VerifiedSetup!=null && OfficialUpdates.IsAutomaticInstallPath(Environment.ProcessPath!);
        if(restoreUpdate!=null)restoreUpdate.IsEnabled=!app.Updates.Busy && UpdateRecovery.PreviousId()!=null && OfficialUpdates.IsAutomaticInstallPath(Environment.ProcessPath!);
        if(updateStatus!=null){string status=Localization.Text(app.Updates.Status)+"\n"+Localization.Text("Доступно:")+" "+(app.Updates.Available?.Version.ToString()??Localization.Text("Нет доступных обновлений"));if(updateStatus.Text!=status)Accessibility.Announce(updateStatus,status);}
    });
    private void UpdatesSection()
    {
        var auto=new CheckBox{Content="Проверять обновления раз в день",IsChecked=app.Settings.AutoCheckUpdates};UI.Named(auto,"Проверять обновления раз в день");auto.Click+=(_,_)=>{app.Settings.AutoCheckUpdates=auto.IsChecked==true;Save();};auto.ToolTip="Проверка обновлений при запуске, если прошло не менее 24 часов.";
        var download=new CheckBox{Content="Скачивать обновления автоматически",IsChecked=app.Settings.AutoDownloadUpdates};UI.Named(download,"Скачивать обновления автоматически");download.Click+=(_,_)=>{app.Settings.AutoDownloadUpdates=download.IsChecked==true;Save();};
        updateStatus=UI.Text(Localization.Text(app.Updates.Status),12,UI.Muted);UI.Named(updateStatus,"Статус обновлений");
        page.Children.Add(Section("Updates",UI.Text("Установлена версия: "+OfficialUpdates.InstalledVersion+" · "+RuntimePlatform.Rid,13),
            UI.Caption("Проверка новых релизов, скачивание и установка в удобное время.",12),auto,download,
            UI.Caption("Автоматическое скачивание выполняется после проверки при запуске. Установка происходит только по вашей команде.",11,UI.Muted),updateStatus,
            UI.CaptionButton("Проверить обновления",()=>{_=app.Updates.Check(true);}),
            UI.CaptionButton("Список изменений",()=>ShortcutService.Open(new(){Target=app.Updates.Available?.Notes.AbsoluteUri??OfficialUpdates.Repository+"/releases"})),
            downloadUpdate=UI.CaptionButton("Скачать обновление",()=>{_=app.Updates.Download();}),
            UI.CaptionButton("Отменить",app.Updates.Cancel),
            installUpdate=UI.CaptionButton("Установить и перезапустить",async ()=>{try{await UpdateHandoff.Prepare(app);}catch(Exception ex){updateStatus.Text=ex.Message;app.Store.Log(ex);}}),
            UI.Caption(OfficialUpdates.IsAutomaticInstallPath(Environment.ProcessPath!)?"Перед установкой создается резервная копия программы и профилей для безопасного восстановления.":OfficialUpdates.IsInstalled(Environment.ProcessPath!)?"Для обновления закройте NabyScape и запустите установщик новой версии.":"Портативная версия: скачайте ZIP-архив и распакуйте с заменой файлов.",11,UI.Muted),
            UI.Text(UpdateRecovery.Description(),11,UI.Muted),
            restoreUpdate=UI.CaptionButton("Восстановить предыдущую версию",async ()=>
            {
                if(MessageBox.Show(this,"Восстановить предыдущую версию приложения и её настройки? Локальные медиафайлы сохранятся.","Восстановление версии",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
                try{await UpdateHandoff.Prepare(app,true);}catch(Exception ex){updateStatus.Text=ex.Message;app.Store.Log(ex);}
            }),
            UI.Caption("Загрузка выполняется из официального репозитория GitHub с проверкой контрольной суммы SHA-256.",11,UI.Muted)));
        downloadUpdate.ToolTip="Скачать официальный установщик для архитектуры системы с проверкой SHA-256.";
        installUpdate.ToolTip="Сохранить параметры, создать резервную копию и установить обновление.";
        UpdateControls();
    }
}
