using System.IO;
using System.Windows.Media.Imaging;

namespace WaysToSnooze.Zolak;

/// <summary>
/// Singleton asset cache. Supports multiple characters.
/// Structure: Assets/{character}/{state}/{character}_{frame}.png
/// </summary>
public sealed class AssetManager
{
    private static readonly Lazy<AssetManager> _instance = new(() => new AssetManager());
    public static AssetManager Instance => _instance.Value;

    // character → state → frames
    private readonly Dictionary<string, Dictionary<string, List<BitmapImage>>> _characters = new();

    private string _currentCharacter = string.Empty;
    private string _assetsRoot = string.Empty;

    private AssetManager() { }

    /// <summary>
    /// The currently active character name.
    /// </summary>
    public string CurrentCharacter => _currentCharacter;

    /// <summary>
    /// Returns all available character names.
    /// </summary>
    public List<string> GetAvailableCharacters() => _characters.Keys.ToList();

    /// <summary>
    /// Scans the Assets root and loads all characters and their state frames.
    /// Sets the first found character as active.
    /// </summary>
    public void LoadAll(string assetsRootPath)
    {
        _characters.Clear();
        _assetsRoot = assetsRootPath;

        if (!Directory.Exists(assetsRootPath))
            throw new DirectoryNotFoundException($"Assets root not found: {assetsRootPath}");

        foreach (var characterDir in Directory.GetDirectories(assetsRootPath))
        {
            string charName = Path.GetFileName(characterDir);
            var stateCache = new Dictionary<string, List<BitmapImage>>();

            foreach (var stateDir in Directory.GetDirectories(characterDir))
            {
                string stateName = Path.GetFileName(stateDir);
                var frames = new List<BitmapImage>();

                var files = Directory.GetFiles(stateDir, "*.png")
                                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

                foreach (var file in files)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(Path.GetFullPath(file), UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    frames.Add(bmp);
                }

                if (frames.Count > 0)
                    stateCache[stateName] = frames;
            }

            if (stateCache.Count > 0)
                _characters[charName] = stateCache;
        }

        // Default to the first character found
        if (_characters.Count > 0)
            _currentCharacter = _characters.Keys.First();
    }

    /// <summary>
    /// Switches active character. Returns false if not found.
    /// </summary>
    public bool SetCharacter(string characterName)
    {
        if (_characters.ContainsKey(characterName))
        {
            _currentCharacter = characterName;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns frames for the given state of the CURRENT character.
    /// </summary>
    public List<BitmapImage> GetFrames(string stateName)
    {
        if (!string.IsNullOrEmpty(_currentCharacter)
            && _characters.TryGetValue(_currentCharacter, out var states)
            && states.TryGetValue(stateName, out var frames))
        {
            return frames;
        }
        return new List<BitmapImage>();
    }

    public int CharacterCount => _characters.Count;
}
