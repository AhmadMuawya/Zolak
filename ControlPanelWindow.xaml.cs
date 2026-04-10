using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WaysToSnooze.Zolak.Pages;
using WaysToSnooze.Zolak.Themes;

namespace WaysToSnooze.Zolak;

/// <summary>
/// VS Code-style control panel window with collapsible sidebar,
/// theme toggle, and page navigation.
/// </summary>
public partial class ControlPanelWindow : Window
{
    private readonly PetFSM _fsm;
    private readonly GameLoopManager _gameLoop;
    private readonly ZolakConfig _config;
    private bool _sidebarExpanded = true;
    private const double SidebarExpandedWidth = 120;
    private const double SidebarCollapsedWidth = 42;

    // Status message auto-clear timer
    private readonly DispatcherTimer _statusTimer;

    // Sun icon path data (used when dark mode → shows "switch to light")
    private const string MoonIconData = "M17.75,4.09L15.22,6.03L16.13,9.09L13.5,7.28L10.87,9.09L11.78,6.03L9.25,4.09L12.44,4L13.5,1L14.56,4L17.75,4.09M21.25,11L19.61,12.25L20.2,14.23L18.5,13.06L16.8,14.23L17.39,12.25L15.75,11L17.81,10.95L18.5,9L19.19,10.95L21.25,11M18.97,15.95C19.8,15.87 20.69,17.05 20.16,17.8C19.84,18.25 19.5,18.67 19.08,19.07C15.17,23 8.84,23 4.94,19.07C1.03,15.17 1.03,8.83 4.94,4.93C5.34,4.53 5.76,4.17 6.21,3.85C6.96,3.32 8.14,4.21 8.06,5.04C7.79,7.9 8.75,10.87 10.95,13.06C13.14,15.26 16.1,16.22 18.97,15.95Z";
    private const string SunIconData = "M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,6.65C6.9,7.16 6.36,7.78 5.93,8.5C5.5,9.24 5.25,10 5.11,10.79L3.34,7M3.36,17L5.12,13.23C5.26,14 5.53,14.78 5.95,15.5C6.37,16.22 6.91,16.85 7.5,17.37L3.36,17M20.65,7L18.88,10.79C18.74,10 18.47,9.23 18.05,8.5C17.63,7.78 17.1,7.15 16.5,6.64L20.65,7M20.64,17L16.5,17.36C17.09,16.85 17.62,16.22 18.05,15.5C18.47,14.78 18.73,14 18.87,13.21L20.64,17M12,22L9.59,18.56C10.33,18.83 11.14,19 12,19C12.82,19 13.63,18.83 14.37,18.56L12,22Z";

    public ControlPanelWindow(PetFSM fsm, GameLoopManager gameLoop, ZolakConfig config)
    {
        _fsm = fsm;
        _gameLoop = gameLoop;
        _config = config;

        InitializeComponent();

        // Status timer for auto-clearing messages
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            StatusMessageText.BeginAnimation(OpacityProperty, fadeOut);
        };

        UpdateCharacterStatus();
        UpdateThemeUI();
        NavigateToStates();

        Loaded += (_, _) => UpdateThemeUI();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STATUS BAR
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows a temporary message in the status bar center. Auto-fades after 3s.
    /// Called by pages instead of MessageBox.
    /// </summary>
    public void ShowStatusMessage(string message)
    {
        _statusTimer.Stop();
        StatusMessageText.Text = message;
        var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
        StatusMessageText.BeginAnimation(OpacityProperty, fadeIn);
        _statusTimer.Start();
    }

    /// <summary>
    /// Updates the character name displayed in the right side of the status bar.
    /// </summary>
    public void UpdateCharacterStatus()
    {
        StatusCharacterText.Text = $"Character: {AssetManager.Instance.CurrentCharacter}";
    }

    private void StatusCharacter_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Cycle to next character
        var characters = AssetManager.Instance.GetAvailableCharacters();
        if (characters.Count <= 1) return;

        int currentIdx = characters.IndexOf(AssetManager.Instance.CurrentCharacter);
        int nextIdx = (currentIdx + 1) % characters.Count;
        string nextChar = characters[nextIdx];

        AssetManager.Instance.SetCharacter(nextChar);
        _config.ActiveCharacter = nextChar;
        ConfigManager.Save(_config);
        UpdateCharacterStatus();
        ShowStatusMessage($"Switched to {nextChar}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TITLE BAR
    // ═══════════════════════════════════════════════════════════════════

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Hide();

    // ═══════════════════════════════════════════════════════════════════
    //  SIDEBAR TOGGLE
    // ═══════════════════════════════════════════════════════════════════

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _sidebarExpanded = !_sidebarExpanded;
        double targetWidth = _sidebarExpanded ? SidebarExpandedWidth : SidebarCollapsedWidth;

        var anim = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        SidebarBorder.BeginAnimation(WidthProperty, anim);

        // Show/hide text labels
        NavStatesLabel.Visibility = _sidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
        NavSettingsLabel.Visibility = _sidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
        ThemeLabel.Visibility = _sidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NAVIGATION
    // ═══════════════════════════════════════════════════════════════════

    private void NavStates_Click(object sender, RoutedEventArgs e)
        => NavigateToStates();

    private void NavSettings_Click(object sender, RoutedEventArgs e)
        => NavigateToSettings();

    private void NavigateToStates()
    {
        NavStatesBtn.Tag = "Active";
        NavSettingsBtn.Tag = null;
        ContentFrame.Navigate(new StatesPage(_fsm, _config));
    }

    private void NavigateToSettings()
    {
        NavStatesBtn.Tag = null;
        NavSettingsBtn.Tag = "Active";
        ContentFrame.Navigate(new SettingsPage(_config, _gameLoop));
    }

    /// <summary>
    /// Called from pages that need to navigate (e.g., States → StateEditor).
    /// </summary>
    public void NavigateToPage(object page)
    {
        ContentFrame.Navigate(page);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  THEME TOGGLE
    // ═══════════════════════════════════════════════════════════════════

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeController.Toggle();
        _config.Theme = ThemeController.CurrentTheme.ToString();
        ConfigManager.Save(_config);
        UpdateThemeUI();
    }

    private void UpdateThemeUI()
    {
        bool isDark = ThemeController.CurrentTheme == ThemeController.ThemeMode.Dark;
        ThemeLabel.Text = isDark ? "Dark Mode" : "Light Mode";

        var iconData = isDark ? MoonIconData : SunIconData;
        ThemeIcon.Data = Geometry.Parse(iconData);
    }
}
