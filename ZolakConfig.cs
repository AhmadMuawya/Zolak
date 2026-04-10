namespace WaysToSnooze.Zolak;

/// <summary>
/// Root configuration model persisted to zolak-config.json.
/// PetFSM reads from this at startup and can hot-reload at runtime.
/// </summary>
public class ZolakConfig
{
    public string Theme { get; set; } = "Dark";
    public string ActiveCharacter { get; set; } = "";
    public int PetSize { get; set; } = 64;
    public double AnimationSpeed { get; set; } = 1.0;
    public bool RunOnStartup { get; set; } = true;
    public double BoredThresholdMinutes { get; set; } = 5.0;
    public double Gravity { get; set; } = 800.0;
    public List<StateConfig> States { get; set; } = new();
}

/// <summary>
/// Configuration for a single pet state.
/// Maps to one PetState enum value and its corresponding asset folder.
/// </summary>
public class StateConfig
{
    public string Name { get; set; } = "";
    public double Weight { get; set; }
    public double MinDuration { get; set; } = 3.0;
    public double MaxDuration { get; set; } = 6.0;
    public bool IsExtraordinary { get; set; }
    public string? HoverTrigger { get; set; }  // "Enter", "Leave", "Inactivity", or null
    public double Speed { get; set; }           // pixels/sec, 0 = no movement
    public bool IsMovement { get; set; }        // true for Walk, Run
    public int Order { get; set; }
}
