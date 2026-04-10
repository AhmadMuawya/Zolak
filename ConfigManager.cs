using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaysToSnooze.Zolak;

/// <summary>
/// Handles loading and saving zolak-config.json next to the executable.
/// Creates default config matching original hardcoded PetFSM values on first launch.
/// </summary>
public static class ConfigManager
{
    private static readonly string ConfigPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zolak-config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Loads config from disk, or creates defaults if the file doesn't exist.
    /// </summary>
    public static ZolakConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<ZolakConfig>(json, JsonOptions);
                if (config is not null && config.States.Count > 0)
                    return config;
            }
            catch
            {
                // Corrupted file – fall through to defaults
            }
        }

        var defaults = CreateDefault();
        Save(defaults);
        return defaults;
    }

    /// <summary>
    /// Persists config to disk.
    /// </summary>
    public static void Save(ZolakConfig config)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// Builds default config matching the original hardcoded PetFSM values.
    /// </summary>
    public static ZolakConfig CreateDefault()
    {
        return new ZolakConfig
        {
            Theme = "Dark",
            ActiveCharacter = "",
            PetSize = 64,
            AnimationSpeed = 1.0,
            RunOnStartup = true,
            BoredThresholdMinutes = 5.0,
            Gravity = 800.0,
            States = new List<StateConfig>
            {
                new() { Name = "Idle",      Weight = 0.30, MinDuration = 3.0,  MaxDuration = 6.0,  Order = 0 },
                new() { Name = "Sit",       Weight = 0.15, MinDuration = 4.0,  MaxDuration = 8.0,  Order = 1 },
                new() { Name = "Walk",      Weight = 0.15, MinDuration = 3.0,  MaxDuration = 7.0,  Speed = 60.0,  IsMovement = true, Order = 2 },
                new() { Name = "Run",       Weight = 0.10, MinDuration = 2.0,  MaxDuration = 4.0,  Speed = 140.0, IsMovement = true, Order = 3 },
                new() { Name = "Move1",     Weight = 0.05, MinDuration = 3.0,  MaxDuration = 8.0,  Order = 4 },
                new() { Name = "Move2",     Weight = 0.05, MinDuration = 3.0,  MaxDuration = 8.0,  Order = 5 },
                new() { Name = "Happy",     Weight = 0.05, MinDuration = 2.0,  MaxDuration = 6.0,  Order = 6 },
                new() { Name = "Celebrate", Weight = 0.15, MinDuration = 3.0,  MaxDuration = 6.0,  Order = 7 },
                new() { Name = "Angry",     IsExtraordinary = true, HoverTrigger = "Enter",      MinDuration = 1.0, MaxDuration = 99.0, Order = 8 },
                new() { Name = "Bored",     IsExtraordinary = true, HoverTrigger = "Inactivity",  MinDuration = 1.0, MaxDuration = 99.0, Order = 9 },
                new() { Name = "Tease",     IsExtraordinary = true, HoverTrigger = "Leave",       MinDuration = 2.0, MaxDuration = 8.0,  Order = 10 },
            }
        };
    }
}
