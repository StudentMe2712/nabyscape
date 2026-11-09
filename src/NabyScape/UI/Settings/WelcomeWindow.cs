using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NabyScape;

internal sealed class WelcomeWindow : Window
{
    private readonly App app;
    private readonly StackPanel body=new(){Margin=new(28,20,28,20)};
    private readonly TextBlock progress=UI.Text("",12,UI.Muted);
    private readonly TextBlock error=UI.Text("",12,UI.Muted);
    private readonly Button back,next;
    private readonly Dictionary<string,bool> widgets;
    private readonly HashSet<string> changedWidgets=new();
    private bool edgeChanged;
    private string edge;
    private int step;
    private string purpose="Custom";

    internal WelcomeWindow(App app)
    {
        this.app=app;edge=app.Settings.DockEdge;
        widgets=app.Settings.Widgets.ToDictionary(w=>w.Id,w=>w.Enabled);
        Title="Добро пожаловать в Desktopia";Width=580;Height=620;MinWidth=420;MinHeight=360;
        MaxHeight=SystemParameters.WorkArea.Height;MaxWidth=SystemParameters.WorkArea.Width;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=Accessibility.HighContrast?SystemColors.WindowBrush:UI.Brush("#111214");
        Foreground=Accessibility.HighContrast?SystemColors.WindowTextBrush:UI.Brush("#F3F4F6");
        FontFamily=new FontFamily("Segoe UI Variable, Segoe UI");FontSize=13;
        var layout=new Grid();layout.RowDefinitions.Add(new());layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var scroll=UI.Scroll(body);layout.Children.Add(scroll);
        var footer=new WrapPanel{Margin=new(24,12,24,20)};
        var skip=UI.CaptionButton("Пропустить",Skip);footer.Children.Add(skip);
        back=UI.CaptionButton("Назад",()=>{step--;Render();});next=UI.CaptionButton("Далее",()=>{if(step<2){step++;Render();}else Finish();},true);
        footer.Children.Add(back);footer.Children.Add(next);Grid.SetRow(footer,1);layout.Children.Add(footer);
        Content=layout;Render();
    }

    private void Render()
    {
        body.Children.Clear();progress.Text=$"НАЧАЛО РАБОТЫ  /  ШАГ {step+1} ИЗ 3";body.Children.Add(progress);
        back.Visibility=step==0?Visibility.Collapsed:Visibility.Visible;
        next.Content=step==2?"Завершить настройку":"Далее";
        body.Children.Add(UI.Text(step==0?"С чего начнём?":step==1?"Настройте под себя":"Островок готов к работе!",28));
        if(step==0)
        {
            body.Children.Add(UI.Text("Выберите вариант для начала. Любые параметры можно изменить позже в настройках.",13,UI.Muted));
            var choices=new[]{
                ("Music","Музыка","Воспроизведение, громкость, звук и треки"),
                ("Productivity","Продуктивность","Фокус, таймер, заметки, задачи и перезагрузка дня"),
                ("Custom","Рекомендуемый","Полный набор возможностей Desktopia по умолчанию")
            };
            foreach(var choice in choices)
            {
                var content=new StackPanel();content.Children.Add(UI.Text(choice.Item2+(purpose==choice.Item1?" · Выбрано":""),16));var hint=UI.Text(choice.Item3,12,UI.Muted);hint.Margin=new(0,5,0,0);content.Children.Add(hint);
                var button=UI.CaptionButton(choice.Item2,()=>{purpose=choice.Item1;ChoosePurpose();Render();});button.Content=content;button.Padding=new(16);button.Margin=new(0,10,0,0);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Background=UI.Brush(purpose==choice.Item1?app.Settings.SelectionFill:app.Settings.ControlFill);body.Children.Add(button);
            }
            body.Children.Add(UI.Text("Выберите подходящий профиль или оставьте рекомендуемый со всеми виджетами.",12,UI.Muted));
        }
        else if(step==1)
        {
            body.Children.Add(UI.Text("Расположение островка",16));var edges=new WrapPanel();
            var sides=new[]{("Top","Вверху"),("Bottom","Внизу"),("Left","Слева"),("Right","Справа")};
            foreach(var side in sides)
            {
                var option=new RadioButton{Content=side.Item2,GroupName="Dock position",IsChecked=edge==side.Item1,Margin=new(0,10,16,14),Padding=new(6)};
                UI.Named(option,"Расположение: "+side.Item2);option.Checked+=(_,_)=>{edge=side.Item1;edgeChanged=true;};edges.Children.Add(option);
            }
            body.Children.Add(edges);body.Children.Add(UI.Text("Ваши виджеты",16));
            foreach(var choice in app.Settings.Widgets.Where(w=>purpose=="Custom" || (widgets.ContainsKey(w.Id) && widgets[w.Id])))
            {
                var id=choice.Id;var toggle=new CheckBox{Content=Settings.Names.GetValueOrDefault(id)??id,IsChecked=widgets.GetValueOrDefault(id,true),Margin=new(0,12,0,3),Padding=new(6)};
                UI.Named(toggle,"Включить "+id);toggle.Click+=(_,_)=>{widgets[id]=toggle.IsChecked==true;changedWidgets.Add(id);};body.Children.Add(toggle);
            }
            body.Children.Add(UI.Text("Все виджеты всегда доступны для настройки в разделе «Настройки».",12,UI.Muted));
        }
        else
        {
            body.Children.Add(UI.Text("Нажмите на островок, чтобы открыть виджеты. Наведите курсор для быстрого просмотра. Кликните правой кнопкой мыши по значку в трее, чтобы открыть Настройки.",14,UI.Muted));
            body.Children.Add(UI.Text(string.Join(" · ",widgets.Where(w=>w.Value).Select(w=>Settings.Names.GetValueOrDefault(w.Key)??w.Key)),13));
            body.Children.Add(UI.Text("История буфера: "+(app.Settings.ClipboardHistory?"Вкл.":"Откл.")+" · Оповещения о загрузках: "+(app.Settings.DownloadsEnabled?"Вкл.":"Откл.")+". Вы можете изменить это в любой момент.",12,UI.Muted));
            body.Children.Add(UI.Text("Вы всегда можете открыть это руководство повторно через Настройки > Поведение.",12,UI.Muted));
        }
        body.Children.Add(error);
    }

