using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace NabyScape;

public sealed partial class SettingsWindow : Window
{
    private readonly App app;
    private readonly StackPanel page=new(){Margin=new(28,20,28,28)};
    private readonly TextBlock status=UI.Caption("Изменения сохраняются автоматически на этом устройстве.",11,UI.Muted);
    private readonly Border preview;
    private readonly Border previewStage;
    private readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(180)};
    private readonly System.Collections.Generic.Dictionary<string,Button> navigationButtons=new();
    private readonly Border sectionIndexHost=new(){Margin=new(28,12,28,8)};
    private int navigationRevision,previewRevision;
    private int? previewBuildKey;
    private bool applyingSettings,previewDefaultPending;
    private readonly Button retrySave=UI.CaptionButton("Повторить сохранение",()=>{});
    private System.Text.Json.JsonElement savedSettings;
    private Action? refreshSettingsLayout;
    private readonly System.Collections.Generic.List<SettingsScene> settingExamples=new();
    private string current="Home";
    private readonly TextBlock categoryHeading=UI.Caption("",30),categoryDescription=UI.Caption("",13,UI.Muted),categoryEyebrow=UI.Text("",11);
    private readonly TextBlock previewHelp=UI.Caption("",11,UI.Muted);
    private ComboBox? categorySelector;
    internal void OpenCategory(string id)=>Navigate(id);
    internal void OpenAiSettings(){Navigate("Home");}
    internal void OpenAiHermesSettings(){Navigate("Home");}

    private readonly TextBlock frameStatus=UI.Caption("",11,UI.Muted);
    private readonly TextBlock hotkeyState=UI.Caption("",11,UI.Muted);
    public SettingsWindow(App app)
    {
        this.app=app;savedSettings=System.Text.Json.JsonSerializer.SerializeToElement(app.Settings);UI.LocalizedName(status,"Статус настроек");Accessibility.Live(status);Title=Localization.Text("NabyScape · Настройки");Width=1100;Height=780;MinWidth=480;MinHeight=300;WindowStartupLocation=WindowStartupLocation.Manual;
        Background=Accessibility.HighContrast?SystemColors.WindowBrush:SettingsTheme.Void;Foreground=Accessibility.HighContrast?SystemColors.WindowTextBrush:SettingsTheme.Bone;FontFamily=SettingsTheme.Body;FontSize=13;
        Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/island.ico"));
        Resources[typeof(CheckBox)]=FindResource("SettingsSwitch");
        InitializeSettingsBounds();InitializeMaterial();
        // Shell: a full-height sidebar (brand, search, navigation) beside one scrolling column
        // that holds the page header, the preview stage and the settings themselves.
        var root=new Grid();root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new());root.ColumnDefinitions.Add(new(){Width=new GridLength(SidebarWidth)});root.ColumnDefinitions.Add(new());
        var chromeResources=SettingsTheme.ChromeResources();
        var sidebar=new DockPanel{LastChildFill=true};
        var brandBlock=SidebarBrand();DockPanel.SetDock(brandBlock,Dock.Top);sidebar.Children.Add(brandBlock);
        var sidebarSearchHost=new Border{Margin=new(16,0,16,14)};DockPanel.SetDock(sidebarSearchHost,Dock.Top);sidebar.Children.Add(sidebarSearchHost);
        var navigation=new StackPanel{Orientation=Orientation.Vertical,Margin=new(12,0,12,16)};
        BuildNavigation(navigation);
        var sidebarFooter=SidebarFooter();DockPanel.SetDock(sidebarFooter,Dock.Bottom);sidebar.Children.Add(sidebarFooter);
        var navigationScroll=UI.Scroll(navigation);UI.LocalizedName(navigationScroll,"Категории настроек");sidebar.Children.Add(navigationScroll);
        var navBorder=new Border{Background=Accessibility.HighContrast?SystemColors.WindowBrush:SettingsTheme.Sidebar,BorderBrush=SettingsTheme.Line,BorderThickness=new(0,0,1,0),Child=sidebar,Resources=chromeResources};root.Children.Add(navBorder);
        var compactBar=new Grid{Margin=new(16,50,16,8),Visibility=Visibility.Collapsed};compactBar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});compactBar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var categories=new ComboBox{Margin=new(0,0,8,0),ItemsSource=SettingsCategories.Select(c=>Localization.Text(c.Title)).ToArray(),SelectedItem=CategoryTitle(current)};categorySelector=categories;UI.LocalizedName(categories,"Категория настроек");compactBar.Children.Add(categories);
        var compactSearchHost=new Border();Grid.SetColumn(compactSearchHost,1);compactBar.Children.Add(compactSearchHost);
        categories.SelectionChanged+=(_,_)=>{if(categories.SelectedItem is string name && CategoryId(name)!=current)Navigate(CategoryId(name));};RefreshNavigationMode();Grid.SetColumnSpan(compactBar,2);compactBar.Resources=chromeResources;root.Children.Add(compactBar);
        var main=new Grid{Resources=chromeResources};Grid.SetColumn(main,1);Grid.SetRowSpan(main,2);Grid.SetRowSpan(navBorder,2);root.Children.Add(main);main.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});main.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var workspace=new Grid();Grid.SetRow(workspace,0);main.Children.Add(workspace);
        var form=new Grid();form.RowDefinitions.Add(new(){Height=GridLength.Auto});form.RowDefinitions.Add(new());workspace.Children.Add(form);
        var column=new StackPanel{HorizontalAlignment=HorizontalAlignment.Left};
        var pageHeader=new StackPanel{Margin=new(0,0,0,20)};categoryEyebrow.FontFamily=SettingsTheme.Display;categoryEyebrow.FontWeight=FontWeights.Bold;categoryEyebrow.FontSize=11;categoryEyebrow.Foreground=SettingsTheme.Lime;categoryEyebrow.Margin=new(0,0,0,6);pageHeader.Children.Add(categoryEyebrow);
        categoryHeading.FontFamily=SettingsTheme.Display;categoryHeading.FontWeight=FontWeights.Bold;categoryHeading.Foreground=SettingsTheme.Bone;pageHeader.Children.Add(categoryHeading);categoryDescription.Foreground=SettingsTheme.Quiet;categoryDescription.Margin=new(0,6,0,0);pageHeader.Children.Add(categoryDescription);column.Children.Add(pageHeader);
        var scroll=UI.Scroll(column);scroll.Padding=new(0,0,8,0);form.Children.Add(scroll);Grid.SetRow(scroll,1);settingsScroll=scroll;InitializePinnedAmbientPreview(form,scroll);
        InitializeSettingsSearch(main,workspace,sidebarSearchHost);
        preview=new Border{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};
        var previewColumn=new StackPanel{Margin=new(18,18,18,16)};var previewTitle=UI.Caption("Предпросмотр",11);previewTitle.FontFamily=SettingsTheme.Display;previewTitle.FontWeight=FontWeights.Bold;previewTitle.Foreground=SettingsTheme.Quiet;previewColumn.Children.Add(previewTitle);previewFocus.Margin=new(0,4,0,0);previewFocus.Foreground=SettingsTheme.Bone;previewColumn.Children.Add(previewFocus);
        var modes=new Grid{Margin=new(0,12,0,12)};for(int i=0;i<3;i++)modes.ColumnDefinitions.Add(new());int modeColumn=0;
        foreach(string mode in new[]{"Closed","Hover","Panel"})
        {
            string modeLabel=mode switch{"Closed"=>"Закрыт","Hover"=>"Наведение","Panel"=>"Панель",_=>mode};
            var button=UI.CaptionButton(modeLabel,()=>{previewMode=mode;RefreshPreview();});
            button.FontSize=11;button.Padding=new(3,6,3,6);button.Margin=new(0,0,4,0);button.MinHeight=30;button.Tag=new CornerRadius(15);
            UI.Named(button,"Предпросмотр: "+modeLabel);Grid.SetColumn(button,modeColumn++);modes.Children.Add(button);previewModeButtons[mode]=button;
        }
        previewColumn.Children.Add(modes);
        previewStage=new Border{Height=StageHeight,Background=Brushes.Transparent,ClipToBounds=true,Child=new System.Windows.Controls.Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=preview,VerticalAlignment=VerticalAlignment.Top,Margin=new(16,3,16,10)}};
        previewDimensions.Margin=new(0,0,0,12);previewColumn.Children.Add(previewDimensions);var sample=UI.Caption("Контент предпросмотра",11,UI.Muted);sample.Margin=new(0,0,0,5);previewColumn.Children.Add(sample);var examples=new ComboBox();previewExamples=examples;
        var exampleItems=new[]{("Музыка","Music"),("Таймер","Timer"),("Фокус","Focus"),("Секундомер","Stopwatch"),("Календарь","Calendar"),("Заметка","Note"),("Полка","Shelf"),("Инструменты","Tools"),("Система","System"),("Запуск","Launch"),("Навигация","Navigation"),("Индикатор громкости","Volume feedback"),("Зона сброса","Drop zone"),("Окно предпросмотра","Detached preview"),("Жесты обложки","Cover gestures"),("Умный буфер","Smart clipboard"),("Цвета элементов","Control colors"),("Типографика","Typography"),("Навигация панели","Panel navigation"),("Видимость","Visibility"),("Задачи","Tasks"),("Автозапуск","Startup")};
        foreach(var item in exampleItems)examples.Items.Add(item.Item1);
        examples.SelectedIndex=0;UI.LocalizedName(examples,"Контент предпросмотра");
        examples.SelectionChanged+=(_,_)=>{if(examples.SelectedIndex>=0)previewContent=exampleItems[examples.SelectedIndex].Item2;RefreshPreview();};
        previewColumn.Children.Add(examples);var hint=previewHelp;hint.Margin=new(0,10,0,0);previewColumn.Children.Add(hint);
        var stage=StageFrame(previewStage,previewColumn);
        var previewExpander=new Expander{Header=UI.Caption("Интерактивный предпросмотр",12,UI.Muted),Foreground=UI.Muted,Margin=new(0,0,0,20),Content=stage,IsExpanded=true};
        var rail=new Border{Child=previewExpander};column.Children.Add(rail);
        sectionIndexHost.Visibility=Visibility.Collapsed;column.Children.Add(sectionIndexHost);column.Children.Add(page);
        var saveRow=new DockPanel();undoSettings.Visibility=Visibility.Collapsed;undoSettings.Click+=(_,_)=>UndoSettings();DockPanel.SetDock(undoSettings,Dock.Right);saveRow.Children.Add(undoSettings);retrySave.Visibility=Visibility.Collapsed;retrySave.HorizontalAlignment=HorizontalAlignment.Right;retrySave.Click+=(_,_)=>CommitSettings();DockPanel.SetDock(retrySave,Dock.Right);saveRow.Children.Add(retrySave);
        var savedDot=new System.Windows.Shapes.Ellipse{Width=6,Height=6,Fill=SettingsTheme.Lime,Margin=new(0,0,9,0),VerticalAlignment=VerticalAlignment.Center};saveRow.Children.Add(savedDot);status.VerticalAlignment=VerticalAlignment.Center;status.Foreground=SettingsTheme.Quiet;saveRow.Children.Add(status);
        var footer=new Border{Padding=new(40,10,26,12),BorderBrush=SettingsTheme.LineSoft,BorderThickness=new(0,1,0,0),Background=Accessibility.HighContrast?SystemColors.WindowBrush:SettingsTheme.Void,Child=saveRow};
        bool? previousNarrow=null;
        bool? previousShort=null;
        void Responsive()
        {
            bool noPreview=current is not ("Appearance" or "Ambience" or "Layout" or "Motion" or "Audio");
            bool narrow=ActualWidth<1100,compactNav=ActualWidth<760,shortScreen=ActualHeight<600,veryShort=ActualHeight<340;
            compactBar.Margin=new Thickness(16,veryShort?44:50,16,veryShort?3:8);
            footer.Padding=veryShort?new Thickness(20,4,20,6):new Thickness(compactNav?20:40,10,26,12);
            root.ColumnDefinitions[0].Width=compactNav?new GridLength(0):new GridLength(SidebarWidth);
            navBorder.Visibility=compactNav?Visibility.Collapsed:Visibility.Visible;compactBar.Visibility=compactNav?Visibility.Visible:Visibility.Collapsed;categories.Visibility=compactBar.Visibility;
            if(settingsSearchHeader!=null){var host=compactNav?compactSearchHost:sidebarSearchHost;if(settingsSearchHeader.Parent!=host){if(settingsSearchHeader.Parent is Border old)old.Child=null;host.Child=settingsSearchHeader;}}
            if(categories.SelectedItem?.ToString()!=CategoryTitle(current))categories.SelectedItem=CategoryTitle(current);
            Grid.SetRow(main,compactNav?1:0);Grid.SetRowSpan(main,compactNav?1:2);Grid.SetColumn(main,compactNav?0:1);Grid.SetColumnSpan(main,compactNav?2:1);
            rail.Visibility=noPreview?Visibility.Collapsed:Visibility.Visible;
            // Each page opens with the preview at its default for the window size; manual toggles last until you switch pages or resize past a breakpoint.
            if(previousNarrow!=narrow || previousShort!=shortScreen || previewDefaultPending){previewExpander.IsExpanded=!narrow && !shortScreen;previousNarrow=narrow;previousShort=shortScreen;previewDefaultPending=false;}
            previewStage.Height=narrow?Math.Clamp(ActualHeight*.24,90,StageHeight):StageHeight;
            StageCompact(stage,narrow);
            hint.Visibility=narrow?Visibility.Collapsed:Visibility.Visible;sample.Visibility=narrow?Visibility.Collapsed:Visibility.Visible;examples.Visibility=narrow?Visibility.Collapsed:Visibility.Visible;
            categoryEyebrow.Visibility=shortScreen?Visibility.Collapsed:Visibility.Visible;
            categoryDescription.Visibility=shortScreen?Visibility.Collapsed:Visibility.Visible;
            categoryHeading.FontSize=shortScreen?20:30;
            pageHeader.Margin=new Thickness(0,0,0,shortScreen?8:20);pageHeader.Visibility=current=="Home"?Visibility.Collapsed:Visibility.Visible;
            double side=compactNav?18:narrow?28:44;
            column.Margin=new Thickness(side,compactNav?(veryShort?4:12):(shortScreen?44:52),side,28);
            // Leave room for the vertical scrollbar so card borders are never clipped.
            double available=workspace.ActualWidth-side*2-28;
            column.Width=Math.Max(240,Math.Min(MaxContentWidth,available));
            page.Width=column.Width;page.Margin=new Thickness(0);
        }
        refreshSettingsLayout=Responsive;
        UI.LocalizedName(previewExpander,"Интерактивный предпросмотр настроек");UI.LocalizedName(scroll,"Область настроек");UI.LocalizedName(rail,"Панель предпросмотра");UI.LocalizedName(navBorder,"Навигация по настройкам");
        SizeChanged+=(_,_)=>Responsive();workspace.SizeChanged+=(_,_)=>Responsive();Loaded+=(_,_)=>Responsive();
        page.HorizontalAlignment=HorizontalAlignment.Left;
        Grid.SetRow(footer,1);main.Children.Add(footer);Content=SettingsFrame(root);InitializePreviewMotion(previewColumn);InitializeAmbientPreview();
        saveTimer.Tick+=(_,_)=>CommitSettings();
        app.Updates.Changed+=UpdateControls;
        app.SettingsChanged+=SettingsApplied;app.Updated+=UpdateFrameStatus;
        Closed+=(_,_)=>{if(saveTimer.IsEnabled)CommitSettings();app.Updates.Changed-=UpdateControls;app.SettingsChanged-=SettingsApplied;app.Updated-=UpdateFrameStatus;};InitializeHomeStatuses();Navigate("Home");RefreshPreview();
    }
    private void CommitSettings()
    {
        saveTimer.Stop();
        try
        {
            applyingSettings=true;app.ChangeSettings();
            if(!app.SettingsSaveSucceeded){SetSaveFeedback("Не сохранено · изменения активны только для этой сессии. Нажмите «Повторить сохранение».",true);return;}
            CaptureSettingsUndo(savedSettings);
            string summary=SettingsChangeFeedback.Describe(savedSettings,app.Settings);
            savedSettings=System.Text.Json.JsonSerializer.SerializeToElement(app.Settings);
            SetSaveFeedback(summary);
        }
        catch(Exception ex){SetSaveFeedback("Не удалось сохранить изменения. Нажмите «Повторить сохранение».",true);app.Store.Log(ex);}
        finally{applyingSettings=false;}
    }
    private void SetSaveFeedback(string text,bool error=false)
    {
        retrySave.Visibility=error?Visibility.Visible:Visibility.Collapsed;
        status.Foreground=Accessibility.HighContrast?SystemColors.WindowTextBrush:error?UI.Brush("#F0B8B2"):UI.Brush("#F2F3F5");
        Accessibility.Announce(status,Localization.Text(text));status.ToolTip=text;
    }
    private void Save() {previewRevision++;InvalidateExamples(System.Windows.Input.Keyboard.FocusedElement as DependencyObject??System.Windows.Input.Mouse.DirectlyOver as DependencyObject);RefreshPreview();Accessibility.Announce(status,Localization.Text("Сохранение изменений…"));saveTimer.Stop();saveTimer.Start();}
    private void SettingsApplied(){ApplyMaterial();if(applyingSettings)return;if(app.SettingsSaveSucceeded){CaptureSettingsUndo(savedSettings);SetSaveFeedback(SettingsChangeFeedback.Describe(savedSettings,app.Settings));savedSettings=System.Text.Json.JsonSerializer.SerializeToElement(app.Settings);}else SetSaveFeedback("Не сохранено · изменения активны только для этой сессии. Нажмите «Повторить сохранение».",true);previewRevision++;InvalidateExamples(null);RefreshPreview();}
    private void InvalidateExamples(DependencyObject? source)
    {
        for(var node=source;node!=null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
            if(node is Border {Tag:string title}){var example=settingExamples.FirstOrDefault(e=>e.Section==title);if(example!=null){example.InvalidateVisual();return;}}
        foreach(var example in settingExamples)example.InvalidateVisual();
    }
    private void RefreshPreview()
    {
        pinnedAmbientScene?.InvalidateVisual();
        RefreshAmbientPreview();
        int key=HashCode.Combine(HashCode.Combine(current,previewMode,previewContent,previewRevision),app.Media.Title,app.Media.Artist,app.Media.Playing,app.Media.Artwork,app.Audio.Volume,app.Audio.Muted);
        if(previewBuildKey!=key){previewBuildKey=key;RenderSettingsPreview();}SyncPreviewRendering();
    }
    private void Navigate(string name)
    {
        if(searchResultsHost!=null)searchResultsHost.Visibility=Visibility.Collapsed;
        Accessibility.Announce(status,Localization.Text("Изменения сохраняются автоматически на этом устройстве."));previewHelp.Text=Localization.Text("Изменения применяются автоматически. В предпросмотре используются ваши форма, размер и цвета.");
        bool same=name==current && page.Children.Count>0;previewDefaultPending|=!same;revealAllSettings=revealAllNext || same && revealAllSettings;revealAllNext=false;double offset=settingsScroll.VerticalOffset;int revision=++navigationRevision;
        var expanded=SettingTree(page).OfType<Expander>().Where(e=>e.IsExpanded).Select(e=>e.Header?.ToString()).ToHashSet();
        string focused=System.Windows.Input.Keyboard.FocusedElement is DependencyObject oldFocus?UI.Identity(oldFocus):"";
        previewRevision++;settingExamples.Clear();HidePinnedAmbientPreview();current=name;categoryHeading.Text=CategoryTitle(name);categoryEyebrow.Text=CategoryEyebrow(name);categoryDescription.Text=Localization.Text(Category(name).Description);if(categorySelector!=null && categorySelector.SelectedItem?.ToString()!=CategoryTitle(name))categorySelector.SelectedItem=CategoryTitle(name);previewFocus.Text="Обзор: "+CategoryTitle(name);previewContent="Music";previewMode="Closed";if(name=="Launch"){previewContent="Launch";previewMode="Panel";}if(name=="Content"){previewContent="Navigation";previewMode="Panel";}if(name=="Controls"){previewContent="Music";previewMode="Hover";}if(name=="System"){previewContent="System";previewMode="Panel";}if(name=="Audio"){previewContent="Music";previewMode="Panel";}if(name=="Application"){previewContent="Startup";previewMode="Panel";}if(!same)settingsScroll.ScrollToTop();page.Children.Clear();
        StyleNavigationSelection(name);
        switch(SettingsRoot(name)){case "Home":SettingsHome();break;case "Appearance":Appearance();break;case "Layout":LayoutSettings();break;case "Content":ContentSettings();break;case "Launch":LaunchSettings();break;case "Controls":Interactions();break;case "Audio":AudioSettings();break;case "System":SystemSettings();break;default:General();break;}FilterSettingsCategory(name);if(PreferenceReset.Categories.TryGetValue(current,out var resetKeys) && resetKeys.Length>0)page.Children.Add(UI.CaptionButton("Сбросить эту категорию",()=>ResetPreferences(current)));ApplyEssentialsFilter();RefreshNavigationMode();OrganizeSettings();if(same){foreach(var group in SettingTree(page).OfType<Expander>())group.IsExpanded=expanded.Contains(group.Header?.ToString());page.UpdateLayout();settingsScroll.ScrollToVerticalOffset(offset);Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>{if(revision!=navigationRevision)return;var replacement=SettingTree(page).OfType<Control>().FirstOrDefault(c=>focused.Length>0 && UI.Identity(c)==focused);replacement?.Focus();settingsScroll.ScrollToVerticalOffset(offset);});}refreshSettingsLayout?.Invoke();RefreshPreview();
    }
    private Border Section(string title,params UIElement[] children)
    {
        var content=new StackPanel();var label=UI.Caption(FriendlySection(title),15);label.FontFamily=SettingsTheme.Display;label.FontWeight=FontWeights.Bold;label.Foreground=SettingsTheme.Bone;label.Margin=new(0,0,0,12);content.Children.Add(label);
        // The dedicated preview rail already shows visual changes; avoid duplicate preview drawers.
        var details=new StackPanel();
        foreach(var child in children)
        {
            if(child is Button action){action.HorizontalAlignment=HorizontalAlignment.Left;action.Margin=new(0,8,0,0);}
            if(child is FrameworkElement element && element.Parent is Panel old)old.Children.Remove(child);else if(child is FrameworkElement owned && owned.Parent is Expander expander)expander.Content=null;
            if(child is TextBlock paragraph && paragraph.Text.Length>220 && title!="Clipboard history")details.Children.Add(child);else content.Children.Add(child);
        }
        if(details.Children.Count>0)content.Children.Add(new Expander{Header=Localization.Text("Подробнее")+" · "+Localization.Text(FriendlySection(title)),Content=details,Foreground=UI.Muted,IsExpanded=false,FontSize=11,Margin=new(0,10,0,0)});
        return new Border{Tag=title,Background=Accessibility.HighContrast?SystemColors.WindowBrush:SettingsTheme.Card,BorderBrush=SettingsTheme.LineSoft,BorderThickness=new(1),CornerRadius=new(14),Padding=new(20,17,20,13),Margin=new(0,0,0,14),Child=content};
    }
    private static Expander Advanced(string title,UIElement content)
    {
        if(content is FrameworkElement element && element.Parent is Expander previous)previous.Content=null;
        return new(){Header=Localization.Text("Дополнительно")+" · "+Localization.Text(title),Content=content,Foreground=Brushes.White,Margin=new(0,8,0,0)};
    }
    private void AddSlider(StackPanel target,string label,double value,double min,double max,Action<double> change,string unit="",double step=1)
    {
        string displayUnit=unit.Trim();string localizedUnit=displayUnit.Length>0?Localization.Text(displayUnit):"";
        var row=new Grid{Margin=new(0,5,0,5)};row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.ColumnDefinitions.Add(new(){Width=new GridLength(64)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.RowDefinitions.Add(new(){Height=GridLength.Auto});row.RowDefinitions.Add(new(){Height=GridLength.Auto});var name=UI.Caption(label,12);name.VerticalAlignment=VerticalAlignment.Center;name.Margin=new(0,0,12,0);row.Children.Add(name);var number=UI.Input(label+" value",value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture));number.Padding=new(6,4,6,4);number.Margin=new(0);number.HorizontalContentAlignment=HorizontalAlignment.Right;number.ToolTip=$"{min:0.##} до {max:0.##} {localizedUnit}";Grid.SetColumn(number,2);row.Children.Add(number);var suffix=UI.Text(localizedUnit,10,UI.Muted);suffix.Margin=new(7,0,0,0);suffix.MinWidth=localizedUnit.Length>0?22:0;Grid.SetColumn(suffix,3);row.Children.Add(suffix);target.Children.Add(row);
        var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=step,IsSnapToTickEnabled=true,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(slider);
        bool? inline=null;
        void Arrange(){bool fits=row.ActualWidth<=0 || row.ActualWidth>=380;if(inline==fits)return;inline=fits;Grid.SetRow(slider,fits?0:1);Grid.SetColumn(slider,fits?1:0);Grid.SetColumnSpan(slider,fits?1:4);slider.Width=fits?190:double.NaN;slider.Margin=fits?new Thickness(0,0,12,0):new Thickness(0,8,0,4);}
        Arrange();row.SizeChanged+=(_,_)=>Arrange();UI.LocalizedName(slider,label);slider.ValueChanged+=(_,_)=>{if(!number.IsKeyboardFocusWithin)number.Text=slider.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);change(slider.Value);ConfigurePreviewForSetting(label);if(label is "Ширина панели" or "Макс. высота панели" or "Внутренний отступ" or "Размер содержимого" or "Отступ между секциями" or "Panel width" or "Maximum panel height" or "Content inset" or "Content size" or "Section spacing")previewMode="Panel";else if(label.StartsWith("Preview") || label.StartsWith("Предпросмотр"))previewMode="Hover";else if(label.StartsWith("Closed") || label.StartsWith("Свёрнутый") || label=="Радиус скругления" || label=="Corner radius")previewMode="Closed";Save();};
        WireSettingPreview(slider,label);WireSettingPreview(number,label);
        void Commit(){if(double.TryParse(number.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double n) && double.IsFinite(n)){slider.Value=Math.Clamp(Math.Round(n/step)*step,min,max);number.Text=slider.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);}else{number.Text=slider.Value.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);Accessibility.Announce(status,Localization.Format("Введите число от {0} до {1} {2}.",min.ToString("0.##"),max.ToString("0.##"),unit));}}
        number.LostKeyboardFocus+=(_,_)=>Commit();number.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){Commit();e.Handled=true;}};string help=SettingDescription(label);if(help.Length>0){var hint=UI.Caption(help,11,UI.Muted);hint.Margin=new(0,-2,0,10);target.Children.Add(hint);}
    }
    private CheckBox Check(string name,bool value,Action<bool> change)
    {
        // The switch itself shows On/Off; the label stays a plain name so rows scan quickly.
        var label=new StackPanel{Margin=new(0,7,0,7)};var stateLabel=UI.Text(Localization.Text(name),12);label.Children.Add(stateLabel);
        string help=SettingDescription(name);if(help.Length>0){var description=UI.Caption(help,11,UI.Muted);description.Margin=new(0,4,0,0);label.Children.Add(description);}
        var check=new CheckBox{Content=label,IsChecked=value};UI.LocalizedName(check,name);check.ToolTip=help.Length>0?help:null;
        WireSettingPreview(check,name);check.Click+=(_,_)=> {change(check.IsChecked==true);ConfigurePreviewForSetting(name);Save();};return check;
    }
    private void Appearance()
    {
        var s=app.Settings;
        void Preset(string name){s.Preset=name;s.Top=name=="Mac"?0:Math.Max(8,s.Top);Save();Navigate(current);}
        page.Children.Add(Section("Форма",ShapeChoices(Preset)));
        MaterialControls();var colors=new StackPanel();ColorControl(colors,"Цвет островка",s.Background,v=>s.Background=v,new[]{"#000000","#101114","#182028","#25221E"});ColorControl(colors,"Цвет акцента",s.Accent,v=>s.Accent=v,new[]{"#FFFFFF","#E3D2AF","#A8C6F2","#B7D6BC"});AddSlider(colors,"Непрозрачность",s.Opacity*100,65,100,v=>s.Opacity=v/100,"%",1);page.Children.Add(Section("Цвет и материалы",colors,Check("Тёмное стекло",s.GlassStyle,v=>s.GlassStyle=v),Check("Цвет из обложки альбома",s.AlbumAccent,v=>s.AlbumAccent=v)));
        AmbientSettings();ControlColorSettings();
        var material=new StackPanel();AddSlider(material,"Интенсивность стекла",s.GlassStrength*100,0,100,v=>s.GlassStrength=v/100,"%");page.Children.Add(Section("Поверхность",material));
        page.Children.Add(Section("Текст и навигация",UI.Caption("Точки обеспечивают быстрый переход между панелями. Наведите курсор на точку, чтобы увидеть её название.",11,UI.Muted)));
        page.Children.Add(Section("Типографика",Check("Жирные заголовки треков",s.BoldMediaTitle,v=>s.BoldMediaTitle=v),Check("Жирные заголовки панелей",s.BoldHeadings,v=>s.BoldHeadings=v),UI.Caption("Выделяйте заголовки без изменения основного текста, таймеров и счетчиков.",11,UI.Muted)));
        var motion=new StackPanel();motion.Children.Add(PremiumChoice("Отклик при взаимодействии",s.InteractionMotion,new[]{"Откл.","Мягкий","Выразительный"},v=>s.InteractionMotion=v));AddSlider(motion,"Длительность анимации",s.AnimationMs,0,600,v=>s.AnimationMs=v," мс",10);
        motion.Children.Add(UI.Caption("Анимации синхронизированы с композитором Windows. Реальная частота кадров зависит от монитора и нагрузки на видеокарту.",11,UI.Muted));
        motion.Children.Add(Check("Живая активность · звук, прогресс и дыхание фокуса",s.LiveActivity,v=>s.LiveActivity=v));
        motion.Children.Add(Check("Уменьшить движение",s.ReduceMotion,v=>s.ReduceMotion=v));page.Children.Add(Section("Анимация",motion));
        var profiles=new[]{("Авто (рекомендуется)","Auto"),("Экономия энергии","Hemat"),("Плавное движение","Maksimal")};
        var performance=new ComboBox();foreach(var profile in profiles)performance.Items.Add(profile.Item1);performance.SelectedIndex=Math.Max(0,Array.FindIndex(profiles,p=>p.Item2==s.Performance));UI.LocalizedName(performance,"Профиль производительности");performance.SelectionChanged+=(_,_)=>{if(performance.SelectedIndex>=0){s.Performance=profiles[performance.SelectedIndex].Item2;Save();}};UpdateFrameStatus();page.Children.Add(Section("Производительность анимаций",UI.Caption("Авто подстраивается под монитор. Экономия энергии снижает частоту кадров; Плавное движение обеспечивает максимальный FPS.",11,UI.Muted),performance,Advanced("Статистика рендеринга",frameStatus)));
    }
    private void UpdateFrameStatus(){if(!IsVisible || WindowState==WindowState.Minimized)return;if(frameStatus.IsVisible)Motion.Text(frameStatus,FrameTelemetry.Status);if((app.CurrentUpdate&RuntimeChange.Settings)!=0){Motion.Text(hotkeyState,app.HotkeyStatus+"\n"+app.VisibilityHotkeyStatus+"\n"+app.TuckHotkeyStatus);Motion.Text(brightnessState,app.BrightnessStatus);}if((app.CurrentUpdate&(RuntimeChange.Media|RuntimeChange.Audio))!=0)RefreshLivePreviewData();if(IsVisible && WindowState!=WindowState.Minimized && (app.CurrentUpdate&(RuntimeChange.Clock|RuntimeChange.Audio|RuntimeChange.System|RuntimeChange.Countdown))!=0)foreach(var frame in previewFrames)frame(previewClock.Elapsed.TotalSeconds);SyncPreviewRendering();}
    private void Interactions()
    {
        var s=app.Settings;
        HoverControls();InteractionFeatureControls();
        ComboBox Choice(string label,string[] labels,string[] values,string currentValue,Action<string> changed)
        {var box=new ComboBox();foreach(var item in labels)box.Items.Add(Localization.Text(item));box.SelectedIndex=Math.Max(0,Array.IndexOf(values,currentValue));UI.Named(box,label);box.SelectionChanged+=(_,_)=>{if(box.SelectedIndex>=0){changed(values[box.SelectedIndex]);Save();}};return box;}
        page.Children.Add(Section("Автоматическая активность",Check("Предпросмотр новых треков и завершающихся сессий",s.SmartActivities,v=>s.SmartActivities=v),Check("Автоматический предпросмотр медиа из браузера",s.BrowserMediaPreviews,v=>s.BrowserMediaPreviews=v),UI.Caption("Отключите, чтобы подавить автоматический предпросмотр из браузера. Управление воспроизведением и наведение на островок продолжают работать.",11,UI.Muted),UI.Caption("Короткие уведомления появляются без перехвата фокуса.",12,UI.Muted)));
        var wheelStep=new StackPanel();AddSlider(wheelStep,"Шаг громкости",s.VolumeStep,1,10,v=>s.VolumeStep=(int)v,"%");
        page.Children.Add(Section("Жесты на островке",UI.Caption("Колёсико мыши",12,UI.Muted),Choice("Действие колёсика",new[]{"Громкость","Переключение вкладок","Откл."},new[]{"volume","pages","none"},s.WheelAction,v=>s.WheelAction=v),UI.Caption("Клик колёсиком",12,UI.Muted),Choice("Действие среднего клика",new[]{"Воспроизведение / пауза","Откл."},new[]{"toggle","none"},s.MiddleAction,v=>s.MiddleAction=v),wheelStep));
        var modifier=Choice("Модификаторы клавиш",new[]{"Ctrl+Alt","Ctrl+Shift","Alt+Shift"},new[]{"Ctrl+Alt","Ctrl+Shift","Alt+Shift"},s.HotkeyModifiers,v=>s.HotkeyModifiers=v);var keys=Enumerable.Range('A',26).Select(c=>((char)c).ToString()).ToArray();
        UpdateFrameStatus();page.Children.Add(Section("Глобальная горячая клавиша",Check("Включить горячую клавишу открытия/закрытия",s.HotkeyEnabled,v=>s.HotkeyEnabled=v),modifier,Choice("Клавиша",keys,keys,s.HotkeyKey,v=>s.HotkeyKey=v),hotkeyState,UI.Caption("По умолчанию: Ctrl+Alt+I. Используйте Ctrl+K для команд, Ctrl+Tab для вкладок и Esc для возврата или закрытия.",12,UI.Muted)));
        VisibilityControls();
    }
    private void ContentSettings()
    {
        var s=app.Settings;FeatureResourceSettings();var widgets=new StackPanel();
        widgets.Children.Add(UI.Caption("Перетаскивайте виджеты для настройки порядка. Включайте или отключайте нужные виджеты.",11,UI.Muted));
        widgets.Children.Add(WidgetOrder(s.Widgets,"widgets",RefreshFeatureUsage));
        page.Children.Add(Section("Включённые виджеты и порядок",widgets));
        var focus=new StackPanel();AddSlider(focus,"Длительность фокуса",s.FocusMinutes,1,240,v=>s.FocusMinutes=(int)v," мин");AddSlider(focus,"Длительность перерыва",s.BreakMinutes,1,120,v=>s.BreakMinutes=(int)v," мин");
        focus.Children.Add(UI.Caption("Новые значения применятся к следующей сессии.",11,UI.Muted));page.Children.Add(Section("Сессии фокуса (Помодоро)",focus));
        HoverPageSettings();
    }
    private void Move<T>(System.Collections.Generic.List<T> list,int index,int delta)
    {int to=index+delta;if(to<0 || to>=list.Count)return;(list[index],list[to])=(list[to],list[index]);Save();Navigate(current);}
    private void LaunchSettings()
    {
        var s=app.Settings;
        page.Children.Add(Section("Отображение запуска",StyleChoice("Внешний вид запуска",s.LaunchStyle,v=>s.LaunchStyle=v)));
        var shortcuts=new StackPanel();shortcuts.Children.Add(UI.CaptionButton("Добавить установленное приложение",()=>new AppPicker(this,shortcut=>
        {
            if(s.Shortcuts.Count>=40){Accessibility.Announce(status,Localization.Text("Можно добавить до 40 ярлыков."));return;}
            s.Shortcuts.Add(shortcut);Save();Navigate(current);
        }).ShowDialog()));
        for(int i=0;i<s.Shortcuts.Count;i++)
        {
            var index=i;var shortcut=s.Shortcuts[i];
            var row=new DockPanel{Margin=new(0,0,0,8)};
            var up=UI.CaptionButton("↑",()=>Move(s.Shortcuts,index,-1));UI.LocalizedName(up,"Вверх");var down=UI.CaptionButton("↓",()=>Move(s.Shortcuts,index,1));UI.LocalizedName(down,"Вниз");var buttons=UI.Row(up,down,UI.CaptionButton("Удалить",()=> {s.Shortcuts.RemoveAt(index);Save();Navigate(current);}));
            ((Button)buttons.Children[0]).IsEnabled=i>0;((Button)buttons.Children[1]).IsEnabled=i<s.Shortcuts.Count-1;
            DockPanel.SetDock(buttons,Dock.Right);row.Children.Add(buttons);
            var detail=new StackPanel();detail.Children.Add(UI.Text(shortcut.Name));var target=UI.Text(shortcut.Target,10,UI.Muted);target.TextWrapping=TextWrapping.NoWrap;target.TextTrimming=TextTrimming.CharacterEllipsis;detail.Children.Add(target);row.Children.Add(detail);shortcuts.Children.Add(row);
        }
        var name=UI.Input("Shortcut name","","например: Браузер");var targetInput=UI.Input("Shortcut destination","","Путь к приложению, папке или ссылка https://");
        shortcuts.Children.Add(UI.Caption("Название ярлыка",12,UI.Muted));shortcuts.Children.Add(name);shortcuts.Children.Add(UI.Caption("Приложение, папка или ссылка",12,UI.Muted));shortcuts.Children.Add(targetInput);
        shortcuts.Children.Add(UI.Row(UI.CaptionButton("Выбрать файл",()=> {var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Приложения и файлы|*.*"};if(dialog.ShowDialog(this)==true){targetInput.Text=dialog.FileName;if(string.IsNullOrWhiteSpace(name.Text))name.Text=System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);}}),UI.CaptionButton("Выбрать папку",()=> {var dialog=new Microsoft.Win32.OpenFolderDialog();if(dialog.ShowDialog(this)==true){targetInput.Text=dialog.FolderName;if(string.IsNullOrWhiteSpace(name.Text))name.Text=System.IO.Path.GetFileName(dialog.FolderName);}})));
        shortcuts.Children.Add(UI.CaptionButton("Добавить ярлык",()=>
        {
            if(string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(targetInput.Text)){Accessibility.Announce(status,Localization.Text("Сначала введите имя и путь."));return;}
            if(s.Shortcuts.Count>=40){Accessibility.Announce(status,Localization.Text("Можно добавить до 40 ярлыков."));return;}
            s.Shortcuts.Add(new(){Name=name.Text.Trim(),Target=targetInput.Text.Trim()});Save();Navigate(current);
        },true));page.Children.Add(Section("Ярлыки быстрого запуска",shortcuts));
    }
    private void MediaAppearanceSettings()
    {
        var s=app.Settings;
        var mediaIcon=new ComboBox();mediaIcon.Items.Add("Волна");mediaIcon.Items.Add("Обложка альбома");mediaIcon.Items.Add("Нет");mediaIcon.SelectedIndex=s.ClosedMediaIcon=="None"?2:s.ClosedMediaIcon=="Artwork"?1:0;UI.LocalizedName(mediaIcon,"Иконка свёрнутого медиа");mediaIcon.SelectionChanged+=(_,_)=>{s.ClosedMediaIcon=mediaIcon.SelectedIndex==2?"None":mediaIcon.SelectedIndex==1?"Artwork":"Waveform";previewMode="Closed";previewContent="Music";Save();};

        var choices=new System.Windows.Controls.Primitives.UniformGrid{Columns=2,Margin=new(0,8,0,8)};
        string[] ids={"Waveform","Lyrics","Title","None"};string[] labels={"Волна","Живой текст","Название трека","Нет"};
        string[] hints={"Следовать за музыкой","Строка в такт песне","Минималистично","Показывать часы или таймер"};
        var buttons=new System.Collections.Generic.List<Button>();
        void RefreshChoices(){for(int i=0;i<buttons.Count;i++){bool selected=s.ClosedMediaContent==ids[i];buttons[i].Background=UI.Brush(selected?s.SelectionFill:s.ControlFill);if(buttons[i].Content is StackPanel body && body.Children[0] is TextBlock title)title.Text=labels[i]+(selected?" · Выбрано":"");}}
        for(int i=0;i<ids.Length;i++)
        {
            int index=i;var content=new StackPanel();content.Children.Add(UI.Text(labels[i],14));var hint=UI.Text(hints[i],12,UI.Muted);hint.Margin=new(0,5,0,0);content.Children.Add(hint);
            var button=UI.Button(labels[i],()=>{s.ClosedMediaContent=ids[index];RefreshChoices();previewMode="Closed";previewContent="Music";Save();});button.Content=content;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new(14);button.Margin=new(0,0,8,8);button.MinHeight=76;choices.Children.Add(button);buttons.Add(button);
        }
        RefreshChoices();UI.LocalizedName(choices,"Отображение свёрнутого медиа");
        page.Children.Add(Section("Отображение свёрнутого медиа",choices,UI.Caption("При отсутствии текста отображается звуковая волна. При воспроизведении данные трека синхронизируются с LRCLIB.",12,UI.Muted),UI.Caption("Иконка медиа",12,UI.Muted),mediaIcon));
    }
    private void DisplaySettings()
    {
        var s=app.Settings;
        var mode=new ComboBox{ItemsSource=new[]{"Основной экран","Выбранный экран","Следовать за курсором"},SelectedIndex=s.DisplayMode=="Follow"?2:s.DisplayMode=="Selected"?1:0};UI.LocalizedName(mode,"Режим отображения");
        mode.SelectionChanged+=(_,_)=>{s.DisplayMode=mode.SelectedIndex==2?"Follow":mode.SelectedIndex==1?"Selected":"Primary";Save();};
        var monitors=new ComboBox();var screens=Forms.Screen.AllScreens;
        monitors.Items.Add("Основной монитор (авто)");foreach(var screen in screens) monitors.Items.Add(screen.DeviceName+" · "+screen.Bounds.Width+" × "+screen.Bounds.Height);
        monitors.SelectedIndex=Array.FindIndex(screens,m=>m.DeviceName==s.Monitor)+1;UI.LocalizedName(monitors,"Монитор островка");
        monitors.SelectionChanged+=(_,_)=> {s.Monitor=monitors.SelectedIndex>0?screens[monitors.SelectedIndex-1].DeviceName:"";s.DisplayMode=monitors.SelectedIndex>0?"Selected":"Primary";mode.SelectedIndex=s.DisplayMode=="Selected"?1:0;Save();};
        var follow=new StackPanel();follow.Children.Add(Check("Плавно перемещаться между мониторами",s.FollowMonitorAnimation,v=>s.FollowMonitorAnimation=v));AddSlider(follow,"Длительность перехода",s.FollowMonitorDuration,180,700,v=>s.FollowMonitorDuration=v," мс",10);
        page.Children.Add(Section("Монитор и экран",mode,monitors,follow,UI.Text(s.Monitor.Length>0 && !screens.Any(screen=>screen.DeviceName==s.Monitor)?"Сохранённый монитор недоступен: "+s.Monitor+" · используется основной":"Выбранный монитор сохранён.",11,UI.Muted),UI.Caption("«Основной» использует главный монитор. «Выбранный» закрепляет островок на конкретном мониторе. «Следовать за курсором» перемещает островок при смене активного монитора.",11,UI.Muted)));
    }
    private void General()
    {
        LanguageAndAccessibilitySettings();
        var s=app.Settings;
        UpdatesSection();DiagnosticsSection();NotificationSettings();
        var startup=new CheckBox{Content=Localization.Text("Запускать автоматически при входе в Windows"),IsChecked=s.AutoStart};
        UI.LocalizedName(startup,"Запускать автоматически при входе в Windows");startup.Click+=(_,_)=>
        {
            try {StartupService.Set(startup.IsChecked==true);s.AutoStart=startup.IsChecked==true;Save();}
            catch(Exception ex) {startup.IsChecked=StartupService.Enabled();s.AutoStart=startup.IsChecked==true;status.Text="Не удалось изменить автозагрузку: "+ex.Message;app.Store.Log(ex);}
        };
        page.Children.Add(Section("Поведение",UI.CaptionButton("Открыть руководство по настройке",app.ShowWelcome),startup,UI.Caption("Запуск NabyScape для текущего пользователя Windows при входе в систему. Можно отключить в любое время.",11,UI.Muted),UI.Caption("NabyScape продолжает работать при закрытии окна настроек. Выйти можно через значок в трее.",12,UI.Muted)));
        page.Children.Add(Section("Локальное хранилище",UI.Caption("Настройки, таймеры и заметки хранятся на этом устройстве. Учётная запись не требуется.",12,UI.Muted),UI.CaptionButton("Открыть папку настроек",()=>ShortcutService.Open(new(){Target=app.Store.DirectoryPath})),UI.CaptionButton("Удалить NabyScape…",()=>{try{Installer.OpenUninstaller();}catch(Exception ex){app.Notify("Удаление NabyScape",ex.Message);}}),UI.Caption("Удаление очищает кэш, историю и сохранённые настройки программы.",11,UI.Muted),UI.CaptionButton("Выйти из NabyScape",app.Quit)));
        var resetCategory=new ComboBox{ItemsSource=PreferenceReset.Categories.Where(pair=>pair.Value.Length>0).Select(pair=>CategoryTitle(pair.Key)).ToArray(),SelectedIndex=0};UI.LocalizedName(resetCategory,"Категория для сброса");
        page.Children.Add(Section("Сброс настроек",UI.Text(PreferenceReset.Summary(null),11,UI.Muted),UI.Caption("Выберите категорию, чтобы сбросить только её параметры; например, «Цвета и тема» не затронет разметку.",11,UI.Muted),resetCategory,UI.CaptionButton("Сбросить эту категорию",()=>ResetPreferences(CategoryId(resetCategory.SelectedItem?.ToString()??"Цвета и тема"))),UI.CaptionButton("Сбросить все настройки",()=>ResetPreferences(null)),UI.CaptionButton("Восстановить из резервной копии",RestorePreferences)));
        page.Children.Add(Section("О программе NabyScape",UI.Text("Версия "+typeof(App).Assembly.GetName().Version!.ToString(3)+" · Windows · "+RuntimePlatform.Rid,14),UI.Caption("Управление медиа зависит от поддержки в вашем плеере. Виджет батареи отображается только на устройствах с аккумулятором.",12,UI.Muted),UI.Caption("Сессии фокуса работают локально в NabyScape и не меняют режим «Не беспокоить» Windows.",12,UI.Muted)));
    }
}
