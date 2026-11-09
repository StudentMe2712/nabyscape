using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace NabyScape;

internal sealed record SettingsCategory(string Id,string Title,string Group,string Description,string Terms,string[]? Sections=null);
public sealed partial class SettingsWindow
{
    private static readonly SettingsCategory[] SettingsCategories=
    {
        new("Home","Начало работы","Settings","Настройте Desktopia под свои предпочтения.","home start setup help beginner"),
        new("Appearance","Цвета и тема","Personalize","Форма островка, цвета, материалы и шрифт.","appearance theme warna tampilan font shape glass",new[]{"Shape","Settings material","Color & material","Surface","Controls & outlines","Typography","Text and navigation"}),
        new("Ambience","Подсветка","Personalize","Свечение вокруг островка и реакция на музыку.","ambient ambience cahaya warna glow spread softness light reach closed dock tertutup",new[]{"Ambient light"}),
        new("Layout","Позиция и размер","Dock","Кромка экрана, положение и размеры островка.","position posisi pindah atas bawah kanan kiri monitor size resize ukuran"),
        new("Visibility","Поведение и скрытие","Dock","Скрытие при перекрытии окнами и поведение у кромки.","hide show tuck sembunyi tutup youtube fullscreen maximized hover preview",new[]{"Automatic tucking","Manual tuck shortcut","Hide or show the island","Automatic activity"}),
        new("Content","Виджеты и инструменты","Features","Включение, сортировка и настройка сессий фокуса.","widget tools urutan notes task timer focus stopwatch pomodoro"),
        new("Audio","Музыка и звук","Features","Плеер, текст песен и аудиовизуализатор.","music spotify volume musik suara waveform spectrum lirik lyrics audio"),
        new("Notifications","Уведомления","App","Оповещения, звуки и тихие часы.","notifications notif pemberitahuan alert sound bunyi quiet hours downloads other apps claude telegram windows",new[]{"Notifications","Other apps","Notification presentation","Downloads","Completion sound"}),
        new("Application","Основные","App","Автозапуск, данные, сброс настроек и обновления.","general startup update version install storage data reset backup mulai windows")
    };
    private static SettingsCategory Category(string id)=>SettingsCategories.FirstOrDefault(c=>c.Id==id)??SettingsCategories[0];
    private static string CategoryTitle(string id)=>Localization.Text(Category(id).Title);
    private static string CategoryId(string title)=>SettingsCategories.FirstOrDefault(c=>c.Title==title || c.Id==title || Localization.Text(c.Title)==title)?.Id??title;
    private static string SettingsRoot(string id)=>id switch{"Ambience"=>"Appearance","Visibility"=>"Controls","Notifications"=>"Application",_=>id};
    private static string FriendlySection(string title)=>title switch
    {
        "Shape"=>"Форма островка","Color & material"=>"Цвета","Settings material"=>"Окно настроек","Surface"=>"Стекло и поверхность","Controls & outlines"=>"Кнопки и поля ввода","Typography"=>"Шрифт и начертание","Text and navigation"=>"Индикаторы страниц","Animation performance"=>"Плавность и производительность","Display"=>"Экран","Closed island"=>"Компактный островок","Open panel"=>"Раскрытая панель","Enabled widgets & order"=>"Ваши виджеты","Your widgets"=>"Ваши виджеты","Focus sessions"=>"Сессии фокуса (Помодоро)","Notch gestures"=>"Действия мыши","Global hotkey"=>"Горячая клавиша открытия","Automatic activity"=>"Автоматические превью","Clipboard history"=>"История буфера обмена","Completion sound"=>"Звук завершения",_=>title
    };
    private void FilterSettingsCategory(string id)
    {
        var definition=Category(id);
        var excluded=id switch{"Content"=>ShowAllSettings?new[]{"Clipboard history"}:Array.Empty<string>(),"Application"=>Category("Notifications").Sections!,"Controls"=>Category("Visibility").Sections!,_=>Array.Empty<string>()};
        foreach(var section in page.Children.OfType<Border>().Where(b=>b.Tag is string).ToArray())
        {
            string key=(string)section.Tag;
            if(definition.Sections is {} allowed && !allowed.Contains(key) || excluded.Contains(key))page.Children.Remove(section);
        }
        if(definition.Sections!=null)foreach(var note in page.Children.OfType<TextBlock>().Skip(1).ToArray())page.Children.Remove(note);
        var retained=page.Children.OfType<Border>().Select(b=>b.Tag as string).ToHashSet();settingExamples.RemoveAll(scene=>!retained.Contains(scene.Section));
    }
}
