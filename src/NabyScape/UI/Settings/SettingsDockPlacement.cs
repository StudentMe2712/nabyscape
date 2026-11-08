using System.Windows;
using System.Windows.Controls;
namespace NabyScape;
public sealed partial class SettingsWindow
{
    private void DockPlacementSettings()
    {
        var s=app.Settings;var p=new StackPanel();
        var floating=new StackPanel{Visibility=s.DockEdge=="Floating"?Visibility.Visible:Visibility.Collapsed};
        AddSlider(floating,"Отступ плавающего островка",s.Top,0,3000,v=>s.Top=v," px",1);
        p.Children.Add(PremiumChoice("Сторона экрана",s.DockEdge,new[]{"Top","Bottom","Left","Right","Floating"},v=>{s.DockEdge=v;floating.Visibility=v=="Floating"?Visibility.Visible:Visibility.Collapsed;}));p.Children.Add(floating);
        AddSlider(p,"Вертикальное выравнивание",s.DockVertical,0,100,v=>s.DockVertical=v,"%",1);
        AddSlider(p,"Боковой отступ",s.DockInset,0,200,v=>s.DockInset=v," px",1);
        p.Children.Add(UI.Text("Для расположения слева и справа действует вертикальное выравнивание. Вверху и внизу используется положение по горизонтали. Плавающий режим позволяет задать произвольный отступ от края.",11,UI.Muted));
        page.Children.Add(Section("Расположение островка",p));
    }
}
