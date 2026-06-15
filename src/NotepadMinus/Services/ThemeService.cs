using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace NotepadMinus.Services;

/// <summary>
/// v1.5 theme system. Applies one of five palettes (System / Light / Sepia / Dim / Dark)
/// by swapping brushes in the Application resource dictionary. Layout, padding, and corner
/// radii are theme-independent — only colors change. "System" follows the current Windows
/// AppsUseLightTheme setting and updates live when the user changes their Windows theme.
/// </summary>
public static class ThemeService
{
    public enum Theme { System, Light, Sepia, Dim, Dark }

    public static Theme Current { get; private set; } = Theme.System;

    public static event Action<Theme>? Changed;

    public static Theme Parse(string? raw)
        => raw?.ToLowerInvariant() switch
        {
            "light" => Theme.Light,
            "sepia" => Theme.Sepia,
            "dim" => Theme.Dim,
            "dark" => Theme.Dark,
            _ => Theme.System,
        };

    public static void Apply(Theme t)
    {
        Current = t;
        var resolved = t == Theme.System ? (WindowsIsDark() ? Theme.Dark : Theme.Light) : t;
        ApplyPalette(resolved);
        try
        {
            // Mirror the chrome theme into Wpf.Ui so its FluentWindow / TitleBar match.
            var wpfUiTheme = resolved switch
            {
                Theme.Dark or Theme.Dim => Wpf.Ui.Appearance.ApplicationTheme.Dark,
                _ => Wpf.Ui.Appearance.ApplicationTheme.Light,
            };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(wpfUiTheme);
        }
        catch { /* If Wpf.Ui isn't initialized yet, the palette alone is enough. */ }
        Changed?.Invoke(t);
    }

    private static void ApplyPalette(Theme resolved)
    {
        var r = Application.Current?.Resources;
        if (r is null) return;

        // Default (Light) palette
        Color sidebarBg = C("#FAFBFC"),
              sidebarHover = C("#EFF2F6"),
              sidebarSel = C("#E2E8F0"),
              border = C("#E5E7EB"),
              header = C("#F1F4F8"),
              headerFg = C("#1F2A37"),
              chromeBg = C("#FFFFFF"),
              chromeFg = C("#1F2937"),
              editorBg = C("#FFFFFF"),
              editorFg = C("#111827"),
              accent = C("#2563EB");

        switch (resolved)
        {
            case Theme.Sepia:
                chromeBg = C("#F4ECD8"); chromeFg = C("#5B4636");
                sidebarBg = C("#EFE6CE"); sidebarHover = C("#E6DCC0"); sidebarSel = C("#DDD0AE");
                header = C("#E9DFC4"); headerFg = C("#3B2F22");
                editorBg = C("#F8F1DD"); editorFg = C("#3B2F22");
                border = C("#D9CCAE"); accent = C("#9C6B2F");
                break;

            case Theme.Dim:
                chromeBg = C("#2A2F38"); chromeFg = C("#D6DAE2");
                sidebarBg = C("#262B33"); sidebarHover = C("#323844"); sidebarSel = C("#3C4350");
                header = C("#323844"); headerFg = C("#E6E9EE");
                editorBg = C("#2A2F38"); editorFg = C("#E6E9EE");
                border = C("#404654"); accent = C("#7BA7E5");
                break;

            case Theme.Dark:
                chromeBg = C("#1A1B1E"); chromeFg = C("#D6D8DD");
                sidebarBg = C("#15161A"); sidebarHover = C("#22232A"); sidebarSel = C("#2D2F38");
                header = C("#22232A"); headerFg = C("#E6E8EE");
                editorBg = C("#1A1B1E"); editorFg = C("#E6E8EE");
                border = C("#2D2F38"); accent = C("#7BA7E5");
                break;
        }

        Set(r, "ChromeBackground", chromeBg);
        Set(r, "ChromeForeground", chromeFg);
        Set(r, "EditorBackground", editorBg);
        Set(r, "EditorForeground", editorFg);
        Set(r, "SidebarBackground", sidebarBg);
        Set(r, "SidebarHover", sidebarHover);
        Set(r, "SidebarSelected", sidebarSel);
        Set(r, "SubtleBorder", border);
        Set(r, "HeaderBandBackground", header);
        Set(r, "HeaderBandForeground", headerFg);
        Set(r, "AccentBrush", accent);
    }

    private static void Set(ResourceDictionary r, string key, Color c)
        => r[key] = new SolidColorBrush(c) { Opacity = 1.0 };

    private static Color C(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex)!;
        return c;
    }

    public static bool WindowsIsDark()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = k?.GetValue("AppsUseLightTheme");
            if (v is int i) return i == 0;
        }
        catch { }
        return false;
    }
}
