using System;
using System.IO;
using System.Text.Json;

namespace ToiletWidget;

public static class SettingsStore
{
    private static readonly string LocalDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToiletWidget");

    private static readonly string LocalPathJson = Path.Combine(LocalDir, "settings.json");

    // 旧保存先（Roaming）
    private static readonly string RoamingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToiletWidget");

    private static readonly string RoamingPathJson = Path.Combine(RoamingDir, "settings.json");

    public static Settings LoadOrCreate()
    {
        try
        {
            // 1) 既に Local にあればそれを優先
            if (File.Exists(LocalPathJson))
            {
                var json = File.ReadAllText(LocalPathJson);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s != null) return s;
            }

            // 2) 旧 Roaming にあれば Local へ移行して読み込む
            if (File.Exists(RoamingPathJson))
            {
                Directory.CreateDirectory(LocalDir);
                File.Copy(RoamingPathJson, LocalPathJson, overwrite: true);

                var json = File.ReadAllText(LocalPathJson);
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
        Directory.CreateDirectory(LocalDir);
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(LocalPathJson, json);
    }
}
