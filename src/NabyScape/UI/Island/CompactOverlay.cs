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

public sealed partial class OverlayWindow
{
    private bool peeking;
    private string activePage="tasks";
    private readonly Dictionary<string,FrameworkElement> compactPages=new();
    private readonly Dictionary<string,Button> compactTabs=new();
    private readonly Grid compactPageHost=new(),peekPanel=new(),timelinePanel=new();
    private readonly TextBlock peekTitle=UI.Caption("",13),peekSubtitle=UI.Caption("",11,UI.Muted);
    private readonly Image peekArt=new(){Width=56,Height=56,Stretch=Stretch.UniformToFill};
    private readonly ProgressBar peekProgress=new(){Minimum=0,Maximum=1,Height=3,Background=UI.Brush("#34363D"),Foreground=Brushes.White,BorderThickness=new(0)};
    private readonly TextBlock peekElapsed=UI.Caption("",11,UI.Muted),peekDuration=UI.Caption("",11,UI.Muted);
    private readonly TranslateTransform peekArtSlide=new();
    private readonly System.Windows.Controls.Grid peekCoverContent=new();
    private FrameworkElement peekFallback=null!;
    private readonly ActivityVisual peekWave=new(){Width=58,Height=16,WaveOnly=true};
    private Button? peekPlay;
    private readonly Slider seekSlider=new(){Minimum=0,Maximum=1,TickFrequency=.001,SmallChange=.01,LargeChange=.05,IsMoveToPointEnabled=true,Height=24,Margin=new(0)};
    private readonly TextBlock elapsed=UI.Caption("0:00",10,UI.Muted),duration=UI.Caption("0:00",10,UI.Muted);
    private readonly DispatcherTimer hoverDelay=new(){Interval=TimeSpan.FromMilliseconds(180)},leaveDelay=new(){Interval=TimeSpan.FromMilliseconds(300)},seekDelay=new(){Interval=TimeSpan.FromMilliseconds(140)};
    private double HostHeight
    {
        get
        {
            if(assistantOpen || VoiceRoomOpen)return AssistantView.PanelSize(app.Settings,VoiceRoomOpen).Height;
            if(!app.Settings.CompactLayout)return expandedHeight;
            var screen=Native.ChooseScreen(app.Settings);return Math.Min(app.Settings.PanelMaxHeight,screen.WorkingArea.Height/Native.Scale(screen)-16);
        }
    }
    internal bool IsPeeking=>peeking;
    internal string ActivePage=>activePage;
    private static readonly string[] OrderedSections = { "tasks", "notes", "media", "reset", "timer", "focus", "system" };
    private static string PageFor(string widget)=>widget switch{
        "tasks"=>"tasks",
        "notes"=>"notes",
        "media"=>"media",
        "reset"=>"reset",
        "timer"=>"timer",
        "focus"=>"focus",
        "system"=>"system",
        _=>"tasks"
    };
    private static string PageName(string page)=>page switch{
        "tasks"=>"Задачи",
        "notes"=>"Заметки",
        "media"=>"Плеер",
        "reset"=>"Daily Reset",
        "timer"=>"Таймер",
        "focus"=>"Помодоро / Фокус",
        "system"=>"Система",
        _=>Localization.Text(page)
    };
    private string? hoverPanelWidget;
    private bool WidgetOn(string id)=>id==hoverPanelWidget || app.Settings.Widgets.Any(w=>w.Enabled && w.Id==id);
    private IEnumerable<WidgetSetting> PanelWidgets=>app.Settings.Widgets.Where(w=>w.Enabled || w.Id==hoverPanelWidget);
    private void OpenHoverPanel(string widget,bool activate=true)
    {
        LeaveAssistantPanel();
        string page=PageFor(widget);
        bool hidden=!app.Settings.Widgets.Any(w=>w.Enabled && w.Id==widget) || !app.Settings.CompactLayout && Settings.ToolNames.ContainsKey(widget);
        bool leavingMixer=classicMixerOpen;classicMixerOpen=false;
        if(leavingMixer || hoverPanelWidget!=(hidden?widget:null))
        {FlushNotes();hoverPanelWidget=hidden?widget:null;activePage=page;BuildBody();}
        Reveal(activate);
        if(app.Settings.CompactLayout)SelectPage(page,false);
        scroll.ScrollToTop();UpdateSystemMonitor();
    }
    private void InitializeCompactInteraction()
    {
        ContentRendered+=(_,_)=>Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
        {
            // Prepare only the lightweight preview shell after startup has painted.
            // Editors and other widget pages remain lazy. Pointer entry starts its
            // delay immediately, without constructing controls on the input path.
            if(!DiagnosticRun && !app.Quitting && app.Settings.HoverPeek && app.Settings.CompactLayout)EnsurePeekDeck();
        }));
        root.MouseEnter+=(_,_)=>{if(DiagnosticRun)return;if(IsTucked){RevealTuckedPreview();return;}leaveDelay.Stop();if(peeking){automaticPreview=false;activityDismiss.Stop();}else{previewKind=activityTitle=activityDetail="";}};
        root.MouseLeave+=(_,_)=>{if(DiagnosticRun)return;hoverDelay.Stop();if(peeking || TuckRequested)leaveDelay.Start();};
        hoverDelay.Tick+=(_,_)=>{hoverDelay.Stop();};
        leaveDelay.Tick+=(_,_)=>
        {
            leaveDelay.Stop();if(!(peeking || TuckRequested) || AssistantReplyVisible || PointerInside() || fileDragActive)return;
            // A drop-down hanging outside the panel keeps it open; collapse once it closes with the pointer outside.
            if(OwnedPopupOpen){leaveDelay.Start();return;}
            if(!PeekInteractionActive)Collapse();
        };
        peekMedia.HorizontalAlignment=HorizontalAlignment.Center;peekMedia.VerticalAlignment=VerticalAlignment.Top;peekMedia.Width=286;peekMedia.Height=100;peekMedia.Margin=new(0);
        peekMedia.RowDefinitions.Add(new(){Height=new GridLength(56)});peekMedia.RowDefinitions.Add(new(){Height=new GridLength(18)});peekMedia.RowDefinitions.Add(new(){Height=new GridLength(18)});
        peekMedia.ColumnDefinitions.Add(new(){Width=GridLength.Auto});peekMedia.ColumnDefinitions.Add(new());peekMedia.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        peekArt.RenderTransform=peekArtSlide;
        peekCoverContent.Children.Add(WrapArtwork(peekArt));peekFallback=Icons.Create("activity",28);peekCoverContent.Children.Add(peekFallback);
        var cover=new Border{CornerRadius=new(14),Child=peekCoverContent,Background=UI.Brush("#16FFFFFF"),Width=56,Height=56,Margin=new(0,0,16,0)};
        cover.Clip=new RectangleGeometry(new Rect(0,0,56,56),14,14);peekMedia.Children.Add(cover);
        peekTitle.FontSize=15;peekSubtitle.FontSize=12;peekSubtitle.Margin=new(0,6,0,0);
        peekTitle.TextWrapping=TextWrapping.NoWrap;peekTitle.TextTrimming=TextTrimming.CharacterEllipsis;peekTitle.FontWeight=FontWeights.SemiBold;
        peekSubtitle.TextWrapping=TextWrapping.NoWrap;peekSubtitle.TextTrimming=TextTrimming.CharacterEllipsis;
        var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};info.Children.Add(WrapTitle(peekTitle));info.Children.Add(WrapTitle(peekSubtitle));Grid.SetColumn(info,1);peekMedia.Children.Add(info);
        peekPlay=Icons.Button("play","Play or pause from preview",()=>_=app.Media.Command("toggle"),true,38);peekPlay.Margin=new(16,0,0,0);Grid.SetColumn(peekPlay,2);peekMedia.Children.Add(peekPlay);
        Grid.SetRow(peekProgress,1);Grid.SetColumnSpan(peekProgress,3);peekProgress.Margin=new(0,12,0,0);peekMedia.Children.Add(peekProgress);
        var peekTimes=new DockPanel{Margin=new(0,6,0,0)};DockPanel.SetDock(peekDuration,Dock.Right);peekTimes.Children.Add(peekDuration);peekTimes.Children.Add(peekElapsed);Grid.SetRow(peekTimes,2);Grid.SetColumnSpan(peekTimes,3);peekMedia.Children.Add(peekTimes);
        Grid.SetRow(peekWave,2);Grid.SetColumn(peekWave,1);peekWave.HorizontalAlignment=HorizontalAlignment.Center;peekWave.Margin=new(0,6,0,0);peekMedia.Children.Add(peekWave);

        timelinePanel.RowDefinitions.Add(new(){Height=GridLength.Auto});timelinePanel.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var labels=new DockPanel();DockPanel.SetDock(duration,Dock.Right);labels.Children.Add(duration);labels.Children.Add(elapsed);timelinePanel.Children.Add(labels);
        Grid.SetRow(seekSlider,1);timelinePanel.Children.Add(seekSlider);UI.LocalizedName(seekSlider,"Playback position");
        seekSlider.PreviewMouseLeftButtonDown+=(_,e)=>
        {
            if(!seekSlider.IsEnabled || updating)return;
            seekSlider.ApplyTemplate();
            if(seekSlider.Template.FindName("PART_Track",seekSlider) is not System.Windows.Controls.Primitives.Track track)return;
            // Thumb gestures keep native dragging. Track clicks seek without depending
            // on whether WPF has assigned keyboard focus before ValueChanged fires.
            if(track.Thumb?.IsMouseOver==true)return;
            double target=track.ValueFromPoint(e.GetPosition(track));if(!double.IsFinite(target))return;
            seekSlider.Focus();seekSlider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty,Math.Clamp(target,seekSlider.Minimum,seekSlider.Maximum));
            seekDelay.Stop();seekDelay.Start();e.Handled=true;
        };
        seekSlider.ValueChanged+=(_,_)=>{if(!updating && (seekSlider.IsMouseCaptureWithin || seekSlider.IsKeyboardFocusWithin)){seekDelay.Stop();seekDelay.Start();}};
        seekDelay.Tick+=async(_,_)=>{seekDelay.Stop();await app.Media.Seek(seekSlider.Value);};
    }
    private void StopHoverTimers(){hoverDelay.Stop();leaveDelay.Stop();detailDelay.Stop();assistantHover.Stop();}
    internal void PreviewActivity()
    {
        if(app.Notifications.Current!=null && !expanded && !peeking && !fileDragActive && !userHidden){OpenNotice();return;}
        if(PeekPages().Length==0 || (!app.Settings.CompactLayout && !tuckPreview && !fileDragActive) || (!app.Settings.HoverPeek && !automaticPreview && !tuckPreview && !fileDragActive) || expanded || peeking || userHidden || (TuckRequested && !tuckPreview && !fileDragActive))return;
        peeking=true;RefreshNoticeEdge();SyncAssistantVisibility();Show();Dimensions();ConfigurePerformance();peekDetailed=!app.Settings.TwoStageHover || automaticPreview;detailDelay.Stop();if(!peekDetailed)detailDelay.Start();
        scroll.Visibility=Visibility.Collapsed;compact.IsHitTestVisible=false;Motion.Fade(compact,0,80);
        PreparePeekDeck();
        peekPanel.Visibility=Visibility.Visible;peekPanel.IsHitTestVisible=true;Motion.Fade(peekPanel,1,160);
        motion.Go(surface,IslandSurface.SurfaceWidthProperty,PeekSurfaceWidth,app.Settings.AnimationMs);
        motion.Go(surface,IslandSurface.SurfaceHeightProperty,peekDetailed?PeekExpandedHeight:100,app.Settings.AnimationMs);motion.Go(surface,IslandSurface.GlassLiftProperty,1,app.Settings.AnimationMs);motion.OnComplete(()=>{});UpdatePeek();WakeLiveRendering();
    }
    private void UpdatePeek()
    {
        if(!peeking || noticeShowing || assistantPeeking)return;
        UpdatePeekDeck();
        bool media=app.Media.Available;ApplyMediaTypography(peekTitle,media && app.Settings.BoldMediaTitle);
        peekArt.Visibility=media && app.Media.Artwork!=null?Visibility.Visible:Visibility.Collapsed;SwapArtwork(peekArt,media?app.Media.Artwork:null);
        peekFallback.Visibility=peekArt.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;
        var now=DateTimeOffset.Now;
        string caption=media?app.Media.Title:WidgetOn("focus") && app.Session.Focus.Remaining(now)>0?"Focus · "+app.Session.Focus.Display(now):WidgetOn("timer") && app.Session.Timer.Remaining(now)>0?"Timer · "+app.Session.Timer.Display(now):now.ToString("HH:mm");
        if(activityDetail=="Ending soon")caption=(previewKind=="timer"?"Timer · "+app.Session.Timer.Display(now):"Focus · "+app.Session.Focus.Display(now));
        if(media)SwapText(peekTitle,caption);else Motion.Text(peekTitle,caption);
        string detail=media?app.Media.Artist:ActiveCountdown()!=null?"Session running · click for controls":now.ToString("dddd, d MMMM",new System.Globalization.CultureInfo("en-US"));
        if(media)SwapText(peekSubtitle,detail);else Motion.Text(peekSubtitle,detail);
        if(peekPlay!=null){peekPlay.Visibility=media?Visibility.Visible:Visibility.Collapsed;peekPlay.IsEnabled=app.Media.CanToggle;Motion.Icon(peekPlay,app.Media.Playing?"pause":"play",18,Brushes.Black);}
        var position=media?app.Media.Timeline():(ActiveCountdown() is Countdown c?(c.DurationSeconds-c.Remaining(now),c.DurationSeconds):(0d,0d));
        peekProgress.Visibility=position.Item2>0?Visibility.Visible:Visibility.Collapsed;
        peekElapsed.Visibility=peekDuration.Visibility=Visibility.Visible;
        peekProgress.Value=position.Item2>0?Math.Clamp(position.Item1/position.Item2,0,1):0;
        var format=position.Item2>=3600?@"h\:mm\:ss":@"m\:ss";
        peekElapsed.Text=TimeSpan.FromSeconds(Math.Max(0,position.Item1)).ToString(format);peekDuration.Text=TimeSpan.FromSeconds(Math.Max(0,position.Item2)).ToString(format);
        if(position.Item2<=0){peekElapsed.Text=media?"Windows media controls":"No active session";peekDuration.Text=media?"":"Open →";}
        if(media){timelineAt=DateTimeOffset.UtcNow;timelineElapsed=position.Item1;timelineDuration=position.Item2;}
        peekWave.Visibility=peekId=="media" && media && app.Settings.LiveActivity?Visibility.Visible:Visibility.Collapsed;
        UpdateClosedLyricsDemand();ApplyPeekGeometry();
    }
    private void BuildCompactBody()
    {
        if(timelinePanel.Parent is Panel old)old.Children.Remove(timelinePanel);
        title.FontSize=18;artist.FontSize=12;artwork.Width=artwork.Height=88;clock.FontSize=26;
        var navigation=new Grid{Tag="panel-navigation",Margin=new(0,0,0,app.Settings.SectionGap)};
        // Equal side columns keep the page dots centered: column 0 empty on the left, notifications and Settings on the right.
        navigation.ColumnDefinitions.Add(new(){Width=new GridLength(60)});navigation.ColumnDefinitions.Add(new());navigation.ColumnDefinitions.Add(new(){Width=new GridLength(60)});
        var settings=Icons.Button("settings",Localization.Text("Settings"),app.ShowSettings,false,28);settings.Margin=new(4,0,0,0);settings.Background=Brushes.Transparent;
        var trailing=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};trailing.Children.Add(NotificationBell(28));trailing.Children.Add(settings);Grid.SetColumn(trailing,2);navigation.Children.Add(trailing);
        var activePages = OrderedSections
            .Where(p => PanelWidgets.Any(w => PageFor(w.Id) == p))
            .ToList();
        foreach (var w in PanelWidgets)
        {
            string p = PageFor(w.Id);
            if (!activePages.Contains(p)) activePages.Add(p);
        }
        int categoryCount = Math.Max(1, activePages.Count);
        var tabRow=new System.Windows.Controls.Primitives.UniformGrid{Columns=categoryCount,Rows=1,Width=categoryCount*18,HorizontalAlignment=HorizontalAlignment.Center};var navScroll=new ScrollViewer{Content=tabRow,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Center};Grid.SetColumn(navScroll,1);KeyboardNavigation.SetDirectionalNavigation(tabRow,KeyboardNavigationMode.Cycle);navScroll.PanningMode=PanningMode.HorizontalOnly;navScroll.ToolTip=Localization.Text("Panel navigation · scroll for more panels");navScroll.PreviewMouseWheel+=(_,e)=>{if(navScroll.ScrollableWidth>0){navScroll.ScrollToHorizontalOffset(navScroll.HorizontalOffset-e.Delta*.5);e.Handled=true;}};navigation.Children.Add(navScroll);body.Children.Add(navigation);body.Children.Add(compactPageHost);
        bodyReflow.Add(()=>
        {
            navigation.Margin=new(0,0,0,app.Settings.SectionGap);tabRow.Width=categoryCount*18;
        });
        foreach(string page in activePages)
        {
            FrameworkElement view;if(page==activePage)view=BuildCompactPage(page);else{view=new Border();pendingCompactPages.Add(page);}view.Visibility=Visibility.Collapsed;compactPages[page]=view;compactPageHost.Children.Add(view);
            var tab=UI.Button(PageName(page),()=>SelectPage(page));tab.Padding=new(8,6,8,6);tab.FontSize=10.5;tab.MinHeight=30;tab.Margin=new(0,0,4,0);tab.Tag=new CornerRadius(15);StyleCategory(tab,page);compactTabs[page]=tab;tabRow.Children.Add(tab);
        }
        if(compactPages.Count==0){var empty=UI.Caption("Enable widgets in Settings.",12,UI.Muted);compactPages["tasks"]=empty;compactPageHost.Children.Add(empty);}
        var mixer=new Border{Visibility=Visibility.Collapsed};pendingCompactPages.Add("mixer");compactPages["mixer"]=mixer;compactPageHost.Children.Add(mixer);
        if(!compactPages.ContainsKey(activePage))activePage=compactPages.Keys.FirstOrDefault()??"tasks";
        SelectPage(activePage,false);
    }
    internal void SelectPage(string page,bool animate=true)
    {
        LeaveAssistantPanel();
        mediaControls?.Close(false);EnsureBody();

        if(!app.Settings.CompactLayout || commandOpen || !compactPages.ContainsKey(page))return;
        if(animate && activePage==page && compactPages[page].Visibility==Visibility.Visible)return;
        FlushNotes();EnsureCompactPage(page);InvalidateSystemMonitor();activePage=page;
        if(Settings.ToolNames.ContainsKey(page))
        {
            toolsMode=page;calendarEditing=false;
            if(toolsContent.Parent is Panel owner)owner.Children.Remove(toolsContent);
            ((Panel)compactPages[page]).Children.Add(toolsContent);RefreshTools();
        }
        scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Hidden;
        foreach(var entry in compactPages){entry.Value.Visibility=entry.Key==page?Visibility.Visible:Visibility.Collapsed;}
        UpdateSystemMonitor();
        foreach(var entry in compactTabs){entry.Value.Background=entry.Key==(page=="mixer"?"media":page)?UI.Brush(app.Settings.SelectionFill):Brushes.Transparent;entry.Value.Foreground=entry.Key==(page=="mixer"?"media":page)?UI.ContrastText(app.Settings.SelectionFill):UI.Muted;StyleCategory(entry.Value,entry.Key,entry.Key==(page=="mixer"?"media":page));}
        if(compactTabs.TryGetValue(page,out var selectedTab))selectedTab.BringIntoView();
        if(animate)Motion.Enter(compactPages[page],4);
        Dimensions();
        if(expanded){motion.Go(surface,IslandSurface.SurfaceWidthProperty,expandedWidth,Math.Min(300,app.Settings.AnimationMs));motion.Go(surface,IslandSurface.SurfaceHeightProperty,expandedHeight,Math.Min(300,app.Settings.AnimationMs));motion.OnComplete(()=>{});}
    }
    private FrameworkElement BuildDailyResetPage() => new DailyResetView(app);
    private FrameworkElement BuildCompactPage(string page)
    {
        switch(page)
        {
            case "reset": return BuildDailyResetPage();
            case "timer": return new CountdownView(app, false);
            case "focus": return new CountdownView(app, true);
            case "system": return BuildSystemMonitor();
            case "notes":
            case "tasks":
                var sp = new StackPanel();
                if(activePage == page)
                {
                    toolsMode = page;
                    calendarEditing = false;
                    if(toolsContent.Parent is Panel owner) owner.Children.Remove(toolsContent);
                    sp.Children.Add(toolsContent);
                    RefreshTools();
                }
                return sp;
            case "media":
            {
                mediaLyricsLayout=null;lyricsAside=null;mediaLyrics=null;lyricsButton=null;
                var panel=new StackPanel();
                if(WidgetOn("media"))
                {
                    var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;artist.TextWrapping=TextWrapping.NoWrap;artist.TextTrimming=TextTrimming.CharacterEllipsis;artist.Margin=new(0,4,0,0);info.Children.Add(WrapTitle(title));info.Children.Add(WrapTitle(artist));
                    var cover=new Border{Child=WrapArtwork(artwork),Width=88,Height=88,Margin=new(0,0,20,0),Clip=new RectangleGeometry(new Rect(0,0,88,88),15,15)};
                    var row=new Grid{Margin=new(0,0,0,6)};row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.ColumnDefinitions.Add(new());row.Children.Add(cover);
                    previous=Icons.Button("previous","Предыдущий трек",()=>_=app.Media.Command("previous"),false,38);previous.ToolTip="Предыдущий трек";
                    play=Icons.Button("play","Воспроизведение или пауза",()=>_=app.Media.Command("toggle"),true,46);play.ToolTip="Воспроизведение / Пауза";playIcon="";
                    next=Icons.Button("next","Следующий трек",()=>_=app.Media.Command("next"),false,38);next.ToolTip="Следующий трек";
                    previous.Background=next.Background=Brushes.Transparent;
                    previous.Margin=new(0,0,8,0);play.Margin=new(0);next.Margin=new(8,0,0,0);
                    lyricsButton=Icons.Button("lyrics","Текст песни",ToggleLyrics,false,28);lyricsButton.ToolTip="Текст песни";lyricsButton.Margin=new(16,0,0,0);lyricsButton.Background=Brushes.Transparent;
                    var buttons=UI.Row(previous,play,next);buttons.HorizontalAlignment=HorizontalAlignment.Center;var playbackRow=new Grid{Margin=new(0)};playbackRow.Children.Add(buttons);lyricsButton.HorizontalAlignment=HorizontalAlignment.Right;lyricsButton.VerticalAlignment=VerticalAlignment.Center;lyricsButton.Margin=new(0);playbackRow.Children.Add(lyricsButton);
                    Grid.SetColumn(info,1);row.Children.Add(info);panel.Children.Add(row);panel.Children.Add(playbackRow);row.Loaded+=(_,_)=>QueueMediaLayout();row.IsVisibleChanged+=(_,_)=>{if(row.IsVisible)QueueMediaLayout();};
                    row.SizeChanged+=(_,_)=>{bool narrow=row.ActualWidth<310;double size=narrow?64:88;cover.Width=cover.Height=artwork.Width=artwork.Height=size;cover.Margin=new(0,0,narrow?14:20,0);if(previousArtwork.TryGetValue(artwork,out var outgoing))outgoing.Width=outgoing.Height=size;musicWave.Width=narrow?112:164;cover.Clip=new RectangleGeometry(new Rect(0,0,size,size),narrow?11:15,narrow?11:15);previous.Width=previous.Height=next.Width=next.Height=narrow?28:38;play.Width=play.Height=narrow?38:46;title.FontSize=narrow?15:18;previous.Margin=new(0,0,narrow?2:8,0);next.Margin=new(narrow?2:8,0,0,0);};
                    if(app.Settings.LiveActivity){if(musicWave.Parent is Panel oldWave)oldWave.Children.Remove(musicWave);musicWave.Height=22;musicWave.Margin=new(0,0,0,8);var waveHost=new Grid{Margin=new(0,3,0,0)};waveHost.Children.Add(musicWave);panel.Children.Add(waveHost);}mediaLyrics=new LyricsView(app);SyncLyrics();timelinePanel.Margin=new(0,0,0,10);panel.Children.Add(timelinePanel);
                }
                var controls=BuildMediaControls(panel);
                return WidgetOn("media")?WrapMediaLyrics(controls):controls;
            }
            default:
                return new StackPanel();
        }
    }
    private void UpdateTimeline()
    {
        if(!app.Settings.CompactLayout || !WidgetOn("media"))return;
        var position=app.Media.Timeline();elapsed.Text=TimeSpan.FromSeconds(position.elapsed).ToString(position.duration>=3600?@"h\:mm\:ss":@"m\:ss");duration.Text=TimeSpan.FromSeconds(position.duration).ToString(position.duration>=3600?@"h\:mm\:ss":@"m\:ss");
        timelineAt=DateTimeOffset.UtcNow;timelineElapsed=position.elapsed;timelineDuration=position.duration;
        seekSlider.IsEnabled=position.duration>0 && app.Media.CanSeek;
        if(!seekSlider.IsMouseCaptureWithin && !seekSlider.IsKeyboardFocusWithin && !seekDelay.IsEnabled)seekSlider.Value=position.duration>0?position.elapsed/position.duration:0;
    }
    private bool mediaLayoutPending;
    private void QueueMediaLayout()
    {
        if(mediaLayoutPending)return;mediaLayoutPending=true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,()=>{mediaLayoutPending=false;if(expanded && app.Settings.CompactLayout && activePage=="media" && !assistantOpen && !commandOpen)ResizeToolPanel();});
    }
}
