using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NabyScape;

public sealed partial class SettingsWindow
{
    private void InteractionFeatureControls()
    {
        var s=app.Settings;
        var edge=new StackPanel();AddSlider(edge,"Высота спрятанной кромки",s.TuckEdge,0,12,v=>s.TuckEdge=v," px");edge.Children.Add(UI.Text("0 скрывает островок полностью. Используйте наведение на кромку или горячую клавишу для возврата.",11,UI.Muted));
        page.Children.Add(Section("Автоматическое скрытие",Check("Всегда за кромкой",s.AlwaysTucked,v=>s.AlwaysTucked=v),edge,Check("Сворачивать в развёрнутых окнах",s.HideMaximized,v=>s.HideMaximized=v),Check("Сворачивать в полноэкранных приложениях",s.HideFullscreen,v=>s.HideFullscreen=v),UI.Text("Оставляет незаметную полоску у верхней кромки экрана. При подведении курсора мыши островок плавно выдвигается.",11,UI.Muted)));
        page.Children.Add(Section("Навигация панели",Check("Переключение панелей смахиванием",s.PanelSwipe,v=>s.PanelSwipe=v),UI.Text("Смахивайте или используйте Shift+колёсико для переключения между вкладками. С клавиатуры: Ctrl+Tab.",11,UI.Muted)));
        page.Children.Add(Section("Перетаскивание на островок",Check("Полка при перетаскивании файлов",s.DropReveal,v=>s.DropReveal=v),UI.Text("Перетаскивайте файлы прямо на островок или его спрятанную кромку для быстрого размещения на полке.",11,UI.Muted)));
        page.Children.Add(Section("Открепляемые мини-окна",Check("Открепляемые мини-окна",s.DetachablePreviews,v=>s.DetachablePreviews=v),UI.Text("Потяните за маркер или выберите «Открепить», чтобы открыть компактное плавающее окно. Поддерживает Музыку, Таймер, Фокус, Заметки и Систему.",11,UI.Muted)));
        page.Children.Add(Section("Управление по клику на обложку",Check("Управление по клику на обложку",s.CoverGestures,v=>s.CoverGestures=v),UI.Text("Клик по мини-обложке запускает или приостанавливает трек; удержание открывает выбор медиаплеера.",11,UI.Muted)));
    }
    private FrameworkElement FeaturePreview(string kind)
    {
        var panel=new StackPanel();
        var s=app.Settings;
        if(kind=="Smart clipboard")return ClipboardExample();
        if(kind is "Control colors" or "Typography" or "Panel navigation")return ControlExample(kind);
        string? inactive=kind switch
        {
            "Drop zone" when !s.DropReveal=>"Появление полки выключено. Откройте полку для добавления файлов.",
            "Detached preview" when !s.DetachablePreviews=>"Открепляемые мини-окна выключены.",
            "Cover gestures" when !s.CoverGestures=>"Управление по обложке выключено. Клик открывает панель.",
            "Cover gestures" when s.ClosedMediaIcon!="Artwork"=>"Выберите «Обложка альбома» в настройках отображения для использования жестов.",
            _=>null
        };
        if(inactive!=null){panel.Children.Add(UI.Text(inactive,12,UI.Muted));return panel;}
        if(kind=="Drop zone")
        {
            var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
            for(int i=0;i<3;i++){var icon=Icons.Create(i==1?"music":"nav-tools",26);icon.Margin=new(9,0,9,0);var move=new TranslateTransform();icon.RenderTransform=move;row.Children.Add(icon);int index=i;previewRealtimeFrames.Add(t=>move.Y=app.Settings.ReduceMotion?0:Math.Sin(t*2+index)*3);}
            panel.Children.Add(row);panel.Children.Add(UI.Text("3 файла · бросьте для прикрепления",12));
        }
        else if(kind=="Detached preview")return DetachedExample();
        else if(kind=="Cover gestures")
        {
            var state=UI.Text("Клик по обложке — играть",12);var cover=UI.Button("Обложка альбома",()=>state.Text=state.Text.Contains("играет")?"Пауза":"Музыка играет");cover.Content=Icons.Create("music",24);cover.Width=44;cover.Height=44;cover.HorizontalAlignment=HorizontalAlignment.Left;panel.Children.Add(cover);panel.Children.Add(state);
        }
        else
        {panel.Children.Add(new Border{Height=28,Background=UI.Brush("#8E9AAF"),CornerRadius=new(4)});panel.Children.Add(UI.Text("#8E9AAF · rgb(142, 154, 175)",12));panel.Children.Add(UI.Text("Скопировать HEX    Скопировать RGB",11,UI.Muted));}
        return panel;
    }
}
