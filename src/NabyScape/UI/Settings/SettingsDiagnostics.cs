using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace NabyScape;

public sealed partial class SettingsWindow
{
    // Parent integration: call DiagnosticsSection() from General(). No report is written until Export is chosen.
    internal void DiagnosticsSection()
    {
        var preview=new TextBox
        {
            IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=180,MaxHeight=320,
            Text=PrivateDiagnostics.CreateReport(app.Store)
        };
        UI.LocalizedName(preview,"Предпросмотр отчёта диагностики");
        var notice=UI.Caption("Ознакомьтесь с отчётом перед копированием или экспортом. Никакие данные не отправляются автоматически.",11,UI.Muted);
        Accessibility.Live(notice);
        var refresh=UI.CaptionButton("Обновить предпросмотр",()=>
        {
            preview.Text=PrivateDiagnostics.CreateReport(app.Store);
            Accessibility.Announce(notice,Localization.Text("Предпросмотр диагностики обновлён."));
        });
        var copy=UI.CaptionButton("Скопировать отчёт",()=>
        {
            try
            {
                Clipboard.SetText(preview.Text);
                Accessibility.Announce(notice,Localization.Text("Отчёт диагностики скопирован в буфер обмена."));
            }
            catch(Exception ex)
            {
                PrivateDiagnostics.Record(DiagnosticStage.DiagnosticsCopy,PrivateDiagnostics.Classify(ex));
                Accessibility.Announce(notice,Localization.Text("Не удалось скопировать отчёт. Попробуйте снова."));
            }
        });
        var export=UI.CaptionButton("Экспортировать отчёт",()=>
        {
            try
            {
                var dialog=new SaveFileDialog
                {
                    Title=Localization.Text("Экспортировать отчёт диагностики"),
                    Filter=Localization.Text("Отчёт JSON (*.json)|*.json"),DefaultExt=".json",AddExtension=true,
                    FileName="NabyScape-diagnostics.json",OverwritePrompt=true,CheckPathExists=true
                };
                if(dialog.ShowDialog(this)!=true)return;
                using(var stream=new FileStream(dialog.FileName,FileMode.Create,FileAccess.Write,FileShare.None))
                {
                    var bytes=new UTF8Encoding(false).GetBytes(preview.Text);
                    stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                Accessibility.Announce(notice,Localization.Text("Отчёт диагностики успешно экспортирован."));
            }
            catch(Exception ex)
            {
                PrivateDiagnostics.Record(DiagnosticStage.DiagnosticsExport,PrivateDiagnostics.Classify(ex));
                Accessibility.Announce(notice,Localization.Text("Не удалось экспортировать отчёт. Выберите другую папку."));
            }
        });
        page.Children.Add(Section("Конфиденциальная диагностика",
            UI.Caption("Включает версии приложения и среды, архитектуру, статус хранилища и до 64 последних событий диагностики. Имена пользователей, пути, персональные ключи, аудио или скриншоты никогда не включаются.",11,UI.Muted),
            UI.Caption("Локальный файл errors.log может содержать подробные данные и никогда не включается в данный отчёт.",11,UI.Muted),
            preview,refresh,copy,export,notice,
            UI.Caption("Восстановление хранилища",13),
            UI.Caption("Если документ настроек повреждён, он защищается от автоматической перезаписи. Для восстановления закройте NabyScape и сохраните оригинальный JSON файл перед откатом на резервную копию.",11,UI.Muted)));
    }
}
