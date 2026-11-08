using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace NabyScape;

public sealed partial class OverlayWindow
{
    private readonly bool idleDockReady;
    private readonly TextBlock idleDate=new(){FontSize=11,FontWeight=FontWeights.Medium,Foreground=UI.Muted,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.NoWrap,ClipToBounds=true,Visibility=Visibility.Collapsed};
    private DateTime idleDateDay;
    private bool idleClosed;
    private double lastClosedWidth=double.NaN;
    private bool? idleLayoutState;
    private bool ClosedVoiceActivity=>app.Assistant.VoiceConnected || app.Assistant.Busy || app.Assistant.Desktop.Sharing;
    private bool IdleContent=>!FeedbackVisible && app.Notifications.Current==null && !ClosedVoiceActivity &&
        !(ClosedMediaActive) &&
        !(WidgetOn("focus") && app.Session.Focus.Remaining(DateTimeOffset.UtcNow)>0) &&
        !(WidgetOn("timer") && app.Session.Timer.Remaining(DateTimeOffset.UtcNow)>0);
    private double ClosedSurfaceWidth=>ClosedLyricsWidth;
    private double ClosedContentWidth=>Math.Max(120,Math.Min(ClosedSurfaceWidth,expandedWidth)-(surface.Notch?32:12));
    private string ClosedVoiceLabel=>app.Assistant.State switch
    {
        AssistantState.Listening=>"Listening",AssistantState.Thinking=>"Thinking…",AssistantState.Speaking=>"Speaking",AssistantState.Connecting=>"Connecting…",AssistantState.Error=>"Voice needs attention",_=>app.Assistant.Desktop.Sharing?"Screen sharing":"Naby voice"
    };
    private void RefreshIdleDock()
    {
        if(!idleDockReady)return;
        idleClosed=IdleContent && !TuckRequested && !dockEditing;
        compactClock.Visibility=Visibility.Collapsed;
        idleDate.Visibility=Visibility.Collapsed;
        summary.Visibility=Visibility.Collapsed;
        closedSummaryHost.Visibility=Visibility.Collapsed;
        closedTitleWave.Visibility=Visibility.Collapsed;
        awakeMark.Visibility=Visibility.Collapsed;
        activity.Visibility=Visibility.Collapsed;
        closedCover.Visibility=Visibility.Collapsed;
        if(idleLayoutState!=idleClosed)
        {
            idleLayoutState=idleClosed;
            UpdateAmbientMotion();
        }
        if(idleDate.Visibility==Visibility.Visible && idleDateDay!=DateTime.Today)
        {
            idleDateDay=DateTime.Today;var culture=System.Globalization.CultureInfo.GetCultureInfo("en-US");
            idleDate.Inlines.Clear();
            idleDate.Inlines.Add(new Run(idleDateDay.ToString("ddd",culture).ToUpperInvariant()){FontSize=9*Accessibility.TextScale,Foreground=Accessibility.HighContrast?SystemColors.WindowTextBrush:UI.Brush("#9DA3AD")});
            idleDate.Inlines.Add(new Run("  ·  "){FontSize=9*Accessibility.TextScale,Foreground=Accessibility.HighContrast?SystemColors.WindowTextBrush:UI.Brush("#626873")});
            idleDate.Inlines.Add(new Run(idleDateDay.ToString("dd",culture)){FontSize=12*Accessibility.TextScale,FontWeight=FontWeights.SemiBold,Foreground=Accessibility.HighContrast?SystemColors.WindowTextBrush:UI.Brush("#E5E8ED")});
            idleDate.Inlines.Add(new Run(" "+idleDateDay.ToString("MMM",culture).ToUpperInvariant()){FontSize=10*Accessibility.TextScale,Foreground=Accessibility.HighContrast?SystemColors.WindowTextBrush:UI.Brush("#B7BDC7")});
            idleDate.ToolTip=idleDateDay.ToString("dddd, dd MMMM yyyy",culture);
        }
        compact.Width=ClosedContentWidth;assistantCharacterHost.Width=ClosedContentWidth;
        if(!expanded && !peeking && !userHidden && !TuckRequested && !dockEditing)
        {
            double target=ClosedSurfaceWidth;
            if(lastClosedWidth!=target || !motion.Active && Math.Abs(surface.SurfaceWidth-target)>.5)
            {lastClosedWidth=target;motion.Go(surface,IslandSurface.SurfaceWidthProperty,target,app.Settings.AnimationMs);WakeLiveRendering();}
        }
    }
}
