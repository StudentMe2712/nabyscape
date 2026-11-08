using System;
using System.Windows;
using System.Windows.Controls;

namespace NabyScape;

public sealed partial class SettingsWindow
{
    private void MediaExperienceSettings()
    {
        var s=app.Settings;
        var modeLabels=new[]{"Частотный спектр","История амплитуды"};
        var mode=new ComboBox{ItemsSource=modeLabels,SelectedIndex=s.WaveformSource=="Spectrum"?0:1};UI.Named(mode,"Входной сигнал визуализатора");
        mode.SelectionChanged+=(_,_)=>{s.WaveformSource=mode.SelectedIndex==0?"Spectrum":"Envelope";Save();};
        var sensitivity=new StackPanel();AddSlider(sensitivity,"Чувствительность визуализатора",s.WaveformSensitivity,.5,3,v=>s.WaveformSensitivity=v,"×",.05);
        page.Children.Add(Section("Визуализатор звука",mode,sensitivity,UI.Text("Спектр разделяет реальные басы, средние и высокие частоты аудиопотока Windows. Захват микрофона не используется.",11,UI.Muted)));
        var presetLabels=new[]{"Скольжение · плавные строки","Фокус · мягкий акцент","Караоке · бегущая строка"};
        var presets=new ComboBox{ItemsSource=presetLabels,SelectedIndex=Math.Max(0,Array.IndexOf(new[]{"Slide","Focus","Karaoke"},s.LyricsAnimation))};UI.Named(presets,"Стиль анимации текста");presets.SelectionChanged+=(_,_)=>{if(presets.SelectedIndex<0)return;s.LyricsAnimation=new[]{"Slide","Focus","Karaoke"}[presets.SelectedIndex];Save();};
        var timing=new StackPanel();AddSlider(timing,"Смещение тайминга текста",s.LyricsOffset,-10,10,v=>s.LyricsOffset=v," с",.1);var local=UI.Text("Локальные файлы .LRC могут использоваться для синхронизации текста при отсутствии сети.",11,UI.Muted);timing.Children.Add(UI.Button("Загрузить файл .LRC…",()=>local.Text=LyricsFiles.Import(app,this)));timing.Children.Add(local);
        var textModeLabels=new[]{"Оригинальный текст","Японский Romaji · латиница"};
        var textMode=new ComboBox{ItemsSource=textModeLabels,SelectedIndex=s.LyricsTextMode=="Romaji"?1:0};UI.Named(textMode,"Система письма текста");
        textMode.SelectionChanged+=(_,_)=>{s.LyricsTextMode=textMode.SelectedIndex==1?"Romaji":"Original";app.Lyrics.SetTextMode(s.LyricsTextMode);Save();};
        timing.Children.Insert(0,UI.Text("Текст песни",11,UI.Muted));timing.Children.Insert(1,textMode);
        page.Children.Add(Section("Текст песни",LyricsAnimationExample(),Check("Показывать синхронизированный текст в панели медиа",s.LyricsEnabled,v=>s.LyricsEnabled=v),UI.Text("Синхронизированный текст автоматически запрашивается из LRCLIB по названию трека и исполнителю.",11,UI.Muted),presets,timing,UI.Text("Значок текста рядом с кнопками воспроизведения переключает показ текста. Клавишами: переход к кнопке и Пробел.",11,UI.Muted)));
    }
    private FrameworkElement LyricsAnimationExample()
    {
        var sample=new LyricResult(new[]{new LyricLine(0,"Строка за строкой в такт песне"),new LyricLine(4,"Музыка наполняет пространство"),new LyricLine(8,"Точное совпадение со звуком")},"Предпросмотр анимации текста");
        var view=new LyricsView(app){Height=124,Margin=new(0,0,0,14)};view.SetFixture(sample,5.5,12);
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};var clock=System.Diagnostics.Stopwatch.StartNew();
        void Sync(){if(ElementVisibility.InViewport(view) && !app.Settings.ReduceMotion && Motion.Enabled)timer.Start();else timer.Stop();view.SetFixture(sample,5.5,12);}
        ScrollChangedEventHandler scrolled=(_,_)=>Sync();EventHandler state=(_,_)=>Sync();
        timer.Tick+=(_,_)=>{if(!ElementVisibility.InViewport(view)){timer.Stop();return;}view.SetFixture(sample,clock.Elapsed.TotalSeconds%12,12);};view.Loaded+=(_,_)=>{app.SettingsChanged+=Sync;settingsScroll.ScrollChanged+=scrolled;StateChanged+=state;Sync();};view.Unloaded+=(_,_)=>{app.SettingsChanged-=Sync;settingsScroll.ScrollChanged-=scrolled;StateChanged-=state;timer.Stop();};view.IsVisibleChanged+=(_,_)=>Sync();view.SizeChanged+=(_,_)=>Sync();return view;
    }
    private void DockEditingSettings()
    {
        page.Children.Add(Section("Перемещение и размер",new DockPositionEditor(app,Save),Check("Показывать маркеры перемещения и изменения размера",app.Settings.DockUnlocked,value=>app.Settings.DockUnlocked=value),UI.Text("Потяните верхний маркер, чтобы изменить положение островка. Потяните за нижние углы, чтобы изменить размер. Удерживайте Alt при фокусе на островке для быстрого показа маркеров.",11,UI.Muted)));
    }
    private void CompletionSoundSettings()
    {
        var s=app.Settings;
        var soundKeys=new[]{"Chime","Bell","Custom","Off"};
        var soundLabels=new[]{"Колокольчик","Звонок","Свой файл","Откл."};
        var sounds=new ComboBox{ItemsSource=soundLabels,SelectedIndex=Math.Max(0,Array.IndexOf(soundKeys,s.CountdownSound))};UI.Named(sounds,"Звук завершения таймера и фокуса");
        var path=UI.Text(s.CountdownSoundFile.Length>0?System.IO.Path.GetFileName(s.CountdownSoundFile):"Пользовательский звук не выбран",11,UI.Muted);
        var volume=new StackPanel();AddSlider(volume,"Громкость звука завершения",s.CountdownSoundVolume*100,0,100,value=>s.CountdownSoundVolume=value/100," %");
        sounds.SelectionChanged+=(_,_)=>{if(sounds.SelectedIndex>=0){s.CountdownSound=soundKeys[sounds.SelectedIndex];Save();}};
        var feedback=UI.Text("",11,UI.Muted);
        var choose=UI.Button("Выбрать WAV…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Аудио Wave (*.wav)|*.wav",Title="Выберите звук завершения (до 15 секунд)"};if(dialog.ShowDialog(this)!=true)return;s.CountdownSoundFile=dialog.FileName;s.CountdownSound="Custom";sounds.SelectedIndex=2;path.Text=System.IO.Path.GetFileName(dialog.FileName);Save();});
        page.Children.Add(Section("Звук завершения",sounds,volume,path,choose,UI.Button("Прослушать звук",()=>{app.CountdownAudio.Play(s);feedback.Text=app.CountdownAudio.LastError??"Воспроизведено через системный аудиовыход Windows.";}),feedback,UI.Text("Таймер и сессии фокуса воспроизводят этот звук при завершении. Используется стандартный аудиовыход Windows.",11,UI.Muted)));
    }
}
