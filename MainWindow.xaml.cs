using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WaysToSnooze.Zolak.Themes;

namespace WaysToSnooze.Zolak;

/// <summary>
/// The "Body" of the pet – a transparent, borderless, always-on-top WPF window.
/// It wires the FSM + GameLoop together and reflects their output visually.
/// Also hosts a System Tray NotifyIcon for user control and character selection.
/// </summary>
public partial class MainWindow : Window
{
    private readonly PetFSM _fsm = new();
    private readonly GameLoopManager _gameLoop;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _isPaused;
    private bool _isDragging;
    private ZolakConfig _config;
    private ControlPanelWindow? _controlPanel;

    private const string AppName = "ZolakPet";

    public MainWindow()
    {
        InitializeComponent();

        // ── Load Config ────────────────────────────────────────────────
        _config = ConfigManager.Load();

        // ── Apply Theme ────────────────────────────────────────────────
        ThemeController.Initialize(_config.Theme);

        // ── Load Assets ────────────────────────────────────────────────
        string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "sprites");
        AssetManager.Instance.LoadAll(assetsPath);

        // Set active character from config if saved
        if (!string.IsNullOrEmpty(_config.ActiveCharacter))
            AssetManager.Instance.SetCharacter(_config.ActiveCharacter);

        // ── Load FSM config ────────────────────────────────────────────
        _fsm.LoadConfig(_config);

        // ── Game Loop ──────────────────────────────────────────────────
        _gameLoop = new GameLoopManager(_fsm, AssetManager.Instance);
        _gameLoop.OnRender = OnRenderFrame;
        _gameLoop.ReloadConfig(_config);

        // ── Apply pet size ─────────────────────────────────────────────
        Width = _config.PetSize;
        Height = _config.PetSize;
        PetImage.Width = _config.PetSize;
        PetImage.Height = _config.PetSize;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // ── Compute screen bounds ──────────────────────────────────────
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen is not null)
        {
            var workArea = screen.WorkingArea;
            double dpiScaleX = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiScaleY = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            double left  = workArea.Left   / dpiScaleX;
            double right = workArea.Right  / dpiScaleX;
            double floor = workArea.Bottom / dpiScaleY;

            _gameLoop.SetScreenBounds(left, right, floor, _config.PetSize, _config.PetSize);
        }

        // ── System Tray ────────────────────────────────────────────────
        SetupTrayIcon();

        // ── Start ──────────────────────────────────────────────────────
        _gameLoop.Start();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _gameLoop.Stop();
        _trayIcon?.Dispose();
        _controlPanel?.Close();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RENDER CALLBACK (called every frame by GameLoopManager)
    // ═══════════════════════════════════════════════════════════════════

    private void OnRenderFrame(BitmapImage? frame, double x, double y, double scaleX)
    {
        if (frame is not null)
            PetImage.Source = frame;

        // Sync window/image size with config (for live resize from Settings)
        double size = _gameLoop.PetWidth;
        if (Width != size)
        {
            Width = size;
            Height = size;
            PetImage.Width = size;
            PetImage.Height = size;
        }

        // Only move the window when NOT being dragged by the user
        if (!_isDragging)
        {
            Left = x;
            Top = y;
        }

        // Flip direction
        SpriteScale.ScaleX = scaleX;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MOUSE INTERACTION (on the Image control only)
    // ═══════════════════════════════════════════════════════════════════

    private void PetImage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isPaused) return;
        _fsm.OnMouseEnter();
    }

    private void PetImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isPaused) return;
        _fsm.OnMouseLeave();
    }

    private void PetImage_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isPaused) return;

        _isDragging = true;
        _gameLoop.IsDragging = true;

        // DragMove() is blocking – it pumps messages until mouse is released
        DragMove();

        // When DragMove returns, the user has released the mouse.
        _isDragging = false;
        _gameLoop.IsDragging = false;

        // Hand the new position to the game loop and trigger gravity
        _gameLoop.OnDropped(Left, Top);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SYSTEM TRAY (NotifyIcon) with Character Selection
    // ═══════════════════════════════════════════════════════════════════

    private void SetupTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Visible = true,
            Text = "Zolak"
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();

        // ── Control Panel ──────────────────────────────────────────────
        var controlPanelItem = new System.Windows.Forms.ToolStripMenuItem("Control Panel");
        controlPanelItem.Click += (_, _) => OpenControlPanel();
        menu.Items.Add(controlPanelItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── Character submenu ──────────────────────────────────────────
        var characterMenu = new System.Windows.Forms.ToolStripMenuItem("Character");
        RebuildCharacterMenu(characterMenu);
        menu.Items.Add(characterMenu);

        // ── Run on Startup ─────────────────────────────────────────────
        var startupItem = new System.Windows.Forms.ToolStripMenuItem("Run on Startup")
        {
            CheckOnClick = true,
            Checked = IsStartupEnabled()
        };
        startupItem.CheckedChanged += (_, _) => ToggleStartup(startupItem.Checked);
        if(!startupItem.Checked) {
            startupItem.Checked = true;
            ToggleStartup(true);
        }
        menu.Items.Add(startupItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── Pause ──────────────────────────────────────────────────────
        var pauseItem = new System.Windows.Forms.ToolStripMenuItem("Pause");
        pauseItem.Click += (_, _) =>
        {
            _isPaused = !_isPaused;
            pauseItem.Text = _isPaused ? "Resume" : "Pause";
            if (_isPaused) _gameLoop.Stop(); else _gameLoop.Start();
        };
        menu.Items.Add(pauseItem);

        // ── Hide / Show ────────────────────────────────────────────────
        var hideItem = new System.Windows.Forms.ToolStripMenuItem("Hide");
        hideItem.Click += (_, _) =>
        {
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Hidden;
                _gameLoop.Stop();
                hideItem.Text = "Show";
            }
            else
            {
                Visibility = Visibility.Visible;
                if (!_isPaused) _gameLoop.Start();
                hideItem.Text = "Hide";
            }
        };
        menu.Items.Add(hideItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── Exit ───────────────────────────────────────────────────────
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon.Visible = false;
            Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CONTROL PANEL
    // ═══════════════════════════════════════════════════════════════════

    private void OpenControlPanel()
    {
        if (_controlPanel is not null && _controlPanel.IsVisible)
        {
            _controlPanel.Activate();
            return;
        }

        _controlPanel = new ControlPanelWindow(_fsm, _gameLoop, _config);
        _controlPanel.Closed += (_, _) => _controlPanel = null;
        _controlPanel.Show();
    }

    private void RebuildCharacterMenu(System.Windows.Forms.ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();
        var characters = AssetManager.Instance.GetAvailableCharacters();
        string current = AssetManager.Instance.CurrentCharacter;

        foreach (var name in characters)
        {
            var item = new System.Windows.Forms.ToolStripMenuItem(name)
            {
                Checked = name == current,
                CheckOnClick = false
            };
            item.Click += (_, _) =>
            {
                AssetManager.Instance.SetCharacter(name);
                // Update checkmarks
                foreach (System.Windows.Forms.ToolStripMenuItem child in parent.DropDownItems)
                    child.Checked = child.Text == name;
            };
            parent.DropDownItems.Add(item);
        }
    }

    private bool IsStartupEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
        string? val = key?.GetValue(AppName) as string;
        string expectedPath = $"\"{Environment.ProcessPath ?? string.Empty}\"";
        return val != null && val.Equals(expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private void ToggleStartup(bool enable)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
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
}
