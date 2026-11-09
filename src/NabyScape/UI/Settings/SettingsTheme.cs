using System;
using System.Windows;
using System.Windows.Media;
namespace NabyScape;

// Settings uses the owner-selected NabyScape identity: near-black, bone text and a lime
// signal reserved for "on", "selected" and the one primary action on screen.
// These resources are scoped to the Settings chrome so the live island preview keeps the
// user's own island colors.
internal static class SettingsTheme
{
    internal static readonly Brush Void=Frozen("#101113");
    internal static readonly Brush Sidebar=Frozen("#101113");
    internal static readonly Brush Card=Frozen("#1B1B1E");
    internal static readonly Brush CardRaised=Frozen("#252321");
    internal static readonly Brush Line=Frozen("#252321");
    internal static readonly Brush LineSoft=Frozen("#252321");
    internal static readonly Brush Bone=Frozen("#F2E9DF");
    internal static readonly Brush Quiet=Frozen("#96918B");
    internal static readonly Brush Lime=Frozen("#E58A43");
    internal static readonly Brush LimeInk=Frozen("#101113");
    internal static readonly Brush LimeWash=Frozen("#252321");
    internal static readonly Brush LimeGlow=Frozen("#20E58A43");
    internal static readonly FontFamily Display=new(new Uri("pack://application:,,,/"),"./Assets/Fonts/#Manrope, Segoe UI Variable Display, Segoe UI");
    internal static readonly FontFamily Body=new("Segoe UI Variable Text, Segoe UI");
    internal static readonly FontFamily Mono=new("Cascadia Mono, Consolas");
    private static Brush Frozen(string hex){var brush=UI.Brush(hex);if(brush.CanFreeze && !brush.IsFrozen)brush=brush.CloneCurrentValue();brush.Freeze();return brush;}
    // Control template resources (see Theme.xaml) re-pointed for Settings only.
    internal static ResourceDictionary ChromeResources()
    {
        var d=new ResourceDictionary();
        if(Accessibility.HighContrast)return d;
        d["Accent"]=Lime;d["AccentText"]=LimeInk;
        d["CheckedFill"]=Lime;d["CheckedText"]=LimeInk;d["SwitchKnob"]=Bone;
        d["TrackFill"]=Line;d["ProgressFill"]=Lime;d["ProgressTrack"]=Line;
        d["ControlFill"]=CardRaised;d["ControlBorder"]=Line;d["ControlText"]=Bone;
        d["SelectionFill"]=LimeWash;d["SelectionText"]=Bone;
        return d;
    }
}
