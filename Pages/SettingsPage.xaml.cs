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
    private readonly PetFSM _fsm;
    private readonly ZolakConfig _config;
    private readonly GameLoopManager _gameLoop;
    private bool _initialized;

    private const string AppName = "ZolakPet";

    public SettingsPage(PetFSM fsm, ZolakConfig config, GameLoopManager gameLoop)
    {
        _fsm = fsm;
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

        // Set current
        CharacterCombo.SelectedItem = _config.ActiveCharacter;
        UpdateDeleteButtonVisibility(_config.ActiveCharacter);
        _initialized = true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CHARACTER
    // ═══════════════════════════════════════════════════════════════════

    private readonly string[] _protectedChars = { "Ahmad", "Karkoor", "Za3tar" };

    private void UpdateDeleteButtonVisibility(string charName)
    {
        bool isProtected = _protectedChars.Contains(charName, StringComparer.OrdinalIgnoreCase);
        DeleteCharBtn.Visibility = isProtected ? Visibility.Collapsed : Visibility.Visible;
    }

    private void CharacterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || CharacterCombo.SelectedItem is not string name) return;
        AssetManager.Instance.SetCharacter(name);
        _config.ActiveCharacter = name;
        Save();

        UpdateDeleteButtonVisibility(name);

        // Update status bar
        var cpWindow = Window.GetWindow(this) as ControlPanelWindow;
        cpWindow?.UpdateCharacterStatus();
        cpWindow?.ShowStatusMessage($"Switched to {name}");
    }

    private void AddCharBtn_Click(object sender, RoutedEventArgs e)
    {
        NewCharPanel.Visibility = Visibility.Visible;
        NewCharNameBox.Text = "";
        NewCharNameBox.Focus();
    }

    private void CancelChar_Click(object sender, RoutedEventArgs e)
    {
        NewCharPanel.Visibility = Visibility.Collapsed;
        NewCharNameBox.Text = "";
    }

    private async void DeleteCharBtn_Click(object sender, RoutedEventArgs e)
    {
        if (CharacterCombo.SelectedItem is not string charName) return;
        var cpWindow = Window.GetWindow(this) as ControlPanelWindow;

        // Double check protection
        if (_protectedChars.Contains(charName, StringComparer.OrdinalIgnoreCase))
        {
            cpWindow?.ShowStatusMessage("Cannot delete original characters");
            return;
        }

        cpWindow?.ShowStatusMessage($"Deleting '{charName}'...");
        _gameLoop.Stop();

        try
        {
            await Task.Run(() =>
            {
                string assetsRoot = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Assets", "sprites");
                string charDir = System.IO.Path.Combine(assetsRoot, charName);
                if (System.IO.Directory.Exists(charDir))
                {
                    System.IO.Directory.Delete(charDir, true);
                }

                AssetManager.Instance.LoadAll(assetsRoot);
            });

            // Revert to Ahmad or first available
            string fallback = _protectedChars[0]; // Ahmad
            AssetManager.Instance.SetCharacter(fallback);
            _config.ActiveCharacter = fallback;
            Save();

            _gameLoop.ResetAnimation();
            _fsm.Reset();

            // Refresh UI
            CharacterCombo.Items.Clear();
            foreach (var name in AssetManager.Instance.GetAvailableCharacters())
                CharacterCombo.Items.Add(name);
            CharacterCombo.SelectedItem = fallback;

            cpWindow?.UpdateCharacterStatus();
            cpWindow?.ShowStatusMessage($"Deleted '{charName}' and reverted to {fallback}");
        }
        catch (Exception ex)
        {
            cpWindow?.ShowStatusMessage($"Error deleting: {ex.Message}");
        }
        finally
        {
            _gameLoop.Start();
        }
    }

    private async void ConfirmChar_Click(object sender, RoutedEventArgs e)
    {
        string charName = NewCharNameBox.Text.Trim();
        var cpWindow = Window.GetWindow(this) as ControlPanelWindow;

        if (string.IsNullOrEmpty(charName))
        {
            cpWindow?.ShowStatusMessage("Please enter a character name");
            return;
        }

        // Check for invalid filename chars
        if (charName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            cpWindow?.ShowStatusMessage("Name contains invalid characters");
            return;
        }

        string assetsRoot = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "sprites");
        string newCharDir = System.IO.Path.Combine(assetsRoot, charName);

        if (System.IO.Directory.Exists(newCharDir))
        {
            cpWindow?.ShowStatusMessage($"'{charName}' already exists");
            return;
        }

        // ─── START SAFE RELOAD ──────────────────────────────────────────
        cpWindow?.ShowStatusMessage($"Creating '{charName}'...");
        _gameLoop.Stop();
        NewCharPanel.IsEnabled = false;

        try
        {
            // 1. Clone folders in background thread to avoid UI lag
            await Task.Run(() =>
            {
                string templateDir = System.IO.Path.Combine(assetsRoot, "Karkoor");
                if (!System.IO.Directory.Exists(templateDir))
                {
                    var chars = AssetManager.Instance.GetAvailableCharacters();
                    if (chars.Count > 0)
                        templateDir = System.IO.Path.Combine(assetsRoot, chars[0]);
                }

                if (System.IO.Directory.Exists(templateDir))
                {
                    CopyDirectory(templateDir, newCharDir);
                }
                else
                {
                    System.IO.Directory.CreateDirectory(newCharDir);
                }

                // 2. Reload assets in background
                AssetManager.Instance.LoadAll(assetsRoot);
            });

            // 3. Switch and reset (back on UI thread)
            AssetManager.Instance.SetCharacter(charName);
            _config.ActiveCharacter = charName;
            Save();

            _gameLoop.ResetAnimation();
            _fsm.Reset(); // Ensure FSM is back to Idle

            // Update UI
            NewCharPanel.Visibility = Visibility.Collapsed;
            NewCharNameBox.Text = "";
            CharacterCombo.Items.Clear();
            foreach (var name in AssetManager.Instance.GetAvailableCharacters())
                CharacterCombo.Items.Add(name);
            CharacterCombo.SelectedItem = charName;

            cpWindow?.UpdateCharacterStatus();
            cpWindow?.ShowStatusMessage($"Created '{charName}' from template");
        }
        catch (Exception ex)
        {
            cpWindow?.ShowStatusMessage($"Error: {ex.Message}");
        }
        finally
        {
            NewCharPanel.IsEnabled = true;
            _gameLoop.Start();
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        System.IO.Directory.CreateDirectory(destDir);
        foreach (var file in System.IO.Directory.GetFiles(sourceDir))
        {
            string destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
            System.IO.File.Copy(file, destFile, true);
        }
        foreach (var dir in System.IO.Directory.GetDirectories(sourceDir))
        {
            string destSubDir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
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

    // ═══════════════════════════════════════════════════════════════════
    //  FACTORY RESET
    // ═══════════════════════════════════════════════════════════════════

    private int _resetClicks = 0;
    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _resetClicks++;
        var cpWindow = Window.GetWindow(this) as ControlPanelWindow;

        if (_resetClicks < 2)
        {
            ResetBtn.Content = "Click again to confirm Reset!";
            cpWindow?.ShowStatusMessage("⚠️ Are you sure? All custom pets will be deleted.");
            return;
        }

        // --- EXECUTE RESET ---
        _gameLoop.Stop();
        cpWindow?.ShowStatusMessage("Performing factory reset...");

        try
        {
            // 1. Reset Config
            var defaults = ConfigManager.CreateDefault();
            // Preserve theme preference
            defaults.Theme = _config.Theme;
            defaults.ActiveCharacter = "Ahmad"; // Ensure default
            ConfigManager.Save(defaults);

            // 2. Clear Custom Assets
            string assetsRoot = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "sprites");
            var protectedChars = new[] { "Ahmad", "Karkoor", "Za3tar" };

            foreach (var dir in System.IO.Directory.GetDirectories(assetsRoot))
            {
                string name = System.IO.Path.GetFileName(dir);
                if (!protectedChars.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    System.IO.Directory.Delete(dir, true);
                }
            }

            // 3. Reload App State
            AssetManager.Instance.LoadAll(assetsRoot);
            AssetManager.Instance.SetCharacter("Ahmad");
            _fsm.Reset();
            _fsm.LoadConfig(defaults);
            _gameLoop.ResetAnimation();

            // 4. Force UI Restart (Simulated)
            cpWindow?.ShowStatusMessage("✓ Factory reset complete.");
            
            // Navigate away and back to refresh everything
            cpWindow?.NavigateToPage(new SettingsPage(_fsm, defaults, _gameLoop));
        }
        catch (Exception ex)
        {
            cpWindow?.ShowStatusMessage($"Error during reset: {ex.Message}");
        }
        finally
        {
            _resetClicks = 0;
            ResetBtn.Content = "Factory Reset";
            _gameLoop.Start();
        }
    }
}
