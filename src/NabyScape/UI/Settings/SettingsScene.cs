using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace NabyScape;

// Static, local examples of the controls in this section. Repaint values without rebuilding editors.
internal sealed class SettingsScene : FrameworkElement
{
    internal static readonly string[] Sections={"Other apps","Feature usage","Your setup","Ambient light","Notification presentation","Visualizer","Lyrics","Move & resize","Completion sound","Shape","Settings material","Color & material","Surface","Controls & outlines","Typography","Text and navigation","Motion","Animation performance","Dock placement","Display","Closed island","Open panel","Hover preview","Hover & swipe","Enabled widgets & order","Individual tools","Hover pages","Clipboard history","Focus sessions","Stopwatch","Automatic tucking","Manual tuck shortcut","Panel navigation","Drag to Island","Detachable previews","Closed cover gestures","Automatic activity","Notch gestures","Global hotkey","Hide or show the island","Closed media display","Waveform source","Live signal","Live feedback","Volume mixer","Audio behavior","Header wheel","Clock","Monitor details","Launch display","Shortcuts","Layout profile","Notifications","Downloads","Updates","Reset preferences","Behavior","Local storage","Language & accessibility","Private diagnostics","About NabyScape"};
    private readonly string kind;
    internal string Section=>kind;
    private readonly Settings s;
    internal SettingsScene(string kind,Settings settings){if(!Sections.Contains(kind))throw new ArgumentException("Unknown Settings example: "+kind);this.kind=kind;s=settings;ClipToBounds=true;IsHitTestVisible=false;}
    internal sealed record WidgetTile(Rect Bounds,FormattedText Label);
    internal static WidgetTile[] WidgetTiles(Settings settings,double pixelsPerDip=1)
    {
        var labels=settings.Widgets.Where(widget=>widget.Enabled).Select(widget=>new FormattedText(Settings.Names[widget.Id],CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,UI.Brush("#F3F4F6"),pixelsPerDip){MaxTextWidth=60,Trimming=TextTrimming.None}).ToArray();
        var result=new WidgetTile[labels.Length];double y=15;
        for(int row=0;row<labels.Length;row+=5)
        {
            double height=Math.Max(28,labels.Skip(row).Take(5).Max(label=>label.Height)+12);
            for(int column=0;column<5 && row+column<labels.Length;column++)result[row+column]=new(new Rect(16+column*79,y,72,height),labels[row+column]);
            y+=height+7;
        }
        return result;
    }
    protected override Size MeasureOverride(Size available)
    {
        double height=126;
        if(kind=="Enabled widgets & order"){var tiles=WidgetTiles(s,VisualTreeHelper.GetDpi(this).PixelsPerDip);height=Math.Max(height,(tiles.LastOrDefault()?.Bounds.Bottom??15)+44);}
        double width=double.IsInfinity(available.Width)?420:available.Width;
        return new(width,height*Math.Min(1,width/420));
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<=0)return;
        double scale=Math.Min(1,ActualWidth/420);dc.PushTransform(new ScaleTransform(scale,scale));
        var white=UI.Brush("#F2F3F5");var muted=UI.Brush("#ADB3BD");var fill=UI.Brush("#24272D");var line=new Pen(UI.Brush("#626874"),1);var edge=new Pen(UI.Brush("#16FFFFFF"),.75);edge.Freeze();
        void Box(double x,double y,double w,double h,Brush? brush=null,double r=8)=>dc.DrawRoundedRectangle(brush??fill,w>=28 && h>=22?edge:null,new Rect(x,y,w,h),r,r);
        void Text(string value,double x,double y,double size=12,bool bold=false,Brush? brush=null,double maxWidth=400)
        {var t=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush??white,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=Math.Max(10,Math.Min(maxWidth,405-x)),MaxTextHeight=42,Trimming=TextTrimming.CharacterEllipsis};dc.DrawText(t,new Point(x,y));}
        void Line(double x,double y,double xx,double yy)=>dc.DrawLine(line,new Point(x,y),new Point(xx,yy));
        void Toggle(double x,double y,bool on){Box(x,y,30,16,on?white:fill,8);dc.DrawEllipse(on?fill:muted,null,new Point(x+(on?22:8),y+8),5,5);}
        void Bar(double x,double y,double w,double value){Box(x,y,w,3,fill,1);Box(x,y,Math.Max(1,w*Math.Clamp(value,0,1)),3,white,1);}
        void Keys(string key,double x,double y,double w=90){Box(x,y,w,30);Text(key,x+10,y+7,11);}
        void Silhouette(double x,double y,double w,double h,bool notch)
        {
            var geometry=IslandGeometry.Create(w,h,s.Radius,notch);dc.PushTransform(new TranslateTransform(x,y));
            dc.DrawGeometry(UI.Brush(s.Background),edge,geometry);
            if(s.GlassStyle){dc.PushOpacity(s.GlassStrength*.5);dc.DrawGeometry(IslandSurface.GlassMaterial,null,geometry);dc.Pop();}
            dc.Pop();
        }
        void Island(double x=105,double y=20,double w=210,double h=34){Silhouette(x,y,w,h,s.Preset=="Mac");Text("Музыка",x+20,y+9,11,true);Text("12:45",x+w-54,y+9,11,true);}
        void Wave(double x,double y){for(int i=0;i<25;i++){double h=4+15*Math.Pow(Math.Sin(i*.37+1),2)*(.7+.3*Math.Cos(i*.13));Box(x+i*3.6,y-h/2,1.5,h,white,1);}}
        void Cover(double x,double y,double size)
        {
            if(System.Windows.Application.Current is App app && app.Media?.Artwork is ImageSource artwork){dc.PushClip(new RectangleGeometry(new Rect(x,y,size,size),5,5));dc.DrawImage(artwork,new Rect(x,y,size,size));dc.Pop();}
            else {Box(x,y,size,size,UI.Brush("#737D8C"),5);dc.DrawEllipse(null,new Pen(white,1),new Point(x+size/2,y+size/2),size*.27,size*.27);}
        }
        void Note(string message)=>Text(message,14,99,11,false,muted);
        var widgetTiles=kind=="Enabled widgets & order"?WidgetTiles(s,VisualTreeHelper.GetDpi(this).PixelsPerDip):Array.Empty<WidgetTile>();
        double widgetBottom=widgetTiles.LastOrDefault()?.Bounds.Bottom??15;
        Box(0,0,420,kind=="Enabled widgets & order"?Math.Max(124,widgetBottom+42):124,UI.Brush("#191C21"),10);
        switch(kind)
        {
            case "Feature usage":
                Text("Выбор активных функций",24,18,18,true);Text(s.Widgets.Count(w=>w.Enabled)+" активных виджетов",24,51,12);Note("Только выбранные виджеты работают в фоне");break;
            case "Your setup":
                Text("Ваш следующий шаг",24,18,18,true);Text("Музыка · Текст песен · Настройки",24,51,12);Note("Сохранённые настройки применяются сразу");break;
            case "Shape":
                Silhouette(28,22,160,36,true);Silhouette(228,22,160,36,false);Text("Вырез · у кромки",30,69,11);Text("Плашка · парящая",230,69,11);Note("Текущий стиль: "+(s.Preset=="Mac"?"Вырез":"Плашка"));break;
            case "Settings material":
                Box(22,18,168,65,IslandSurface.GlassMaterial);Text("Окно настроек",34,31,13,true);Text(SettingsBackdrop.Requested(s),34,55,11);Text("Островок независим",210,32,11);Note("Оформление и фон окна настроек");break;
            case "Color & material":
                Box(22,19,216,68,UI.Brush(s.Background));Box(36,33,34,34,UI.Brush(s.Accent));Text("Название трека",82,34,14,true,UI.ContrastText(s.Background));Text(s.GlassStyle?"Тёмное стекло":"Сплошной цвет",82,57,11,false,UI.ContrastText(s.Background));Text($"Непрозрачность {s.Opacity*100:0}%",256,28);Text($"Стекло {s.GlassStrength*100:0}%",256,52);Note("Цвет и материал островка");break;
            case "Surface":
                var glass=new LinearGradientBrush(UI.Brush(s.Background).Color,UI.Brush("#3E424A").Color,90){Opacity=.25+s.GlassStrength*.6};
                Box(24,18,176,61,Brushes.Black);Box(224,18,176,61,glass);Text("Сплошной",39,38,14,true);Text("Стекло",240,38,14,true);Note($"Интенсивность стекла {s.GlassStrength*100:0}% · стиль стекла {(s.GlassStyle?"вкл":"выкл")}");break;
            case "Controls & outlines":
                Box(18,24,112,34,UI.Brush(s.ControlFill));Text("Кнопка",36,33,12,false,UI.ContrastText(s.ControlFill));dc.DrawRoundedRectangle(null,new Pen(UI.Brush(s.ControlBorder),1),new Rect(146,24,122,34),6,6);Text("Поле ввода",158,33);Box(284,24,116,34,UI.Brush(s.SelectionFill));Text("Выбранная вкладка",296,33,12,false,UI.ContrastText(s.SelectionFill));Note("Заливка кнопок · рамки полей · активное состояние");break;
            case "Typography":
                Text("Название трека",24,23,21,s.BoldMediaTitle);Text("Исполнитель",24,54,12,false,muted);Text("Заголовок панели",232,27,16,s.BoldHeadings);Text("Основной текст обычный",232,56,11,false,muted);Note("Начертание заголовков и текста");break;
            case "Text and navigation":
                foreach(var item in new[]{("Музыка",22d),("Фокус",112d),("Статус",202d),("Инструменты",292d)}){Box(item.Item2,22,76,46);if(s.CategoryStyle=="Dots"){dc.DrawEllipse(white,null,new Point(item.Item2+38,44),3,3);continue;}if(s.CategoryStyle!="Text")dc.DrawEllipse(null,new Pen(white,1.4),new Point(item.Item2+38,36),6,6);if(s.CategoryStyle!="Icons")Text(item.Item1,item.Item2+14,s.CategoryStyle=="Text"?38:49,11);}Note("Навигация · "+s.CategoryStyle.Replace("IconsLabels","Иконки + текст"));break;
            case "Motion":
                Box(23,28,68,22,Brushes.Black);Text("Музыка",34,33,9);Line(108,40,176,40);Box(193,16,135,63,Brushes.Black);Cover(203,25,22);Text("Название трека",234,28,10,true);Bar(204,65,112,.4);Text(s.ReduceMotion?"Без анимации":$"{s.AnimationMs:0} мс переход",23,78,11);Text("Закрыт → открыт",193,87,11,false,muted);break;
            case "Animation performance":
                Text("Эко",24,16,12,true);Text("Авто",24,42,12,true);Text("Максимум",24,68,12,true);for(int row=0;row<3;row++)for(int i=0;i<(row+1)*8;i++)Box(120+i*10,20+row*26,3,12,white,1);Note("Частота обновления кадров анимации");break;
            case "Dock placement":
                Box(110,12,205,76,UI.Brush("#14171D"),7);dc.DrawRoundedRectangle(null,line,new Rect(110,12,205,76),7,7);
                var marker=s.DockEdge switch{"Left"=>new Rect(111,36,10,30),"Right"=>new Rect(304,36,10,30),"Bottom"=>new Rect(182,77,62,9),"Floating"=>new Rect(182,45,62,12),_=>new Rect(182,14,62,9)};
                dc.DrawRoundedRectangle(white,null,marker,4,4);Note(s.DockEdge+" · "+s.DockInset.ToString("0")+" px отступ");break;
            case "Display":
                dc.DrawRoundedRectangle(null,line,new Rect(24,17,166,67),5,5);dc.DrawRoundedRectangle(null,line,new Rect(220,17,166,67),5,5);Box(s.DisplayMode is "Follow" or "Selected"?266:70,18,76,12,white,3);if(s.DisplayMode=="Follow"){Line(318,39,332,48);Line(318,39,321,54);Text("250 мс",242,39,10);}
                Text("Основной экран",50,66,11);Text("Другой экран",250,66,11);Note(s.DisplayMode=="Follow"?"Следование за курсором · пауза при взаимодействии":s.DisplayMode=="Selected"?"Выбранный экран: "+s.Monitor:"Основной экран");break;
            case "Closed island":
                Island(90,18,240,34);Line(90,69,330,69);Line(90,63,90,75);Line(330,63,330,75);Text($"{s.Width:0} × {s.Height:0} px · скругление {s.Radius:0} px",115,77,11);Note($"Положение {s.Horizontal:0}% · смещение {s.Top:0} px");break;
            case "Open panel":
                Box(24,12,210,76,Brushes.Black);Box(37,23,35,14);Text("Музыка",86,25,10);Text("Название трека",42,48,13,true);Bar(42,73,172,.4);Text($"{s.PanelWidth:0} px ширина",253,24);Text($"Отступ {s.PanelPadding:0} px",253,47,11);Text($"Масштаб {s.PanelScale*100:0}%",253,67,11);Note($"Макс. высота {s.PanelMaxHeight:0} px · интервал {s.SectionGap:0} px");break;
            case "Hover preview":case "Hover & swipe":
                Island(20,18,126,27);Line(159,37,199,37);Box(212,12,186,73,Brushes.Black);Text("Название трека",226,24,13,true);Bar(226,61,156,.35);Text("Наведение",45,63,11);Note(kind=="Hover & swipe"?$"Превью через {s.HoverDelayMs} мс · подробности через {s.HoverDetailDelayMs} мс":$"Превью {s.HoverWidth:0} × {s.HoverHeight:0} px");break;
            case "Enabled widgets & order":
                foreach(var tile in widgetTiles){var bounds=tile.Bounds;Box(bounds.X,bounds.Y,bounds.Width,bounds.Height);dc.DrawText(tile.Label,new Point(bounds.X+6,bounds.Y+6));}Text("Видимые панели в выбранном порядке",14,Math.Max(99,widgetBottom+12),11,false,muted);break;
            case "Individual tools":
                int ti=0;foreach(var item in s.Tools.Take(7)){double x=16+ti%4*100,y=10+ti/4*40;Text(Settings.ToolNames[item.Id],x,y,10,false,item.Enabled?white:muted,92);Toggle(x,y+18,item.Enabled);ti++;}Note("Отключенные инструменты скрываются; их данные сохраняются");break;
            case "Hover pages":
                var available=HoverPagePolicy.Pages(s);var enabled=available.Take(4).ToArray();for(int i=0;i<enabled.Length;i++)
                {
                    double x=18+i*99;string id=enabled[i];Box(x,12,88,72,Brushes.Black);
                    Text(id switch{"media"=>"Музыка","files"=>"Файлы","notes"=>"Заметки","clips"=>"Буфер",_=>CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id)},x+8,20,10,true,null,72);
                    switch(id)
                    {
                        case "media":Cover(x+8,41,22);Bar(x+36,44,42,.7);Bar(x+36,53,32,.5);Bar(x+8,72,71,.35);break;
                        case "timer":Text("05:00",x+10,40,18,true);Bar(x+10,69,67,.45);break;
                        case "focus":dc.DrawEllipse(null,new Pen(muted,2),new Point(x+21,53),10,10);Text("25:00",x+37,47,11,true,null,45);Text("Сессия фокуса",x+9,69,9);break;
                        case "calendar":Text("16",x+11,39,21,true);Text("ОКТ",x+43,48,10);Bar(x+10,73,66,.6);break;
                        case "tasks":for(int j=0;j<3;j++){dc.DrawRectangle(null,line,new Rect(x+10,42+j*12,6,6));Bar(x+23,45+j*12,50,.7);}break;
                        case "files":for(int j=0;j<3;j++)Box(x+11+j*11,49-j*4,28,25,UI.Brush(j==2?"#737D8C":"#353B45"),3);break;
                        case "system":Text("ЦП   ОЗУ",x+9,42,9);Bar(x+9,59,69,.3);Bar(x+9,70,69,.6);break;
                        default:Bar(x+10,45,65,.9);Bar(x+10,56,65,.6);Bar(x+10,67,65,.75);break;
                    }
                }Note($"{available.Length} доступных страниц · первые {enabled.Length} показаны в превью");break;
            case "Clipboard history":
                Text("Текст",22,20,12,true);Text("Скопированная мысль",22,42,11,false,muted);Text("Ссылка",158,20,12,true);Text("example.com",158,42,11,false,muted);Box(296,18,40,40);dc.DrawEllipse(muted,null,new Point(324,28),3,3);Line(301,50,312,35);Line(312,35,331,52);Text("Картинка",346,30,11);Toggle(22,74,s.ClipboardHistory);Text(s.ClipboardHistory?"История включена":"История выключена",64,74,11);Note("При отключении истории сохранённые элементы очищаются");break;
            case "Stopwatch":
                Text("Секундомер",28,13,12,true);Text("00:00",28,33,28,true);Keys("Старт",205,29,78);Keys("Сброс",292,29,78);Note("Старт → Пауза → Продолжить · Сброс обнуляет время");break;
            case "Focus sessions":
                Text(s.FocusMinutes+":00",28,20,28,true);Text("Фокус",30,59,12,false,muted);Line(170,25,170,77);Text(s.BreakMinutes+":00",224,20,28,true);Text("Перерыв",226,59,12,false,muted);Note("Применяется при запуске следующей сессии");break;
            case "Automatic tucking":
                Box(20,18,112,35,Brushes.Black);Text("Видим",45,66,11);Box(154,18,112,s.TuckEdge,Brushes.Black,2);Text($"Прижат · {s.TuckEdge:0} DIP",164,66,11);dc.DrawRoundedRectangle(null,line,new Rect(288,18,112,35),8,8);Text("Скрыт",320,66,11);Note(s.AlwaysTucked?"Всегда прижат · наведите на кромку для показа":"Автоскрытие при работе в других программах");break;
            case "Manual tuck shortcut":
                Keys(s.TuckHotkeyModifiers,24,24,130);Keys(s.TuckHotkeyKey,166,24,40);Text("Прижатие / авто",232,30,13,true);Note("Первое нажатие прижимает · второе возвращает автоскрытие");break;
            case "Panel navigation":
                Box(20,15,146,68,Brushes.Black);Text("Музыка",35,28,14,true);Line(182,48,234,48);Box(252,15,146,68,Brushes.Black);Text("Статус",268,28,14,true);Note("Свайп по пустому месту · Shift + колесико · Ctrl + Tab");break;
            case "Drag to Island":
                for(int i=0;i<3;i++){Box(28+i*12,34-i*8,42,44,UI.Brush(i==2?"#737D8C":"#3C424C"),4);}Line(110,48,198,48);Box(218,19,180,64,Brushes.Black);Text("Перетащите файлы сюда",239,32,14,true);Text("Оригиналы остаются на месте",230,59,11);Note(s.DropReveal?"Перетаскивание открывает область сброса":"Область сброса отключена");break;
            case "Detachable previews":
                Box(18,15,151,68,Brushes.Black);Bar(79,24,30,1);Text("Превью островка",33,49,12);Line(185,48,224,61);Box(244,31,151,57,Brushes.Black);Text("Мини-окно",260,49,12,true);Note("Перетащите за ручку для перемещения");break;
            case "Closed cover gestures":
                Cover(22,22,38);Text("Клик",80,21,13,true);Text("Плей / пауза",80,43,11);Text("Удержание 500 мс",240,21,13,true);Text("Выбор плеера",240,43,11);Note(s.CoverGestures?"Включено, когда иконка использует обложку альбома":"Жесты по обложке отключены");break;
            case "Automatic activity":
                Text("Смена трека / завершение сессии",24,20,14,true);Island(26,51,225,30);Text("Краткое превью",274,57,11);Note(s.SmartActivities?"Появляется без перехвата фокуса":"Автоматические превью отключены");break;
            case "Notch gestures":
                Text("Колесико мыши",24,22,13,true);Text(s.WheelAction switch{"volume"=>"Громкость","pages"=>"Переключение вкладок",_=>"Выключено"},24,49,14);Text("Средняя кнопка",232,22,13,true);Text(s.MiddleAction switch{"toggle"=>"Плей / пауза",_=>"Выключено"},232,49,14);Note("Превью при наведении листаются колесиком");break;
            case "Global hotkey":case "Hide or show the island":
                bool hide=kind=="Hide or show the island";Keys(hide?s.VisibilityHotkeyModifiers:s.HotkeyModifiers,24,24,130);Keys(hide?s.VisibilityHotkeyKey:s.HotkeyKey,166,24,40);Text(hide?"Скрыть / показать":"Открыть / закрыть панель",232,30,13,true);Note("Работает поверх других программ");break;
            case "Closed media display":
                Silhouette(32,22,352,40,s.Preset=="Mac");if(s.ClosedMediaIcon=="Artwork")Cover(46,31,22);else Wave(46,42);if(s.ClosedMediaContent=="Waveform")Wave(158,42);else Text("Название трека",160,34,13,true);Text("12:45",334,36,11,true);Note("Текущий вид закрытого островка при воспроизведении");break;
            case "Visualizer":case "Waveform source":case "Live signal":
                Text(s.WaveformSource=="Spectrum"?"Бас → средние → высокие":"История амплитуды",22,18,13,true);Wave(125,62);Text(s.WaveformSensitivity.ToString("0.##")+"× чувствительность",270,38,12);Note(s.WaveformSource=="Spectrum"?"Частотные полосы активного плеера":"Измерение амплитуды звука");break;
            case "Lyrics":
                Text("Ближе к музыке",24,17,11,false,muted);Text("Строка синхронно с песней",24,42,22,true);Text("Слайд     Фокус     Караоке",24,77,11,false,muted);Toggle(355,20,s.LyricsEnabled);Note(s.LyricsAnimation+" · Синхронизированный текст через LRCLIB");break;
            case "Move & resize":
                Island(95,22,220,36);Line(88,18,66,18);Line(66,18,66,83);Line(66,83,88,83);Text("↔",180,70,20);Toggle(352,28,s.DockUnlocked);Note("Перетащите за верхний край · углы меняют размер");break;
            case "Completion sound":
                Text(s.CountdownSound=="Off"?"Без звука":"♪",25,18,28,true);Wave(119,50);Text(s.CountdownSound,264,25,16,true);Text((s.CountdownSoundVolume*100).ToString("0")+"%",264,53,12,false,muted);Note("Таймер и фокус · слышно без системных уведомлений Windows");break;
            case "Live feedback":
                Island(24,23,208,34);Bar(54,47,146,.66);Text("Громкость / яркость",252,27,12);Text("Всплывающая индикация",252,50,11,false,muted);Note($"Громкость {(s.VolumeFeedback?"вкл":"выкл")} · яркость {(s.BrightnessFeedback?"вкл":"выкл")}");break;
            case "Volume mixer":case "Audio behavior":
                Text("Программы",24,18,13,true);Text("Вывод",246,18,13,true);Text("Плеер",24,46,11);Text("По умолчанию Windows",246,46,11);Text("Громкость и без звука",24,75,11,false,muted);Note("Откройте микшер для точной настройки громкости");break;
            case "Header wheel":
                Text("Один шаг колесика",24,22,15,true);Text($"+{s.VolumeStep}%",264,20,26,true);Bar(24,73,365,.6);Note("Изменение громкости за одно деление колесика");break;
            case "Clock":
                Text(s.Clock24Hour?"14:35"+(s.ClockSeconds?":08":""):"2:35"+(s.ClockSeconds?":08":"")+" PM",26,18,30,true);Text(s.ShowDate?"Пятница, 16 октября":"Дата скрыта",27,60,12,false,muted);Note("Формат времени · секунды видны в статусе");break;
            case "Monitor details":
                Text("Процессор · Память",24,20,14,true);Text("Всегда в обзоре",24,47,11,false,muted);Text("Сеть      "+(s.MonitorNetwork?"Вкл":"Выкл"),244,17,11);Text("Диск      "+(s.MonitorStorage?"Вкл":"Выкл"),244,39,11);Text("Процессы  "+(s.MonitorProcesses?"Вкл":"Выкл"),244,61,11);Note("Подробная статистика в системном мониторе");break;
            case "Launch display":
                Text("Ярлыки",24,18,16,true);Text("Текущий стиль: "+s.LaunchStyle,24,50,13);Note("Стиль отображения сохранённых ярлыков");break;
            case "Shortcuts":
                Text("Ярлыки",24,18,15,true);Text(s.Shortcuts.Count+" сохранённых ярлыков",250,22,11,false,muted);int si=0;foreach(var item in s.Shortcuts.Take(3)){Box(24+si*128,49,116,29);Text(item.Name,32+si++*128,57,11,false,null,100);}if(si==0)Text("Добавьте программу, папку или сайт ниже",24,56,12);Note("Только добавленные ярлыки отображаются в панели");break;
            case "Layout profile":
                Text("Внешний вид + разметка",24,22,16,true);Text("Порядок виджетов + жесты",24,50,12);Keys("Экспорт / Импорт",250,27,143);Note("Не включает заметки, буфер обмена и автозапуск");break;
            case "Ambient light":
                Box(36,12,348,82,Brushes.Black,16);dc.PushClip(new RectangleGeometry(new Rect(36,12,348,82),16,16));
                var ambientPalette=(System.Windows.Application.Current as App)?.AmbientAudio?.CoverColors??default;
                var ambientColors=AmbientPolicy.ColorsFor(s,ambientPalette);
                if(s.CoverAmbient){var light=new DrawingGroup{OpacityMask=AmbientPaint.BottomFade(s.AmbientSpread,AmbientLook.From(s))};light.Children.Add(new GeometryDrawing(AmbientPaint.ColorBand(ambientColors,s.AmbientStrength,AmbientLook.From(s)),null,new RectangleGeometry(new Rect(36,12,348,82))));dc.DrawDrawing(light);}dc.Pop();Cover(51,28,36);Text(s.AmbientScope,102,30,15,true);Text(s.AmbientAudioResponse+" · "+(int)(s.AmbientStrength*100)+"%",102,56,11);Note(s.AmbientColorSource+" · "+s.AmbientMotion+" · реакция на звук");break;
            case "Notification presentation":
                Box(46,10,328,77,Brushes.Black,16);Text("Фокус завершён",68,25,16,true);Text("Время для короткого перерыва",68,52,11,false,muted);Text("2 в очереди",286,28,10,false,muted);Bar(68,73,280,.65);Note(s.NotificationAnimation+" · "+s.NotificationSeconds+" сек · "+(s.QuietHours?"тихие часы вкл":"тихие часы выкл"));break;
            case "Notifications":
                Box(36,18,215,34,Brushes.Black,17);Text("Таймер завершён",53,29,11,true);Keys(s.NotificationDelivery,274,20,110);Note("Островок / Windows / Оба / Выкл");break;
            case "Other apps":
                Box(24,18,42,47);Text("C",38,26,20,true);Text("Claude",83,22,14,true);Text("Ответ готов",83,49,11,false,muted);Toggle(350,25,s.ExternalNotifications);Note("Оповещения от сторонних программ Windows");break;
            case "Downloads":
                Box(24,18,42,47);Text("↓",36,25,21);Text("Report.pdf",83,22,14,true);Text("2 файла загружено",83,49,11,false,muted);Toggle(350,25,s.DownloadsEnabled);Note("Оповещения о завершённых загрузках");break;
            case "Updates":
                Text("Установлена версия "+OfficialUpdates.InstalledVersion,22,15,13,true);Text("Проверка обновлений",225,15,12,true);Bar(24,55,365,.6);Text("Проверка > загрузка > проверка > установка",24,70,12);Note("Автопроверка "+(s.AutoCheckUpdates?"вкл":"выкл")+" · без автоустановки");break;
            case "Reset preferences":
                Keys("Ваши настройки",20,24,128);Line(158,39,196,39);Keys("Резервная копия",208,24,120);Text("Сброс выбранной категории",22,71,12,true);Note("Заметки, задачи и автозапуск сохраняются");break;
            case "Behavior":
                Text("Вход в Windows",24,23,15,true);Line(163,41,217,41);Text("NabyScape",240,23,15,true);Toggle(244,60,s.AutoStart);Text(s.AutoStart?"Запускать автоматически":"Запуск вручную",24,68,12,false,muted);break;
            case "Local storage":
                Text("Локально на этом ПК",24,21,17,true);Text("Настройки · Заметки · Задачи",24,51,12);Text("Учётная запись не требуется",24,77,11,false,muted);break;
            case "About NabyScape":
                Text("NabyScape",24,20,23,true);Text("Windows 11 · x64",24,60,12,false,muted);Text("Версия "+typeof(App).Assembly.GetName().Version!.ToString(3),250,31,12);break;
            case "Language & accessibility":
                Text("Язык",24,18,12,true);Keys(s.UiLanguage=="es"?"Español":s.UiLanguage=="id"?"Bahasa Indonesia":"Русский",210,12,176);
                Text("Размер текста",24,55,12,true);Text((s.AccessibilityTextScale*100).ToString("0",CultureInfo.InvariantCulture)+"%",218,55,12);Toggle(350,55,s.AccessibilityHighContrast);Note("Язык · размер текста · контраст");break;
            case "Private diagnostics":
                Text("Проверьте перед отправкой",24,18,16,true);Text("Версия · статус служб · события",24,48,11,false,muted);Keys("Копировать",24,73,95);Keys("Экспорт",133,73,95);Note("Локальные диагностические логи");break;
            default:throw new InvalidOperationException("Missing Settings example: "+kind);
        }
        dc.Pop();
    }
}