    private void ChoosePurpose()
    {
        if(purpose=="Custom"){foreach(var widget in app.Settings.Widgets)widgets[widget.Id]=widget.Enabled;changedWidgets.Clear();return;}
        string[] enabled=purpose switch
        {
            "Music"=>new[]{"media","volume","clock"},
            "Productivity"=>new[]{"timer","focus","reset","notes","tasks"},
            _=>new[]{"media","timer","focus","reset","notes","system"}
        };
        foreach(string id in widgets.Keys.ToArray()){widgets[id]=enabled.Contains(id);changedWidgets.Add(id);}
    }
    private bool MarkComplete()
    {
        try{WelcomeSetup.Complete(app.Store);return true;}
        catch(Exception ex){app.Store.Log(ex);error.Text="Не удалось сохранить настройки. Проверьте папку настроек и попробуйте снова.";return false;}
    }

    private void Skip(){if(MarkComplete())Close();}

    private void Finish()
    {
        Settings candidate;
        try
        {
            candidate=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(app.Settings))!;
            if(edgeChanged)candidate.DockEdge=edge;
            foreach(var widget in candidate.Widgets)if(changedWidgets.Contains(widget.Id))widget.Enabled=widgets[widget.Id];
            if(purpose!="Custom")candidate.HoverFollowsWidgets=true;
            candidate.Normalize();app.Store.Save("settings",candidate);
        }
        catch(Exception ex)
        {
            app.Store.Log(ex);error.Text="Не удалось сохранить изменения. Предыдущие настройки не изменены.";return;
        }
        app.Settings.DockEdge=candidate.DockEdge;app.Settings.HoverFollowsWidgets=candidate.HoverFollowsWidgets;
        foreach(var widget in app.Settings.Widgets)if(candidate.Widgets.FirstOrDefault(w=>w.Id==widget.Id) is {} savedWidget)widget.Enabled=savedWidget.Enabled;
        foreach(var tool in app.Settings.Tools)if(candidate.Tools.FirstOrDefault(t=>t.Id==tool.Id) is {} savedTool)tool.Enabled=savedTool.Enabled;
        app.ChangeSettings();if(!MarkComplete())return;Close();
    }
}
