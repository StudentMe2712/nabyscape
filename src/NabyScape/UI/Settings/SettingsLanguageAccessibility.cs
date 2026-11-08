using System;
using System.Windows;
using System.Windows.Controls;

namespace NabyScape;

public sealed partial class SettingsWindow
{
    internal void LanguageAndAccessibilitySettings()
    {
        var settings = app.Settings;
        settings.NormalizeLanguageAccessibility();
        var codes = new[] { "ru", "en", "es", "id" };
        var language = new ComboBox { ItemsSource = new[] { "Русский", "English", "Español", "Bahasa Indonesia" },
            SelectedIndex = Math.Max(0, Array.IndexOf(codes, settings.UiLanguage)) };
        UI.LocalizedName(language, "Язык интерфейса");
        var scale = new ComboBox { ItemsSource = new[] { "100%", "110%", "125%", "150%" } };
        var sizes = new[] { 1d, 1.1, 1.25, 1.5 };
        scale.SelectedIndex = Array.FindIndex(sizes, value => value >= settings.AccessibilityTextScale);
        UI.LocalizedName(scale, "Размер текста");
        var contrast = new CheckBox { Content = UI.Caption("Высокая контрастность"), IsChecked = settings.AccessibilityHighContrast };
        UI.LocalizedName(contrast, "Высокая контрастность");
        var notice = UI.Caption("Сохраните настройки, затем перезапустите NabyScape для их применения.", 12, UI.Muted);
        Accessibility.Live(notice);
        var save = UI.CaptionButton("Сохранить параметры", () =>
        {
            var previous = (settings.UiLanguage, settings.AccessibilityTextScale, settings.AccessibilityHighContrast);
            settings.UiLanguage = codes[Math.Max(0, language.SelectedIndex)];
            settings.AccessibilityTextScale = sizes[Math.Max(0, scale.SelectedIndex)];
            settings.AccessibilityHighContrast = contrast.IsChecked == true;
            settings.NormalizeLanguageAccessibility();
            try
            {
                app.Store.Save("settings", settings);
                app.ChangeSettings();
                Accessibility.Announce(notice, Localization.Text("Сохранено. Перезапустите NabyScape для применения настроек языка и масштаба текста."));
            }
            catch (Exception ex)
            {
                (settings.UiLanguage, settings.AccessibilityTextScale, settings.AccessibilityHighContrast) = previous;
                app.Store.Log(ex);
                Accessibility.Announce(notice, Localization.Text("Не удалось сохранить изменения. Попробуйте снова."));
            }
        }, true);
        page.Children.Add(Section("Язык и доступность", UI.Caption("Язык интерфейса", 12), language,
            UI.Caption("Русский язык установлен по умолчанию. Ваши пользовательские заметки сохраняются без изменений.", 11, UI.Muted),
            UI.Caption("Размер текста", 12), scale, contrast,
            UI.Caption("Высокая контрастность Windows поддерживается автоматически. Увеличенный текст может потребовать прокрутки.", 11, UI.Muted),
            Check("Уменьшить движение", settings.ReduceMotion, value => settings.ReduceMotion = value),
            UI.Caption("Анимации также согласуются с системными настройками Windows.", 11, UI.Muted), save, notice));
    }
}
