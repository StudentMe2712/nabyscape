using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NabyScape;

public sealed partial class OverlayWindow
{
    private Button AssistantLauncher()
    {
        var orb=new AssistantOrb{Width=26,Height=26,ReduceMotion=app.Settings.ReduceMotion};
        var button=AssistantOrb.Launcher("Нажмите для открытия панели",app.ShowAssistant,orb);button.Width=28;button.MinHeight=28;button.ToolTip="Нажмите для открытия панели";
        void Refresh()
        {button.Visibility=app.Assistant.Options.Enabled?Visibility.Visible:Visibility.Collapsed;orb.State=app.Assistant.WakeListening?AssistantState.Listening:app.Assistant.State;orb.Live=app.Assistant.VoiceConnected || app.Assistant.WakeListening;orb.Level=app.Assistant.Level;orb.ReduceMotion=app.Settings.ReduceMotion;button.ToolTip=app.Assistant.WakeListening?"Голосовая активация активна · нажмите для управления":"Нажмите для открытия панели";}
        button.Loaded+=(_,_)=>{app.Assistant.Changed+=Refresh;app.Assistant.VisualChanged+=Refresh;Refresh();};
        button.Unloaded+=(_,_)=>{app.Assistant.Changed-=Refresh;app.Assistant.VisualChanged-=Refresh;};
        Refresh();return button;
    }
}
