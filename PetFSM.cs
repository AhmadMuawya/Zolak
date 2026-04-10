namespace WaysToSnooze.Zolak;

/// <summary>
/// The "Brain" of the pet. Manages modes A (Autonomous), B (Interaction),
/// and C (Neglect). Decides the current PetState and handles transitions.
/// Now reads all configuration from ZolakConfig at runtime.
/// </summary>
public class PetFSM
{
    // ── Current State ──────────────────────────────────────────────────
    public PetState CurrentState { get; private set; } = PetState.Idle;
    public int Direction { get; private set; } = 1;  // 1 = Right, -1 = Left

    // ── Mode Tracking ──────────────────────────────────────────────────
    private bool _isInteracting;        // true while mouse is over pet
    private bool _teaseTimerActive;
    private double _teaseCooldown;       // seconds remaining for tease timer
    private double _inactivityTimer;     // seconds since last interaction
    private double _stateTimer;          // seconds spent in the current state
    private double _stateDuration;       // how long the current state should last

    private const double TeaseDelay = 5.0;
    private double _boredThreshold = 300.0; // 5 minutes (configurable)

    private readonly Random _rng = new();

    // ── Configurable State Data (loaded from ZolakConfig) ──────────────
    private List<(PetState state, double weight)> _autonomousWeights = new()
    {
        (PetState.Idle,       0.30),
        (PetState.Sit,        0.15),
        (PetState.Walk,       0.15),
        (PetState.Run,        0.10),
        (PetState.Move1,      0.05),
        (PetState.Move2,      0.05),
        (PetState.Happy,      0.05),
        (PetState.Celebrate,  0.15),
    };

    private Dictionary<PetState, (double min, double max)> _durations = new()
    {
        { PetState.Idle,      (3.0, 6.0) },
        { PetState.Sit,       (4.0, 8.0) },
        { PetState.Walk,      (3.0, 7.0) },
        { PetState.Run,       (2.0, 4.0) },
        { PetState.Move1,     (3.0, 8.0) },
        { PetState.Move2,     (3.0, 8.0) },
        { PetState.Angry,     (1.0, 99.0) },
        { PetState.Bored,     (1.0, 99.0) },
        { PetState.Happy,     (2.0, 6.0) },
        { PetState.Celebrate, (3.0, 6.0) },
        { PetState.Tease,     (2.0, 8.0) },
    };

    private Dictionary<PetState, double> _speeds = new()
    {
        { PetState.Walk, 60.0 },
        { PetState.Run, 140.0 },
    };

    private HashSet<PetState> _movementStates = new() { PetState.Walk, PetState.Run };

    // ── Configurable hover triggers ────────────────────────────────────
    private PetState _hoverEnterState = PetState.Angry;
    private PetState _hoverLeaveState = PetState.Tease;
    private PetState _inactivityState = PetState.Bored;

    // ── Movement speeds (pixels per second) ────────────────────────────
    public double GetSpeed()
    {
        return _speeds.GetValueOrDefault(CurrentState, 0.0);
    }

    /// <summary>
    /// Returns true if the pet is in a state that requires horizontal movement.
    /// </summary>
    public bool IsMoving => _movementStates.Contains(CurrentState);

    // ── Public API ─────────────────────────────────────────────────────

    public void LoadConfig(ZolakConfig config)
    {
        _boredThreshold = config.BoredThresholdMinutes * 60.0;

        var newWeights = new List<(PetState state, double weight)>();
        var newDurations = new Dictionary<PetState, (double min, double max)>();
        var newSpeeds = new Dictionary<PetState, double>();
        var newMovement = new HashSet<PetState>();
        PetState? hoverEnter = null;
        PetState? hoverLeave = null;
        PetState? inactivity = null;

        foreach (var sc in config.States)
        {
            if (Enum.TryParse<PetState>(sc.Name, out var petState))
            {
                // Duration
                newDurations[petState] = (sc.MinDuration, sc.MaxDuration);

                // Autonomous weight (only for non-extraordinary states)
                if (!sc.IsExtraordinary && sc.Weight > 0)
                    newWeights.Add((petState, sc.Weight));

                // Speed / movement
                if (sc.Speed > 0)
                    newSpeeds[petState] = sc.Speed;
                if (sc.IsMovement)
                    newMovement.Add(petState);

                // Hover triggers
                switch (sc.HoverTrigger)
                {
                    case "Enter":     hoverEnter = petState; break;
                    case "Leave":     hoverLeave = petState; break;
                    case "Inactivity": inactivity = petState; break;
                }
            }
        }

        if (newWeights.Count > 0) _autonomousWeights = newWeights;
        if (newDurations.Count > 0) _durations = newDurations;
        if (newSpeeds.Count > 0) _speeds = newSpeeds;
        if (newMovement.Count > 0) _movementStates = newMovement;

        if (hoverEnter.HasValue) _hoverEnterState = hoverEnter.Value;
        if (hoverLeave.HasValue) _hoverLeaveState = hoverLeave.Value;
        if (inactivity.HasValue) _inactivityState = inactivity.Value;
    }

