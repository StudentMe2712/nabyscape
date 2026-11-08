using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace NabyScape;
public sealed partial class SettingsWindow
{
    private readonly IslandSurface ambientPreview=new(){Fill=System.Windows.Media.Brushes.Transparent,Notch=false,IsHitTestVisible=false};
    private void InitializeAmbientPreview()
    {
        ambientPreview.SizeChanged+=(_,_)=>{ambientPreview.BeginUpdate();ambientPreview.SurfaceWidth=ambientPreview.ActualWidth;ambientPreview.SurfaceHeight=ambientPreview.ActualHeight;ambientPreview.EndUpdate();};
        IsVisibleChanged+=(_,_)=>RefreshAmbientPreview();StateChanged+=(_,_)=>RefreshAmbientPreview();previewStage.IsVisibleChanged+=(_,_)=>RefreshAmbientPreview();
        Closed+=(_,_)=>app.AmbientAudio.Remove(ambientPreview);
    }
    private void RefreshAmbientPreview()
    {
        var s=app.Settings;
        string widget=previewMode=="Closed"?"closed":previewContent switch{"Music"=>"media","Timer"=>"timer","Focus"=>"focus","Stopwatch"=>"stopwatch","System"=>"system","Note"=>"notes","Tasks"=>"tasks","Calendar"=>"calendar","Shelf"=>"files","Smart clipboard"=>"clips","Launch"=>"shortcuts",_=>"preview"};
        bool allowed=AmbientPolicy.Includes(s,widget),moving=app.Media.Playing && s.LiveActivity && IsVisible && WindowState!=WindowState.Minimized && previewStage.IsVisible;
        ambientPreview.Radius=s.Radius;
        ambientPreview.SetAmbient(AmbientPolicy.ColorsFor(s,app.AmbientAudio.CoverColors),allowed?s.AmbientStrength:0,s.AmbientSpread,s.AmbientMotion,moving,AmbientLook.From(s));
        app.AmbientAudio.Set(ambientPreview,allowed && moving);
    }
    private FrameworkElement PremiumChoice(string name,string value,string[] values,Action<string> change)
    {
        var choice=new ComboBox{MinHeight=32,Margin=new(0,4,0,10)};
        foreach(var item in values)choice.Items.Add(Localization.Text(item));
        choice.SelectedIndex=Math.Max(0,Array.IndexOf(values,value));
        UI.Named(choice,name);
        choice.SelectionChanged+=(_,_)=>{if(choice.SelectedIndex>=0){change(values[choice.SelectedIndex]);Save();}};
        if(name.StartsWith("Quiet hours"))return choice;
        string label=name switch{"Ambient scope"=>"Где отображается свет","Ambient color source"=>"Источник цвета","Cover palette"=>"Цвета обложки","Ambient audio response"=>"Реакция на музыку","Ambient movement"=>"Анимация света","Interaction feedback"=>"Отклик на наведение","Dock side"=>"Положение островка",_=>name};
        var field=new StackPanel();var caption=UI.Caption(label,11,UI.Muted);caption.Margin=new(0,4,0,0);field.Children.Add(caption);field.Children.Add(choice);return field;
    }
    private void AmbientSettings()
    {
        var s=app.Settings;var controls=new StackPanel();
        controls.Children.Add(Check("Подсветка островка",s.CoverAmbient,v=>s.CoverAmbient=v));
        controls.Children.Add(Check("Свечение на закрытом островке",s.ClosedAmbient,v=>s.ClosedAmbient=v));
        var widgets=new StackPanel();
        foreach(var pair in Settings.Names.Where(p=>p.Key!="tools"))
        {
            string id=pair.Key;
            widgets.Children.Add(Check(pair.Value+" (подсветка)",s.AmbientWidgets.Contains(id),v=>{if(v){if(!s.AmbientWidgets.Contains(id))s.AmbientWidgets.Add(id);}else s.AmbientWidgets.Remove(id);}));
        }
        var selected=new Expander{Header="Выбрать виджеты",Content=widgets,Margin=new(0,0,0,12),Visibility=s.AmbientScope=="Selected widgets"?Visibility.Visible:Visibility.Collapsed};
        controls.Children.Add(PremiumChoice("Ambient scope",s.AmbientScope,new[]{"All widgets","Music only","Selected widgets"},v=>{s.AmbientScope=v;selected.Visibility=v=="Selected widgets"?Visibility.Visible:Visibility.Collapsed;}));controls.Children.Add(selected);
        var custom=new StackPanel{Visibility=s.AmbientColorSource.StartsWith("Custom")?Visibility.Visible:Visibility.Collapsed};
        ColorControl(custom,"Пользовательский цвет подсветки",s.AmbientCustomColor,v=>s.AmbientCustomColor=v,new[]{"#7AA8DC","#D9A46D","#B4A0DC","#73BFA6"});
        var secondColor=new StackPanel{Visibility=s.AmbientColorSource=="Custom blend"?Visibility.Visible:Visibility.Collapsed};
        ColorControl(secondColor,"Второй цвет подсветки",s.AmbientSecondaryColor,v=>s.AmbientSecondaryColor=v,new[]{"#CB72D9","#50BBD7","#E6AF62","#76C18E"});custom.Children.Add(secondColor);
        controls.Children.Add(PremiumChoice("Ambient color source",s.AmbientColorSource,new[]{"Album cover","Accent color","Custom color","Custom blend"},v=>{s.AmbientColorSource=v;custom.Visibility=v.StartsWith("Custom")?Visibility.Visible:Visibility.Collapsed;secondColor.Visibility=v=="Custom blend"?Visibility.Visible:Visibility.Collapsed;}));controls.Children.Add(custom);
        controls.Children.Add(UI.Text("Цвет обложки альбома мягко рассеивается по краям островка. Без обложки используется ваш акцентный цвет.",11,UI.Muted));
        AddSlider(controls,"Интенсивность подсветки",s.AmbientStrength*100,0,100,v=>s.AmbientStrength=v/100,"%",1);
        controls.Children.Add(PremiumChoice("Cover palette",s.AmbientPalette,new[]{"Single color","Cover blend"},v=>s.AmbientPalette=v));
        var fine=new StackPanel();
        fine.Children.Add(PremiumChoice("Light direction",s.AmbientDirection,new[]{"Bottom","Top","Left","Right","Surround"},v=>s.AmbientDirection=v));
        AddSlider(fine,"Баланс цвета",s.AmbientBalance*100,0,100,v=>s.AmbientBalance=v/100,"%",1);
        AddSlider(fine,"Дальность свечения",s.AmbientHeight*100,25,200,v=>s.AmbientHeight=v/100,"%",1);
        AddSlider(fine,"Мягкость подсветки",s.AmbientSoftness*100,0,100,v=>s.AmbientSoftness=v/100,"%",1);
        fine.Children.Add(UI.Text("Один цвет использует доминирующий оттенок. Градиент смешивает два цвета обложки. В покое закрытый островок остается темным.",11,UI.Muted));
        AddSlider(fine,"Рассеивание света",s.AmbientSpread*100,25,250,v=>s.AmbientSpread=v/100,"%",1);
        controls.Children.Add(Advanced("Дополнительная настройка света",fine));
        controls.Children.Add(PremiumChoice("Ambient audio response",s.AmbientAudioResponse,new[]{"Off","Soft","Responsive","Punchy"},v=>s.AmbientAudioResponse=v));
        AddSlider(controls,"Чувствительность к звуку",s.AmbientSensitivity*100,50,300,v=>s.AmbientSensitivity=v/100,"%",5);
        controls.Children.Add(UI.Text("Подсветка следует за громкостью выбранного плеера. Мягкая — плавные переходы; Динамичная — быстрый отклик на бас.",11,UI.Muted));
        controls.Children.Add(PremiumChoice("Ambient movement",s.AmbientMotion,new[]{"Still","Breathe","Drift"},v=>s.AmbientMotion=v));
        controls.Children.Add(UI.Text("Статичная — свечение неподвижно. Дыхание и Движение создают мягкую пульсацию в такт музыке.",11,UI.Muted));
        page.Children.Add(Section("Ambient light",controls));
    }
    private void NotificationPresentationSettings()
    {
        var s=app.Settings;var controls=new StackPanel();
        controls.Children.Add(PremiumChoice("Notification animation",s.NotificationAnimation,new[]{"Lift","Fade","Soft scale"},v=>s.NotificationAnimation=v));
        AddSlider(controls,"Время показа уведомления",s.NotificationSeconds,4,30,v=>s.NotificationSeconds=v," с",1);
        controls.Children.Add(UI.Text("Компактные уведомления исчезают по истечении этого времени.",11,UI.Muted));
        controls.Children.Add(UI.Button("Тестовое уведомление",()=>{app.Notifications.Post(new Settings{NotificationDelivery="Notch"},new("preview","Обновление островка","Это тестовое превью уведомления. Ваши файлы и сессии не затронуты."),false,_=>{},DateTimeOffset.UtcNow);app.OpenNotifications();}));
        controls.Children.Add(Check("Тихие часы по расписанию",s.QuietHours,v=>s.QuietHours=v));
        string[] hours=Enumerable.Range(0,24).Select(h=>h.ToString("00")+":00").ToArray();
        var schedule=new Grid();schedule.ColumnDefinitions.Add(new());schedule.ColumnDefinitions.Add(new());
        var from=new StackPanel{Margin=new(0,0,6,0)};from.Children.Add(UI.Text("С (время)",11,UI.Muted));from.Children.Add(PremiumChoice("Quiet hours start",hours[s.QuietStart],hours,v=>s.QuietStart=Array.IndexOf(hours,v)));
        var to=new StackPanel{Margin=new(6,0,0,0)};to.Children.Add(UI.Text("До",11,UI.Muted));to.Children.Add(PremiumChoice("Quiet hours end",hours[s.QuietEnd],hours,v=>s.QuietEnd=Array.IndexOf(hours,v)));Grid.SetColumn(to,1);schedule.Children.Add(from);schedule.Children.Add(to);controls.Children.Add(schedule);
        controls.Children.Add(UI.Text("В тихие часы уведомления и сигналы таймеров не воспроизводят звук.",11,UI.Muted));
        var snoozeStatus=UI.Text("",11,UI.Muted);void Status()=>snoozeStatus.Text=s.NotificationsSnoozedUntil is {} until && until>DateTimeOffset.UtcNow?"Отложено до "+until.ToLocalTime().ToString("HH:mm"):"Уведомления включены.";Status();controls.Children.Add(snoozeStatus);
        var actions=new WrapPanel();
        foreach(int minutes in new[]{15,30,60})actions.Children.Add(UI.Button("Отложить на "+minutes+" мин",()=>{s.NotificationsSnoozedUntil=DateTimeOffset.UtcNow.AddMinutes(minutes);app.Notifications.Clear();Save();Status();}));
        actions.Children.Add(UI.Button("Включить уведомления",()=>{s.NotificationsSnoozedUntil=null;Save();Status();}));controls.Children.Add(actions);
        controls.Children.Add(UI.Text("Режим «Не беспокоить» отключается автоматически по истечении времени.",11,UI.Muted));
        page.Children.Add(Section("Notification presentation",controls));
    }
}
