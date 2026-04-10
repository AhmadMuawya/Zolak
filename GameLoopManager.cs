using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WaysToSnooze.Zolak;

/// <summary>
/// Runs the 60 fps game loop. Each tick performs:
///   1. Update – advances FSM, moves pet coordinates, increments frame index.
///   2. Render – pushes the computed frame and position to the UI callback.
/// Now supports runtime config reloading for pet size, animation speed, and gravity.
/// </summary>
public class GameLoopManager
{
    // ── Dependencies ───────────────────────────────────────────────────
    private readonly PetFSM _fsm;
    private readonly AssetManager _assets;

    // ── Position / Physics ─────────────────────────────────────────────
    public double X { get; set; }
    public double Y { get; set; }

    private double _floorY;            // computed taskbar-aware floor
    private double _screenLeft;
    private double _screenRight;
    private double _petWidth = 64;
    private double _petHeight = 64;
    private bool _isFalling;
    private double _gravity = 800.0;   // px/s² (configurable)
    private double _velocityY;

    /// <summary>
    /// Set by MainWindow during DragMove. When true, loop skips position
    /// updates but still advances animation so the sprite stays visible.
    /// </summary>
    public bool IsDragging { get; set; }

    // ── Animation ──────────────────────────────────────────────────────
    private int _frameIndex;
    private double _frameTimer;
    private double _frameDuration = 0.12; // seconds per sprite frame (configurable)
    private PetState _lastState;

    // ── Timer ──────────────────────────────────────────────────────────
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;

    // ── Render callback ────────────────────────────────────────────────
    /// <summary>
    /// The MainWindow sets this. Called every tick with (image, x, y, scaleX).
    /// scaleX is 1 for right-facing, -1 for left-facing.
    /// </summary>
    public Action<BitmapImage?, double, double, double>? OnRender { get; set; }

    // ── Constructor ────────────────────────────────────────────────────
    public GameLoopManager(PetFSM fsm, AssetManager assets)
    {
        _fsm = fsm;
        _assets = assets;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 fps
        };
        _timer.Tick += Tick;
    }

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>
    /// Must be called once before Start() to configure the pet's world boundaries.
    /// </summary>
    public void SetScreenBounds(double left, double right, double floorY, double petWidth, double petHeight)
    {
        _screenLeft = left;
        _screenRight = right;
        _floorY = floorY;
        _petWidth = petWidth;
        _petHeight = petHeight;

        // Start the pet on the floor, centered
        X = (left + right) / 2.0 - petWidth / 2.0;
        Y = floorY - petHeight;
    }

    /// <summary>
    /// Reloads configurable values from ZolakConfig at runtime.
    /// Called by the Settings page for live preview.
    /// </summary>
    public void ReloadConfig(ZolakConfig config)
    {
        _petWidth = config.PetSize;
        _petHeight = config.PetSize;
        _gravity = config.Gravity;

        // Animation speed: higher = faster = shorter frame duration
        _frameDuration = 0.12 / Math.Max(config.AnimationSpeed, 0.1);

        // Recalculate floor position with new pet size
        Y = _floorY - _petHeight;
    }

    public void Start()
    {
        _lastTick = DateTime.UtcNow;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    /// <summary>
    /// Resets animation state to avoid frame index mismatch during reloads.
    /// </summary>
    public void ResetAnimation()
    {
        _frameIndex = 0;
        _frameTimer = 0;
        _lastState = PetState.Idle; // default to reset
    }

    /// <summary>
    /// Called from MainWindow after DragMove ends to trigger gravity.
    /// </summary>
    public void OnDropped(double newX, double newY)
    {
        X = newX;
        Y = newY;
        _velocityY = 0;
        _isFalling = true;
    }

    /// <summary>
    /// Exposes current pet dimensions for MainWindow to resize the window/image.
    /// </summary>
    public double PetWidth => _petWidth;
    public double PetHeight => _petHeight;

    // ── Core Loop ──────────────────────────────────────────────────────

    private void Tick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        // Clamp to avoid huge jumps when window is minimised
        if (dt > 0.1) dt = 0.016;

        // ─── UPDATE ────────────────────────────────────────────────────

        // 1. FSM Update (always, even while dragging, to keep timers alive)
        _fsm.Update(dt, X, _screenLeft, _screenRight, _petWidth);

        // 2 & 3. Skip movement + gravity while the user is dragging
        if (!IsDragging)
        {
            // 2. Horizontal movement
            if (_fsm.IsMoving && !_isFalling)
            {
                double speed = _fsm.GetSpeed();
                X += _fsm.Direction * speed * dt;

                // Clamp to screen
                if (X < _screenLeft) X = _screenLeft;
                if (X + _petWidth > _screenRight) X = _screenRight - _petWidth;
            }

            // 3. Gravity
            if (_isFalling)
            {
                _velocityY += _gravity * dt;
                Y += _velocityY * dt;

                double groundY = _floorY - _petHeight;
                if (Y >= groundY)
                {
                    Y = groundY;
                    _velocityY = 0;
                    _isFalling = false;
                }
            }
        }

        // 4. Animation frame advancement (always runs so sprite animates during drag)
        var state = _fsm.CurrentState;
        if (state != _lastState)
        {
            _frameIndex = 0;
            _frameTimer = 0;
            _lastState = state;
        }

        _frameTimer += dt;
        var frames = _assets.GetFrames(state.ToString());
        if (frames.Count > 0 && _frameTimer >= _frameDuration)
        {
            _frameTimer -= _frameDuration;
            _frameIndex = (_frameIndex + 1) % frames.Count;
        }

        // ─── RENDER ────────────────────────────────────────────────────
        if (frames.Count > 0 && _frameIndex >= frames.Count)
            _frameIndex = 0;
        BitmapImage? currentFrame = frames.Count > 0 ? frames[_frameIndex] : null;
        double scaleX = _fsm.Direction >= 0 ? 1.0 : -1.0;

        OnRender?.Invoke(currentFrame, X, Y, scaleX);
    }
}
