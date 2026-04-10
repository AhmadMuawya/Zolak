using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WaysToSnooze.Zolak.Pages;

/// <summary>
/// Frame sequence editor for a single state.
/// Allows reordering, adding, and deleting individual animation frames.
/// Live preview animates with the current frame sequence.
/// </summary>
public partial class StateEditorPage : Page
{
    private readonly PetFSM _fsm;
    private readonly ZolakConfig _config;
    private readonly string _stateName;

    // Frame file paths (working copy – persisted on Save)
    private readonly List<string> _framePaths = new();
    private readonly List<BitmapImage> _frameImages = new();

    // Live preview
    private readonly DispatcherTimer _previewTimer;
    private int _previewIndex;

    public StateEditorPage(PetFSM fsm, ZolakConfig config, string stateName)
    {
        _fsm = fsm;
        _config = config;
        _stateName = stateName;

        InitializeComponent();
        StateTitleText.Text = stateName;

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _previewTimer.Tick += PreviewTimer_Tick;

        Loaded += (_, _) => { LoadFrames(); BuildTiles(); StartPreview(); };
        Unloaded += (_, _) => _previewTimer.Stop();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LOAD FRAMES FROM DISK
    // ═══════════════════════════════════════════════════════════════════

    private void LoadFrames()
    {
        _framePaths.Clear();
        _frameImages.Clear();

        string stateDir = GetStateDirectory();
        if (!Directory.Exists(stateDir)) return;

        var files = Directory.GetFiles(stateDir, "*.png")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToArray();

        foreach (var file in files)
        {
            _framePaths.Add(file);
            _frameImages.Add(LoadBitmap(file));
        }

        FrameCountLabel.Text = $"{_framePaths.Count} frames";
    }

    private string GetStateDirectory()
    {
        string assetsRoot = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "sprites");
        return Path.Combine(assetsRoot, AssetManager.Instance.CurrentCharacter, _stateName);
    }

