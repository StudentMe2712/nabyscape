using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NabyScape;

public sealed partial class SettingsWindow
{
    private readonly TextBlock brightnessState=UI.Text("",11,UI.Muted);
    private FrameworkElement StyleChoice(string label,string selected,Action<string> change)
    {
        var row=new Grid{Margin=new(0,4,0,10)};row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=new GridLength(152)});row.Children.Add(UI.Text(label,12));
        var box=new ComboBox();bool navigation=!label.StartsWith("Launch");foreach(string title in navigation?new[]{"Иконки + текст","Только иконки","Только текст","Точки"}:new[]{"Иконки + текст","Только иконки","Только текст"})box.Items.Add(title);var values=navigation?new[]{"IconsLabels","Icons","Text","Dots"}:new[]{"IconsLabels","Icons","Text"};box.SelectedIndex=Math.Max(0,Array.IndexOf(values,selected));UI.Named(box,label);Grid.SetColumn(box,1);row.Children.Add(box);box.SelectionChanged+=(_,_)=>{if(box.SelectedIndex>=0){change(values[box.SelectedIndex]);previewMode="Panel";previewContent=label.StartsWith("Launch")?"Launch":"Navigation";Save();}};var group=new StackPanel();group.Children.Add(row);group.Children.Add(UI.Text(label.StartsWith("Launch")?"Стиль отображения сохранённых ярлыков.":"Точки делают навигацию минималистичной. Используйте Ctrl+Tab для переключения.",11,UI.Muted));group.Margin=new(0,0,0,14);return group;
    }
    private void VisibilityControls()
    {
        var s=app.Settings;var modifiers=new ComboBox();foreach(string label in new[]{"Ctrl+Alt","Ctrl+Shift","Alt+Shift"})modifiers.Items.Add(label);modifiers.SelectedItem=s.VisibilityHotkeyModifiers;UI.Named(modifiers,"Модификаторы клавиш скрытия");modifiers.SelectionChanged+=(_,_)=>{s.VisibilityHotkeyModifiers=modifiers.SelectedItem?.ToString()??"Ctrl+Alt";Save();};
        var key=new ComboBox();foreach(string label in Enumerable.Range('A',26).Select(n=>((char)n).ToString()))key.Items.Add(label);key.SelectedItem=s.VisibilityHotkeyKey;UI.Named(key,"Клавиша скрытия");key.SelectionChanged+=(_,_)=>{s.VisibilityHotkeyKey=key.SelectedItem?.ToString()??"H";Save();};
        var keys=new Grid();keys.ColumnDefinitions.Add(new());keys.ColumnDefinitions.Add(new(){Width=new GridLength(80)});keys.Children.Add(modifiers);Grid.SetColumn(key,1);keys.Children.Add(key);
        page.Children.Add(Section("Hide or show the island",Check("Включить горячую клавишу скрытия",s.VisibilityHotkeyEnabled,v=>s.VisibilityHotkeyEnabled=v),keys,UI.Text("По умолчанию: Ctrl+Alt+H. Полностью скрывает или возвращает островок.",11,UI.Muted)));
        var tuckModifiers=new ComboBox{ItemsSource=new[]{"Ctrl+Alt","Ctrl+Shift","Alt+Shift"},SelectedItem=s.TuckHotkeyModifiers};UI.Named(tuckModifiers,"Модификаторы клавиш прижатия");tuckModifiers.SelectionChanged+=(_,_)=>{s.TuckHotkeyModifiers=tuckModifiers.SelectedItem?.ToString()??"Ctrl+Alt";Save();};
        var tuckKey=new ComboBox{ItemsSource=Enumerable.Range('A',26).Select(n=>((char)n).ToString()).ToArray(),SelectedItem=s.TuckHotkeyKey};UI.Named(tuckKey,"Клавиша прижатия");tuckKey.SelectionChanged+=(_,_)=>{s.TuckHotkeyKey=tuckKey.SelectedItem?.ToString()??"T";Save();};
        page.Children.Add(Section("Manual tuck shortcut",Check("Включить горячую клавишу прижатия",s.TuckHotkeyEnabled,v=>s.TuckHotkeyEnabled=v),tuckModifiers,tuckKey,UI.Text("По умолчанию: Ctrl+Alt+T. Первое нажатие прижимает островок к кромке, второе возвращает обычное поведение.",11,UI.Muted)));
    }
    private void AudioFeedbackSettings()
    {
        var s=app.Settings;
        page.Children.Add(Section("Live feedback",Check("Живая активность · звук, прогресс и пульсация фокуса",s.LiveActivity,v=>s.LiveActivity=v),Check("Индикация при изменении громкости и устройств",s.ReactiveFeedback,v=>s.ReactiveFeedback=v),Check("Мягкая анимация волны в паузах",s.WaveformIdleMotion,v=>s.WaveformIdleMotion=v),UI.Text("Уровень звука управляет анимацией волны.",11,UI.Muted),Check("Индикатор громкости на островке",s.VolumeFeedback,v=>s.VolumeFeedback=v),Check("Индикатор яркости на островке",s.BrightnessFeedback,v=>s.BrightnessFeedback=v),brightnessState,UI.Text("Индикация яркости отображается при изменении системной яркости экрана.",11,UI.Muted)));
    }
}
