using System;
using System.Windows;
using System.Windows.Input;

namespace NabyScape;

public sealed partial class SettingsWindow
{
    private void FlushPreferenceChanges()
    {
        Keyboard.ClearFocus();saveTimer.Stop();app.FlushPreferenceEditors();app.ChangeSettings();
    }
    private void ResetPreferences(string? category)
    {
        if(MessageBox.Show(this,PreferenceReset.Summary(category),"Сброс настроек",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
        try{FlushPreferenceChanges();PreferenceReset.Reset(app.Store,app.Settings,category);app.ChangeSettings();Navigate(current);status.Text="Настройки сброшены. Резервную копию можно восстановить в разделе Основные.";}
        catch(Exception ex){status.Text="Сброс отменен: "+ex.Message;app.Store.Log(ex);}
    }
    private void RestorePreferences()
    {
        if(MessageBox.Show(this,"Восстановить настройки из последней резервной копии? Автозапуск, буфер обмена и заметки сохраняются.","Восстановление настроек",MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
        try{FlushPreferenceChanges();PreferenceReset.Restore(app.Store,app.Settings);app.ChangeSettings();Navigate(current);status.Text="Резервная копия настроек восстановлена.";}
        catch(Exception ex){status.Text="Восстановление отменено: "+ex.Message;app.Store.Log(ex);}
    }
}
