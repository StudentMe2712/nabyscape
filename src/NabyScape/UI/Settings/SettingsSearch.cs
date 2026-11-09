using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace NabyScape;
internal sealed record SettingsSearchItem(string Title,string Category,string Section,string Control,string Keywords);
public sealed partial class SettingsWindow
{
    private TextBox? settingsSearch;
    private Border? settingsSearchHeader;
    private Border? searchResultsHost;
    private readonly StackPanel settingsResults=new();
    private SettingsSearchItem[] settingsSearchIndex=Array.Empty<SettingsSearchItem>();
    private static string[] CategorySections(string id)=>Category(id).Sections??(id switch
    {
        "Layout"=>new[]{"Display","Dock placement","Move & resize","Closed island","Open panel","Hover preview","Layout profile"},
        "Content"=>new[]{"Feature usage","Enabled widgets & order","Individual tools","Hover pages","Focus sessions","Stopwatch"},
        "Controls"=>new[]{"Hover & swipe","Panel navigation","Drag to Island","Detachable previews","Closed cover gestures","Notch gestures","Global hotkey"},
        "Audio"=>new[]{"Visualizer","Lyrics","Closed media display","Volume mixer","Waveform source","Live signal","Live feedback","Audio behavior","Header wheel"},
        "System"=>new[]{"Clock","Monitor details"},"Launch"=>new[]{"Launch display","Shortcuts"},
        "Application"=>new[]{"Language & accessibility","Updates","Behavior","Local storage","Private diagnostics","Reset preferences","About Desktopia"},_=>Array.Empty<string>()
    });
    private static SettingsSearchItem[] BuildSettingsSearchIndex()
    {
        var items=new List<SettingsSearchItem>();
        foreach(var category in SettingsCategories)
        {
            items.Add(new(category.Title,category.Id,"","",category.Terms+" "+category.Description));
            foreach(string section in CategorySections(category.Id))items.Add(new(FriendlySection(section),category.Id,section,"",section+" "+category.Title));
        }
        void Add(string title,string category,string section,string control,string terms)=>items.Add(new(title,category,section,control,terms));
        Add("Текст песни на островке","Audio","Closed media display","Closed media content","lyrics текст песни слова lirik караоке трек");
        Add("Использование ресурсов","Content","Feature usage","Connect to music apps","память ram ресурсы виджеты отключение");
        Add("Свечение на закрытом островке","Ambience","Ambient light","Show ambience on closed dock","подсветка свечение ambient glow");
        Add("Яркость подсветки","Ambience","Ambient light","Ambient intensity","интенсивность яркость подсветка свет glow");
        Add("Цвет подсветки и палитра","Ambience","Ambient light","Ambient color source","цвет палитра обложка градиент кастомный");
        Add("Радиус рассеивания света","Ambience","Ambient light","Ambient spread","рассеивание радиус ореол мягкость");
        Add("Высота свечения","Ambience","Ambient light","Light reach","высота охват дальность свет");
        Add("Направление света","Ambience","Ambient light","Light direction","направление свечения сверху снизу по кругу");
        Add("Мягкость подсветки","Ambience","Ambient light","Ambient softness","мягкость сглаживание размытие градиент");
        Add("Баланс цвета","Ambience","Ambient light","Color balance","баланс смешивание цветов оттенки");
        Add("Реакция света на музыку","Ambience","Ambient light","Ambient audio response","музыка бит реакция звук свечение ритм");
        Add("Анимация подсветки","Ambience","Ambient light","Ambient movement","дыхание пульсация движение анимация");
        Add("Кромка экрана островка","Layout","Dock placement","Dock side","кромка экрана край верх низ право лево");
        Add("Точная позиция островка","Layout","Move & resize","Horizontal position percent","позиция центр пиксели отступ смещение");
        Add("Плавное следование за монитором","Layout","Display","Slide smoothly between monitors","монитор экран курсор следование плавность");
        Add("Порядок виджетов","Content","Enabled widgets & order","","виджеты порядок перетаскивание сортировка");
        Add("Превью при наведении","Content","Hover pages","Show all active widgets in hover","наведение превью свайп карточки");
        Add("Размер закрытого островка","Layout","Closed island","Closed width","размер ширина высота компактный");
        Add("Размер раскрытой панели","Layout","Open panel","Panel width","размер панели ширина высота максимум");
        Add("Автоматическое скрытие и прижатие","Visibility","Automatic tucking","","скрытие прижатие кромка край автоскрытие");
        Add("Превью YouTube и браузера","Visibility","Automatic activity","Automatic browser media previews","превью браузер ютуб chrome media");
        Add("Горячая клавиша","Controls","Global hotkey","","сочетание клавиш хоткей открыть островок");
        Add("Текст песен","Audio","Lyrics","","текст слова песен lyrics lrc синхронизация");
        Add("Аудиовизуализатор","Audio","Visualizer","","волна спектр визуализатор эквалайзер частоты");
        Add("Громкость и динамики","Audio","Volume mixer","","звук громкость микшер устройство вывод");
        Add("История буфера обмена","Clipboard","Clipboard history","Save text & image history locally","буфер обмена история копирование вставка");
        Add("Удаление Desktopia","Application","Local storage","Uninstall Desktopia…","удаление деинсталляция очистить кэш сброс");
        Add("Длительность фокуса и таймера","Content","Focus sessions","","таймер фокус помодоро минуты перерыв");
        Add("Звуки уведомлений","Notifications","Completion sound","","звук уведомление сигнал таймер окончание");
        Add("Уведомления от других программ","Notifications","Other apps","Show notifications from other apps","уведомления telegram discord claude windows");
        Add("Тихие часы","Notifications","Notification presentation","","тихие часы ночь без звука расписание");
        Add("Запуск при старте Windows","Application","Behavior","Start automatically when signing in to Windows","автозапуск автостарт windows включение");
        return items.ToArray();
    }
    private void InitializeSettingsSearch(Grid main,Grid workspace,Border fieldHost)
    {
        settingsSearchIndex=BuildSettingsSearchIndex();settingsSearch=UI.Input("Поиск параметров","");settingsSearch.Background=Brushes.Transparent;settingsSearch.BorderThickness=new(0);settingsSearch.Margin=new(0);settingsSearch.Padding=new(0,6,0,6);settingsSearch.MinHeight=34;
        var row=new DockPanel();var glyph=Icons.Create("search",15,UI.Muted);glyph.Margin=new(0,0,10,0);DockPanel.SetDock(glyph,Dock.Left);row.Children.Add(glyph);
        var clear=Icons.Button("x","Очистить поиск",()=>settingsSearch.Clear(),false,28);clear.Background=Brushes.Transparent;clear.Margin=new(5,0,0,0);clear.Visibility=Visibility.Collapsed;DockPanel.SetDock(clear,Dock.Right);row.Children.Add(clear);
        var inputHost=new Grid();inputHost.Children.Add(settingsSearch);var hint=UI.Caption("Поиск · Ctrl+F",12,UI.Muted);hint.IsHitTestVisible=false;inputHost.Children.Add(hint);row.Children.Add(inputHost);
        var field=new Border{Child=row,Background=SettingsTheme.Card,BorderBrush=SettingsTheme.Line,BorderThickness=new(1),CornerRadius=new(10),Padding=new(12,0,6,0),Margin=new(0)};settingsSearchHeader=field;fieldHost.Child=field;
        var resultsScroll=UI.Scroll(settingsResults);resultsScroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;
        searchResultsHost=new Border{Child=resultsScroll,Background=SettingsTheme.CardRaised,BorderBrush=SettingsTheme.Line,BorderThickness=new(1),CornerRadius=new(14),Padding=new(8),Margin=new(36,48,36,10),VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=28,ShadowDepth=8,Opacity=.5,Color=Colors.Black}};UI.LocalizedName(searchResultsHost,"Результаты поиска");Grid.SetRow(searchResultsHost,0);Panel.SetZIndex(searchResultsHost,40);main.Children.Add(searchResultsHost);
        workspace.SizeChanged+=(_,_)=>resultsScroll.MaxHeight=Math.Max(80,Math.Min(430,workspace.ActualHeight-28));
        settingsSearch.TextChanged+=(_,_)=>{bool empty=string.IsNullOrWhiteSpace(settingsSearch.Text);hint.Visibility=empty?Visibility.Visible:Visibility.Collapsed;clear.Visibility=empty?Visibility.Collapsed:Visibility.Visible;ShowSettingsSearch();};
        settingsSearch.GotKeyboardFocus+=(_,_)=>ShowSettingsSearch();
        settingsSearch.PreviewKeyDown+=(_,e)=>
        {
            if(e.Key==Key.Escape){settingsSearch.Clear();e.Handled=true;}
            else if(e.Key is Key.Down or Key.Enter && settingsResults.Children.OfType<Button>().FirstOrDefault() is {} first){if(e.Key==Key.Down)first.Focus();else first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}
        };
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.F && Keyboard.Modifiers==ModifierKeys.Control){settingsSearch.Focus();settingsSearch.SelectAll();e.Handled=true;}else if(e.Key==Key.Escape && searchResultsHost.Visibility==Visibility.Visible){settingsSearch.Clear();settingsSearch.Focus();e.Handled=true;}};
        PreviewMouseDown+=(_,e)=>{if(searchResultsHost.Visibility!=Visibility.Visible)return;if(e.OriginalSource is DependencyObject source && !Inside(source,field) && !Inside(source,searchResultsHost))searchResultsHost.Visibility=Visibility.Collapsed;};
    }
    private static bool Inside(DependencyObject source,DependencyObject ancestor)
    {
        for(DependencyObject? node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))if(node==ancestor)return true;
        return false;
    }
    private void ShowSettingsSearch()
    {
        if(settingsSearch==null || searchResultsHost==null)return;
        string query=settingsSearch.Text.Trim();
        if(query.Length<1){searchResultsHost.Visibility=Visibility.Collapsed;settingsResults.Children.Clear();return;}
        var terms=query.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var matches=settingsSearchIndex.Select(item=>
        {
            int score=0;string haystack=(item.Title+" "+item.Section+" "+item.Keywords+" "+item.Control).ToLowerInvariant();
            foreach(var term in terms){string needle=term.ToLowerInvariant();if(item.Title.Contains(needle,StringComparison.OrdinalIgnoreCase))score+=12;else if(haystack.Contains(needle))score+=3;}
            return(Item:item,Score:score);
        }).Where(m=>m.Score>0).OrderByDescending(m=>m.Score).Take(9).Select(m=>m.Item).ToArray();
        settingsResults.Children.Clear();
        if(matches.Length==0)
        {
            var empty=UI.Caption("Ничего не найдено",12,UI.Muted);empty.Margin=new(12,8,12,8);settingsResults.Children.Add(empty);searchResultsHost.Visibility=Visibility.Visible;return;
        }
        foreach(var item in matches)
        {
            var result=item;var button=UI.Button("Открыть "+result.Title,()=>OpenSettingsSearchResult(result));button.Background=Brushes.Transparent;button.BorderThickness=new(0);button.Padding=new(12,9,12,9);button.Margin=new(0,1,0,1);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            var labels=new StackPanel();labels.Children.Add(UI.Caption(result.Title,12));var path=UI.Text(CategoryTitle(result.Category)+(result.Section.Length>0?"  /  "+FriendlySection(result.Section):""),10,UI.Muted);path.Margin=new(0,3,0,0);labels.Children.Add(path);button.Content=labels;settingsResults.Children.Add(button);
        }
        searchResultsHost.Visibility=Visibility.Visible;
    }
    private void OpenSettingsSearchResult(SettingsSearchItem item)
    {
        settingsSearch?.Clear();revealAllNext=!app.Settings.SettingsShowAll && !EssentialSetting(item.Category,item.Section);if(revealAllNext && item.Category==current)revealAllSettings=false;Navigate(item.Category);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {
            if(current!=item.Category)return;
            var section=SettingTree(page).OfType<Border>().FirstOrDefault(b=>b.Tag as string==item.Section);
            var target=SettingTree((DependencyObject?)section??page).OfType<Control>().FirstOrDefault(c=>item.Control.Length>0 && UI.Identity(c)==item.Control);
            if(target!=null)
            {
                for(DependencyObject? node=target;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))if(node is Expander expander)expander.IsExpanded=true;
                foreach(var expander in SettingTree((DependencyObject?)section??page).OfType<Expander>().Where(x=>SettingTree(x).Contains(target)))expander.IsExpanded=true;
                page.UpdateLayout();target.Focus();target.BringIntoView();SetSaveFeedback("Открыто · "+item.Title);
            }
            else if(section!=null){foreach(var parent in SettingTree(page).OfType<Expander>().Where(e=>SettingTree(e).Contains(section)))parent.IsExpanded=true;page.UpdateLayout();section.BringIntoView();ConfigurePreviewForSection(item.Section);RefreshPreview();}
        });
    }
}
