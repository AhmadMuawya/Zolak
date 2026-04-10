using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WaysToSnooze.Zolak.Pages;

/// <summary>
/// Settings page – configures pet appearance, physics, and startup behavior.
/// All changes persist to zolak-config.json in real-time.
/// </summary>
public partial class SettingsPage : Page
{
    private readonly ZolakConfig _config;
    private readonly GameLoopManager _gameLoop;
    private bool _initialized;

    private const string AppName = "ZolakPet";

    public SettingsPage(ZolakConfig config, GameLoopManager gameLoop)
    {
        _config = config;
        _gameLoop = gameLoop;
        InitializeComponent();
        Loaded += (_, _) => Initialize();
    }

    private void Initialize()
    {
        // ── Character dropdown ──
        CharacterCombo.Items.Clear();
        foreach (var name in AssetManager.Instance.GetAvailableCharacters())
            CharacterCombo.Items.Add(name);
        CharacterCombo.SelectedItem = AssetManager.Instance.CurrentCharacter;

        // ── Pet Size ──
        PetSizeSlider.Value = _config.PetSize;
        PetSizeTextBox.Text = _config.PetSize.ToString();
        PetSizeValueLabel.Text = $"{_config.PetSize}px";

        // ── Animation Speed ──
        AnimSpeedSlider.Value = _config.AnimationSpeed;
        AnimSpeedTextBox.Text = _config.AnimationSpeed.ToString("F1");
        AnimSpeedValueLabel.Text = $"{_config.AnimationSpeed:F1}x";

        // ── Gravity ──
        GravitySlider.Value = _config.Gravity;
        GravityTextBox.Text = _config.Gravity.ToString("F0");
        GravityValueLabel.Text = $"{_config.Gravity:F0} px/s²";

        // ── Bored Threshold ──
        BoredSlider.Value = _config.BoredThresholdMinutes;
        BoredTextBox.Text = _config.BoredThresholdMinutes.ToString("F0");
        BoredValueLabel.Text = $"{_config.BoredThresholdMinutes:F0} min";

        // ── Startup ──
        StartupCheckBox.IsChecked = _config.RunOnStartup;

        _initialized = true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CHARACTER
    // ═══════════════════════════════════════════════════════════════════

    private void CharacterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || CharacterCombo.SelectedItem is not string name) return;
        AssetManager.Instance.SetCharacter(name);
        _config.ActiveCharacter = name;
        Save();

        // Update status bar
        var cpWindow = Window.GetWindow(this) as ControlPanelWindow;
        cpWindow?.UpdateCharacterStatus();
        cpWindow?.ShowStatusMessage($"Switched to {name}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PET SIZE
    // ═══════════════════════════════════════════════════════════════════

    private void PetSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        int val = (int)e.NewValue;
        PetSizeTextBox.Text = val.ToString();
        PetSizeValueLabel.Text = $"{val}px";
        _config.PetSize = val;
        _gameLoop.ReloadConfig(_config);
        Save();
    }

    private void PetSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(PetSizeTextBox.Text, out int val))
        {
            val = Math.Clamp(val, 32, 192);
            PetSizeSlider.Value = val;
        }
        else
        {
            PetSizeTextBox.Text = PetSizeSlider.Value.ToString();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ANIMATION SPEED
    // ═══════════════════════════════════════════════════════════════════

    private void AnimSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        double val = Math.Round(e.NewValue, 1);
        AnimSpeedTextBox.Text = val.ToString("F1");
        AnimSpeedValueLabel.Text = $"{val:F1}x";
        _config.AnimationSpeed = val;
        _gameLoop.ReloadConfig(_config);
        Save();
    }

    private void AnimSpeedTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(AnimSpeedTextBox.Text, out double val))
        {
            val = Math.Clamp(val, 0.5, 3.0);
            AnimSpeedSlider.Value = val;
        }
        else
        {
            AnimSpeedTextBox.Text = AnimSpeedSlider.Value.ToString("F1");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GRAVITY
    // ═══════════════════════════════════════════════════════════════════

    private void GravitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        double val = Math.Round(e.NewValue);
        GravityTextBox.Text = val.ToString("F0");
        GravityValueLabel.Text = $"{val:F0} px/s²";
        _config.Gravity = val;
        _gameLoop.ReloadConfig(_config);
        Save();
    }

    private void GravityTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(GravityTextBox.Text, out double val))
        {
            val = Math.Clamp(val, 200, 2000);
            GravitySlider.Value = val;
        }
        else
        {
            GravityTextBox.Text = GravitySlider.Value.ToString("F0");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BORED THRESHOLD
    // ═══════════════════════════════════════════════════════════════════

    private void BoredSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized) return;
        double val = Math.Round(e.NewValue);
        BoredTextBox.Text = val.ToString("F0");
        BoredValueLabel.Text = $"{val:F0} min";
        _config.BoredThresholdMinutes = val;
        Save();
    }

    private void BoredTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(BoredTextBox.Text, out double val))
        {
            val = Math.Clamp(val, 1, 30);
            BoredSlider.Value = val;
        }
        else
        {
            BoredTextBox.Text = BoredSlider.Value.ToString("F0");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RUN ON STARTUP
    // ═══════════════════════════════════════════════════════════════════

    private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        bool enabled = StartupCheckBox.IsChecked == true;
        _config.RunOnStartup = enabled;
        ToggleStartup(enabled);
        Save();
    }

    private void ToggleStartup(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                string appPath = Environment.ProcessPath ?? string.Empty;
                if (!string.IsNullOrEmpty(appPath))
                    key.SetValue(AppName, $"\"{appPath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Registry access might fail in restricted environments
        }
    }

    private void Save() => ConfigManager.Save(_config);
}
