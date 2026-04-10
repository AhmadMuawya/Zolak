namespace WaysToSnooze.Zolak.Themes;

/// <summary>
/// Controls runtime theme switching by swapping the merged ResourceDictionary
/// at index 0 of Application.Current.Resources.MergedDictionaries.
/// Persists the user's choice to ZolakConfig.
/// </summary>
public static class ThemeController
{
    public enum ThemeMode { Dark, Light }

    public static ThemeMode CurrentTheme { get; private set; } = ThemeMode.Dark;

    /// <summary>
    /// Initializes the theme from config. Call once on app startup.
    /// </summary>
    public static void Initialize(string themeName)
    {
        var mode = themeName == "Light" ? ThemeMode.Light : ThemeMode.Dark;
        ApplyTheme(mode);
    }

    /// <summary>
    /// Toggles between Dark and Light themes.
    /// </summary>
    public static void Toggle()
    {
        var next = CurrentTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        ApplyTheme(next);
    }

    /// <summary>
    /// Applies the specified theme by swapping the first merged dictionary.
    /// </summary>
    public static void ApplyTheme(ThemeMode mode)
    {
        CurrentTheme = mode;

        string path = mode == ThemeMode.Light
            ? "Themes/Light.xaml"
            : "Themes/Dark.xaml";

        var uri = new Uri(path, UriKind.Relative);
        var dict = new System.Windows.ResourceDictionary { Source = uri };

        var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;

        // Replace the theme dictionary (always at index 0)
        if (mergedDicts.Count > 0)
            mergedDicts[0] = dict;
        else
            mergedDicts.Insert(0, dict);
    }
}
