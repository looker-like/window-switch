using System.IO;
using System.Text.Json;

namespace WindowSwitch.Services;

public sealed class WindowSettingsStore : IWindowSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public WindowSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowSwitch",
            "settings.json"))
    {
    }

    public WindowSettingsStore(string path)
    {
        _path = path;
    }

    public WindowSettings? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<WindowSettings>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(WindowSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Position persistence is best-effort and should not prevent shutdown.
        }
    }
}