    /// <summary>
    /// Resets the brain to a clean Idle state.
    /// Used when switching characters or reloading.
    /// </summary>
    public void Reset()
    {
        _inactivityTimer = 0;
        _teaseTimerActive = false;
        _teaseCooldown = 0;
        TransitionTo(PetState.Idle);
    }

    /// <summary>
    /// Called every frame by GameLoopManager. deltaTime is in seconds.
    /// screenLeft/screenRight define the walking boundaries.
    /// petX is the current horizontal position of the pet.
    /// </summary>
    public void Update(double deltaTime, double petX, double screenLeft, double screenRight, double petWidth)
    {
        // ── Mode C: Inactivity ─────────────────────────────────────────
        if (!_isInteracting)
        {
            _inactivityTimer += deltaTime;
            if (_inactivityTimer >= _boredThreshold && CurrentState != _inactivityState)
            {
                TransitionTo(_inactivityState);
                return;
            }
        }

        // ── Mode B: Tease Cooldown ─────────────────────────────────────
        if (_teaseTimerActive)
        {
            _teaseCooldown -= deltaTime;
            if (_teaseCooldown <= 0)
            {
                _teaseTimerActive = false;
                TransitionTo(_hoverLeaveState);
                return;
            }
        }

        // ── State Timer ────────────────────────────────────────────────
        _stateTimer += deltaTime;

        // Angry/hover and Bored/inactivity are held externally, skip auto-transition
        if (CurrentState == _hoverEnterState || CurrentState == _inactivityState)
            return;

        if (_stateTimer >= _stateDuration)
        {
            // The current state has expired – pick the next one
            PickNextAutonomousState(petX, screenLeft, screenRight, petWidth);
        }

        // ── Edge Detection: force direction flip if pet hits screen edge ──
        if (IsMoving)
        {
            if (petX <= screenLeft + 10)
                Direction = 1;
            else if (petX + petWidth >= screenRight - 10)
                Direction = -1;
        }
    }

    /// <summary>
    /// Called when user's mouse enters the pet image.
    /// </summary>
    public void OnMouseEnter()
    {
        _isInteracting = true;
        _inactivityTimer = 0;
        _teaseTimerActive = false;
        TransitionTo(_hoverEnterState);
    }

    /// <summary>
    /// Called when user's mouse leaves the pet image.
    /// </summary>
    public void OnMouseLeave()
    {
        _isInteracting = false;
        TransitionTo(PetState.Idle);

        // Start the 5-second tease timer
        _teaseTimerActive = true;
        _teaseCooldown = TeaseDelay;
    }

    // ── Private Helpers ────────────────────────────────────────────────

    private void TransitionTo(PetState newState)
    {
        CurrentState = newState;
        _stateTimer = 0;

        var (min, max) = _durations.GetValueOrDefault(newState, (3.0, 5.0));
        _stateDuration = min + _rng.NextDouble() * (max - min);
    }

    private void PickNextAutonomousState(double petX, double screenLeft, double screenRight, double petWidth)
    {
        // Roll weighted random
        double roll = _rng.NextDouble();
        double cumulative = 0;
        PetState chosen = PetState.Idle;

        foreach (var (state, weight) in _autonomousWeights)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                chosen = state;
                break;
            }
        }

        // Pick a direction for movement states
        if (_movementStates.Contains(chosen))
        {
            // Bias direction away from edges
            double center = (screenLeft + screenRight) / 2.0;
            if (petX < center - 100)
                Direction = 1;
            else if (petX > center + 100)
                Direction = -1;
            else
                Direction = _rng.Next(2) == 0 ? -1 : 1;
        }

        TransitionTo(chosen);
    }
}
