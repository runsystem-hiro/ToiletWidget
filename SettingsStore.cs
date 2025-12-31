using System;
using System.IO;
using System.Text.Json;

namespace ToiletWidget;

public static class SettingsStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToiletWidget");

    private static readonly string PathJson = Path.Combine(Dir, "settings.json");

    public static Settings LoadOrCreate()
    {
        try
        {
            if (File.Exists(PathJson))
            {
                var json = File.ReadAllText(PathJson);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s != null) return s;
            }
        }
        catch
        {
            // 読めなくても起動優先
        }

        var created = new Settings();
        Save(created);
        return created;
    }

    public static void Save(Settings s)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(PathJson, json);
    }
}