    private static BitmapImage LoadBitmap(string filePath)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(Path.GetFullPath(filePath), UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BUILD FRAME TILES
    // ═══════════════════════════════════════════════════════════════════

    private void BuildTiles()
    {
        TilesPanel.Children.Clear();

        for (int i = 0; i < _frameImages.Count; i++)
        {
            int index = i; // capture for closures
            var tile = new Border
            {
                Style = (Style)FindResource("StateCard"),
                Width = 100,
                Height = 120,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = index,
                AllowDrop = true
            };

            var overlay = new Grid();

            var stack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Frame image
            var img = new Image
            {
                Width = 48, Height = 48,
                Source = _frameImages[i],
                Margin = new Thickness(0, 0, 0, 6)
            };

            // Frame number
            var numLabel = new TextBlock
            {
                Text = $"Frame {i + 1}",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimary"),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Filename
            var fileLabel = new TextBlock
            {
                Text = Path.GetFileName(_framePaths[i]),
                FontSize = 8,
                Foreground = (Brush)FindResource("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 85
            };

            stack.Children.Add(img);
            stack.Children.Add(numLabel);
            stack.Children.Add(fileLabel);

            // Delete button (top-right)
            var deleteBtn = new Button
            {
                Style = (Style)FindResource("VsGhostButton"),
                Content = "✕",
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -2, -2, 0),
                Padding = new Thickness(3, 1, 3, 1),
                Tag = index,
                Foreground = (Brush)FindResource("DangerColor")
            };
            deleteBtn.Click += DeleteFrame_Click;

            overlay.Children.Add(stack);
            overlay.Children.Add(deleteBtn);
            tile.Child = overlay;

            // Drag start
            tile.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 1)
                {
                    DragDrop.DoDragDrop(tile, index, System.Windows.DragDropEffects.Move);
                    e.Handled = true;
                }
            };

            TilesPanel.Children.Add(tile);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DRAG AND DROP
    // ═══════════════════════════════════════════════════════════════════

    private void TilesPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void TilesPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int))) return;
        int draggedIndex = (int)e.Data.GetData(typeof(int));

        // Find drop position
        var dropPos = e.GetPosition(TilesPanel);
        int dropIndex = _frameImages.Count - 1;

        for (int i = 0; i < TilesPanel.Children.Count; i++)
        {
            var child = (FrameworkElement)TilesPanel.Children[i];
            var transform = child.TransformToAncestor(TilesPanel);
            var topLeft = transform.Transform(new Point(0, 0));
            var centerX = topLeft.X + child.ActualWidth / 2;

            if (dropPos.X < centerX && dropPos.Y < topLeft.Y + child.ActualHeight)
            {
                dropIndex = i;
                break;
            }
        }

        if (draggedIndex == dropIndex) return;

        // Reorder
        var movedPath = _framePaths[draggedIndex];
        var movedImage = _frameImages[draggedIndex];

        _framePaths.RemoveAt(draggedIndex);
        _frameImages.RemoveAt(draggedIndex);

        dropIndex = Math.Min(dropIndex, _framePaths.Count);
        _framePaths.Insert(dropIndex, movedPath);
        _frameImages.Insert(dropIndex, movedImage);

        BuildTiles();
        RestartPreview();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LIVE PREVIEW
    // ═══════════════════════════════════════════════════════════════════

    private void StartPreview()
    {
        _previewIndex = 0;
        if (_frameImages.Count > 0)
            PreviewImage.Source = _frameImages[0];
        _previewTimer.Start();
    }

    private void RestartPreview()
    {
        _previewTimer.Stop();
        _previewIndex = 0;
        FrameCountLabel.Text = $"{_frameImages.Count} frames";
        if (_frameImages.Count > 0)
            PreviewImage.Source = _frameImages[0];
        _previewTimer.Start();
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (_frameImages.Count == 0) return;
        _previewIndex = (_previewIndex + 1) % _frameImages.Count;
        PreviewImage.Source = _frameImages[_previewIndex];
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ACTIONS
    // ═══════════════════════════════════════════════════════════════════

    private void AddFrame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Add frames to {_stateName}",
            Filter = "PNG Images|*.png",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            string stateDir = GetStateDirectory();
            if (!Directory.Exists(stateDir))
                Directory.CreateDirectory(stateDir);

            foreach (var sourceFile in dialog.FileNames)
            {
                // Copy to state folder with a unique name
                string destName = $"{_stateName}_{_framePaths.Count + 1:D3}.png";
                string destPath = Path.Combine(stateDir, destName);

                // Avoid overwriting
                int counter = 1;
                while (File.Exists(destPath))
                {
                    destName = $"{_stateName}_{_framePaths.Count + counter:D3}.png";
                    destPath = Path.Combine(stateDir, destName);
                    counter++;
                }

                File.Copy(sourceFile, destPath);
                _framePaths.Add(destPath);
                _frameImages.Add(LoadBitmap(destPath));
            }

            BuildTiles();
            RestartPreview();
        }
    }

    private void DeleteFrame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int idx && idx < _framePaths.Count)
        {
            string deletedName = Path.GetFileName(_framePaths[idx]);
            try { File.Delete(_framePaths[idx]); } catch { /* ignore */ }
            _framePaths.RemoveAt(idx);
            _frameImages.RemoveAt(idx);
            BuildTiles();
            RestartPreview();

            var cpWindow = Window.GetWindow(this) as ControlPanelWindow;
            cpWindow?.ShowStatusMessage($"Deleted {deletedName}");
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        _previewTimer.Stop();

        string stateDir = GetStateDirectory();
        if (!Directory.Exists(stateDir))
            Directory.CreateDirectory(stateDir);

        // Rename all frames to enforce the new order: 001.png, 002.png, ...
        // First, move to temp names to avoid collisions
        var tempPaths = new List<string>();
        for (int i = 0; i < _framePaths.Count; i++)
        {
            string tempName = $"_tmp_{i:D3}.png";
            string tempPath = Path.Combine(stateDir, tempName);
            if (File.Exists(_framePaths[i]) && _framePaths[i] != tempPath)
            {
                File.Move(_framePaths[i], tempPath, true);
            }
            tempPaths.Add(tempPath);
        }

        // Now rename to final names
        _framePaths.Clear();
        for (int i = 0; i < tempPaths.Count; i++)
        {
            string finalName = $"{AssetManager.Instance.CurrentCharacter}_{_stateName}_{i + 1:D3}.png";
            string finalPath = Path.Combine(stateDir, finalName);
            if (File.Exists(tempPaths[i]))
            {
                File.Move(tempPaths[i], finalPath, true);
            }
            _framePaths.Add(finalPath);
        }

        // Reload assets globally
        string assetsRoot = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Assets", "sprites");
        AssetManager.Instance.LoadAll(assetsRoot);

        // Reload our local images
        _frameImages.Clear();
        foreach (var fp in _framePaths)
        {
            if (File.Exists(fp))
                _frameImages.Add(LoadBitmap(fp));
        }

        BuildTiles();
        RestartPreview();

        var cpWindow = Window.GetWindow(this) as ControlPanelWindow;
        cpWindow?.ShowStatusMessage($"✓ Saved {_framePaths.Count} frames for {_stateName}");
    }

    private void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        _previewTimer.Stop();
        var cpWindow = Window.GetWindow(this) as ControlPanelWindow;
        cpWindow?.NavigateToPage(new StatesPage(_fsm, _config));
    }
}
