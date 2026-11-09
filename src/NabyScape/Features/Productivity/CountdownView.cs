using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace NabyScape;

// A single state-driven action prevents accidentally restarting a running countdown.
// Uses the shared heartbeat only while visible; no private timer or render loop.
internal sealed class CountdownView : StackPanel
{
    private readonly App app;
    private readonly bool focus;
    private readonly TextBlock digits=UI.Caption("",29), status=UI.Caption("Ready",10,UI.Muted), hint=UI.Caption("",10,UI.Muted), durationLabel=UI.Caption("",10,UI.Muted);
    private readonly ProgressRing progress=new();
    private readonly TextBox minutes, timerSeconds, breaks;
    private readonly Button primary, reset;
    private bool syncing, subscribed;
    private int knownWork,knownRest;
    private Guid shownRun;
    private double shownDuration=-1;
    private Countdown State=>focus?app.Session.Focus:app.Session.Timer;
    internal int Refreshes {get;private set;}
    internal bool Subscribed=>subscribed;

    internal CountdownView(App app,bool focus,bool heading=true)
    {
        this.app=app;this.focus=focus;knownWork=app.Settings.FocusMinutes;knownRest=app.Settings.BreakMinutes;
        string id=focus?"focus":"timer";
        var header=new DockPanel{LastChildFill=true,Margin=new(0,0,0,9)};
        status.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(status,Dock.Right);header.Children.Add(status);
        if(heading){var title=UI.Heading(Localization.Text(focus?"Помодоро / Фокус":"Таймер"),13);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);}Children.Add(header);
        Typography.SetNumeralAlignment(digits,FontNumeralAlignment.Tabular);digits.FontWeight=FontWeights.Medium;digits.TextWrapping=TextWrapping.NoWrap;digits.HorizontalAlignment=HorizontalAlignment.Center;
        progress.Accent(focus);UI.Named(progress,focus?"Focus session progress":"Timer progress");
        var instrument=new Grid{Margin=new(0,4,0,14)};instrument.Children.Add(progress);
        var center=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,IsHitTestVisible=false};center.Children.Add(digits);durationLabel.HorizontalAlignment=HorizontalAlignment.Center;durationLabel.Margin=new(0,4,0,0);center.Children.Add(durationLabel);instrument.Children.Add(center);Children.Add(instrument);

        minutes = new TextBox
        {
            Text = focus ? app.Settings.FocusMinutes.ToString(CultureInfo.CurrentCulture) : "05",
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxLength = focus ? 3 : 4
        };
        AutomationProperties.SetName(minutes, focus ? "Focus minutes" : "Timer duration in minutes");

        timerSeconds = new TextBox
        {
            Text = "00",
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxLength = 2
        };
        AutomationProperties.SetName(timerSeconds, "Timer duration in seconds");

        breaks = new TextBox
        {
            Text = app.Settings.BreakMinutes.ToString(CultureInfo.CurrentCulture),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxLength = 3
        };
        AutomationProperties.SetName(breaks, "Break minutes");

        FrameworkElement fieldsContainer;
        if (focus)
        {
            var focusGrid = new Grid();
            focusGrid.ColumnDefinitions.Add(new ColumnDefinition());
            focusGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var focusCol = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var focusLabel = UI.Caption(Localization.Text("Фокус · мин"), 11, UI.Muted);
            focusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            focusLabel.Margin = new Thickness(0, 0, 0, 5);
            focusCol.Children.Add(focusLabel);
            var focusPill = Pill(minutes, 68, 36);
            focusCol.Children.Add(focusPill);
            focusGrid.Children.Add(focusCol);

            var breakCol = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            Grid.SetColumn(breakCol, 1);
            var breakLabel = UI.Caption(Localization.Text("Перерыв · мин"), 11, UI.Muted);
            breakLabel.HorizontalAlignment = HorizontalAlignment.Center;
            breakLabel.Margin = new Thickness(0, 0, 0, 5);
            breakCol.Children.Add(breakLabel);
            var breakPill = Pill(breaks, 68, 36);
            breakCol.Children.Add(breakPill);
            focusGrid.Children.Add(breakCol);

            void OnFocusWheel(MouseWheelEventArgs e)
            {
                if (State.Remaining(DateTimeOffset.UtcNow) > 0) { e.Handled = true; return; }
                int cur = int.TryParse(minutes.Text, out int m) ? m : app.Settings.FocusMinutes;
                int step = e.Delta > 0 ? 1 : -1;
                minutes.Text = Math.Clamp(cur + step, 1, 240).ToString();
                CommitPreferences();
                e.Handled = true;
            }
            focusPill.PreviewMouseWheel += (_, e) => OnFocusWheel(e);
            minutes.PreviewMouseWheel += (_, e) => OnFocusWheel(e);

            void OnBreakWheel(MouseWheelEventArgs e)
            {
                if (State.Remaining(DateTimeOffset.UtcNow) > 0) { e.Handled = true; return; }
                int cur = int.TryParse(breaks.Text, out int b) ? b : app.Settings.BreakMinutes;
                int step = e.Delta > 0 ? 1 : -1;
                breaks.Text = Math.Clamp(cur + step, 1, 120).ToString();
                CommitPreferences();
                e.Handled = true;
            }
            breakPill.PreviewMouseWheel += (_, e) => OnBreakWheel(e);
            breaks.PreviewMouseWheel += (_, e) => OnBreakWheel(e);

            fieldsContainer = focusGrid;
        }
        else
        {
            var timerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };

            var minCol = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var minLabel = UI.Caption(Localization.Text("мин"), 11, UI.Muted);
            minLabel.HorizontalAlignment = HorizontalAlignment.Center;
            minLabel.Margin = new Thickness(0, 0, 0, 5);
            minCol.Children.Add(minLabel);
            var minPill = Pill(minutes, 68, 36);
            minCol.Children.Add(minPill);

            var colon = UI.Caption(":", 18, UI.Muted);
            colon.FontWeight = FontWeights.Bold;
            colon.VerticalAlignment = VerticalAlignment.Center;
            colon.Margin = new Thickness(10, 16, 10, 0);

            var secCol = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var secLabel = UI.Caption(Localization.Text("сек"), 11, UI.Muted);
            secLabel.HorizontalAlignment = HorizontalAlignment.Center;
            secLabel.Margin = new Thickness(0, 0, 0, 5);
            secCol.Children.Add(secLabel);
            var secPill = Pill(timerSeconds, 68, 36);
            secCol.Children.Add(secPill);

            timerStack.Children.Add(minCol);
            timerStack.Children.Add(colon);
            timerStack.Children.Add(secCol);

            void OnMinWheel(MouseWheelEventArgs e)
            {
                if (State.Remaining(DateTimeOffset.UtcNow) > 0) { e.Handled = true; return; }
                int cur = 0;
                if (double.TryParse(minutes.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double m) ||
                    double.TryParse(minutes.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out m))
                    cur = (int)Math.Round(m);
                int step = e.Delta > 0 ? 1 : -1;
                minutes.Text = Math.Clamp(cur + step, 0, 999).ToString("00");
                e.Handled = true;
            }
            minPill.PreviewMouseWheel += (_, e) => OnMinWheel(e);
            minutes.PreviewMouseWheel += (_, e) => OnMinWheel(e);

            void OnSecWheel(MouseWheelEventArgs e)
            {
                if (State.Remaining(DateTimeOffset.UtcNow) > 0) { e.Handled = true; return; }
                int cur = int.TryParse(timerSeconds.Text, out int s) ? s : 0;
                int step = e.Delta > 0 ? 1 : -1;
                timerSeconds.Text = Math.Clamp(cur + step, 0, 59).ToString("00");
                e.Handled = true;
            }
            secPill.PreviewMouseWheel += (_, e) => OnSecWheel(e);
            timerSeconds.PreviewMouseWheel += (_, e) => OnSecWheel(e);

            fieldsContainer = timerStack;
        }

        primary=UI.CaptionButton("Start",Activate,true);AutomationProperties.SetAutomationId(primary,id+"-primary");
        reset=UI.CaptionButton("Reset",()=>{State.Reset();app.SaveSession();});UI.Named(reset,Localization.Text(focus?"Reset focus":"Reset timer"));AutomationProperties.SetAutomationId(reset,id+"-reset");
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,0,0,18)};
        foreach(var button in new[]{primary,reset}){button.Width=button.Height=36;button.MinHeight=36;button.Margin=new(0);button.Padding=new(9);button.Tag=new CornerRadius(18);button.ToolTip="Tab to select; Space or Enter to activate";}
        primary.Margin=new(0,0,12,0);reset.Background=Brushes.Transparent;reset.Foreground=UI.Muted;reset.BorderThickness=new(1);reset.BorderBrush=UI.Brush("#3C4048");reset.Content=Icons.Create("reset",17,UI.Muted);actions.Children.Add(primary);actions.Children.Add(reset);Children.Add(actions);
        Children.Add(fieldsContainer);hint.MinHeight=20;hint.Margin=new(0,10,0,0);hint.VerticalAlignment=VerticalAlignment.Top;Children.Add(hint);

        static void BindInput(TextBox input, Action onBlur)
        {
            input.PreviewTextInput += (_, e) =>
            {
                foreach (char c in e.Text)
                {
                    if (!char.IsDigit(c))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            };
            input.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Space)
                    e.Handled = true;
                else if (e.Key == Key.Enter)
                {
                    onBlur();
                    e.Handled = true;
                }
            };
            input.GotKeyboardFocus += (_, _) => input.SelectAll();
            input.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!input.IsKeyboardFocusWithin)
                {
                    input.Focus();
                    input.SelectAll();
                    e.Handled = true;
                }
            };
            input.LostKeyboardFocus += (_, _) => onBlur();
        }

        if (focus)
        {
            BindInput(minutes, () =>
            {
                if (int.TryParse(minutes.Text, out int m))
                    minutes.Text = Math.Clamp(m, 1, 240).ToString();
                else
                    minutes.Text = app.Settings.FocusMinutes.ToString(CultureInfo.CurrentCulture);
                CommitPreferences();
            });
            BindInput(breaks, () =>
            {
                if (int.TryParse(breaks.Text, out int b))
                    breaks.Text = Math.Clamp(b, 1, 120).ToString();
                else
                    breaks.Text = app.Settings.BreakMinutes.ToString(CultureInfo.CurrentCulture);
                CommitPreferences();
            });
            minutes.TextChanged += (_, _) => { if (!syncing) Refresh(); };
            breaks.TextChanged += (_, _) => { if (!syncing) Refresh(); };
        }
        else
        {
            BindInput(minutes, () =>
            {
                if (double.TryParse(minutes.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double m) ||
                    double.TryParse(minutes.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out m))
                {
                    m = Math.Clamp(m, 0, 999);
                    if (m == Math.Floor(m))
                        minutes.Text = ((int)m).ToString("00");
                    else
                        minutes.Text = m.ToString("0.###", CultureInfo.InvariantCulture);
                }
                else
                {
                    minutes.Text = "05";
                }
                CommitPreferences();
            });
            BindInput(timerSeconds, () =>
            {
                if (int.TryParse(timerSeconds.Text, out int s))
                    timerSeconds.Text = Math.Clamp(s, 0, 59).ToString("00");
                else
                    timerSeconds.Text = "00";
                CommitPreferences();
            });
            minutes.TextChanged += (_, _) => { if (!syncing) Refresh(); };
            timerSeconds.TextChanged += (_, _) => { if (!syncing) Refresh(); };
        }

        Loaded+=(_,_)=>Subscribe();Unloaded+=(_,_)=>Unsubscribe();
        IsVisibleChanged+=(_,_)=>{if(IsVisible)Subscribe();else Unsubscribe();};
        Refresh();
    }

    private static Border Pill(TextBox input, double width, double height)
    {
        if (!double.IsNaN(width)) input.Width = width;
        input.MinHeight = height;
        input.Height = height;
        var border = new Border
        {
            Background = UI.Brush("#16181D"),
            BorderBrush = UI.Brush("#30343B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = input
        };
        if (!double.IsNaN(width)) border.Width = width;
        border.Height = height;
        input.GotKeyboardFocus += (_, _) => border.BorderBrush = UI.Brush("#4F535C");
        input.LostKeyboardFocus += (_, _) => border.BorderBrush = UI.Brush("#30343B");
        return border;
    }

    private void Subscribe(){if(!IsLoaded || !IsVisible || subscribed)return;app.Updated+=OnUpdate;subscribed=true;Refresh();}
    private void Unsubscribe(){if(!subscribed)return;app.Updated-=OnUpdate;subscribed=false;}
    private void OnUpdate(){if((app.CurrentUpdate & (RuntimeChange.Countdown|RuntimeChange.Settings))!=0)Refresh();}
    private bool Valid(out double seconds,out int work,out int rest)
    {
        seconds=0;work=rest=0;
        if(!focus)
        {
            bool hasM = double.TryParse(minutes.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double m) ||
                        double.TryParse(minutes.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out m);
            bool hasS = double.TryParse(timerSeconds.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ||
                        double.TryParse(timerSeconds.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out s);
            if (!hasM || !hasS) return false;
            if (double.IsNaN(m) || double.IsNaN(s) || double.IsInfinity(m) || double.IsInfinity(s)) return false;
            if (m < 0 || s < 0 || s >= 60 || m > 1440) return false;
            seconds = (m * 60) + s;
            return seconds >= 1;
        }
        bool valid=int.TryParse(minutes.Text,out work) && work>=1 && work<=240 && int.TryParse(breaks.Text,out rest) && rest>=1 && rest<=120;seconds=work*60;return valid;
    }
    private void CommitPreferences()
    {
        if(!focus || State.Remaining(DateTimeOffset.UtcNow)>0 || !Valid(out _,out int work,out int rest))return;
        if(app.Settings.FocusMinutes==work && app.Settings.BreakMinutes==rest)return;
        app.Settings.FocusMinutes=work;app.Settings.BreakMinutes=rest;app.ChangeSettings();
    }
    private void Activate()
    {
        var now=DateTimeOffset.UtcNow;
        if(State.Running)State.Pause(now);
        else if(State.Remaining(now)>0)State.Resume(now);
        else
        {
            if(!Valid(out double seconds,out _,out _)){Refresh();minutes.Focus();return;}
            CommitPreferences();
            State.Start(seconds,now,focus?"work":"");
            shownRun=State.RunId;shownDuration=State.DurationSeconds;
        }
        app.SaveSession();Refresh();
    }
    private void Refresh()
    {
        if(Window.GetWindow(this)?.WindowState==WindowState.Minimized)return;
        Refreshes++;var now=DateTimeOffset.UtcNow;var state=State;double remaining=state.Remaining(now);bool active=remaining>0, completed=!active && state.DurationSeconds>0;
        if(!focus && state.DurationSeconds>0 && (shownRun!=state.RunId || shownDuration!=state.DurationSeconds))
        {
            syncing=true;
            double sec = state.DurationSeconds;
            if (sec % 60 == 0)
            {
                minutes.Text = ((int)(sec / 60)).ToString("00");
                timerSeconds.Text = "00";
            }
            else
            {
                minutes.Text = (sec / 60).ToString("0.###", CultureInfo.InvariantCulture);
                timerSeconds.Text = "00";
            }
            syncing=false;
            shownRun=state.RunId;shownDuration=state.DurationSeconds;
        }
        // External preference changes update only unfocused editors; typing and selection survive heartbeats.
        if(focus && !active && !syncing)
        {
            syncing=true;
            if(knownWork!=app.Settings.FocusMinutes && !minutes.IsKeyboardFocusWithin && minutes.Text==knownWork.ToString(CultureInfo.CurrentCulture))minutes.Text=app.Settings.FocusMinutes.ToString(CultureInfo.CurrentCulture);
            if(knownRest!=app.Settings.BreakMinutes && !breaks.IsKeyboardFocusWithin && breaks.Text==knownRest.ToString(CultureInfo.CurrentCulture))breaks.Text=app.Settings.BreakMinutes.ToString(CultureInfo.CurrentCulture);
            knownWork=app.Settings.FocusMinutes;knownRest=app.Settings.BreakMinutes;syncing=false;
        }
        bool valid=Valid(out double seconds,out _,out _);
        digits.Text=active || completed?state.Display(now):Countdown.FormatSeconds(valid?seconds:0);
        Accessibility.Announce(status,Localization.Text(active?(state.Running?(focus?(state.Phase=="break"?"Break":"Focusing"):"Running"):"Paused"):completed?"Complete":"Ready"));
        string action=state.Running?"Pause":active?"Resume":"Start";
        if(AutomationProperties.GetItemStatus(primary)!=action){primary.Content=Icons.Create(state.Running?"pause":"play",17,Brushes.Black);AutomationProperties.SetItemStatus(primary,action);}
        AutomationProperties.SetName(primary,Localization.Text(state.Running?(focus?"Pause focus":"Pause timer"):active?(focus?"Resume focus":"Resume timer"):(focus?"Start focus":"Start")));
        primary.IsEnabled=active || valid;reset.IsEnabled=active || completed;
        minutes.IsEnabled=breaks.IsEnabled=timerSeconds.IsEnabled=!active;
        progress.Value=active || completed?state.Progress(now):0;
        progress.Accent(focus && state.Phase!="break");
        durationLabel.Text=active || completed?Localization.Format("of {0} min",(state.DurationSeconds/60).ToString("0.#",CultureInfo.CurrentCulture)):focus?Localization.Format("Focus · {0} min",minutes.Text):Localization.Format("of {0} min",(valid?seconds/60:0).ToString("0.#",CultureInfo.CurrentCulture));
        hint.Text=!active && !valid?Localization.Text(focus?"Use 1–240 focus and 1–120 break minutes.":"Укажите время (от 1 сек)."):completed?Localization.Text("Time is up."):focus?Localization.Format("{0} min break after focus.",breaks.Text):"";
        hint.Foreground=!active && !valid?UI.Brush("#F0B8A8"):UI.Muted;
    }
}
