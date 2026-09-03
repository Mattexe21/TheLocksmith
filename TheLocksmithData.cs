using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using TheLocksmith.Models;

namespace TheLocksmith;

/// <summary>
/// Singleton holder of mod paths and runtime config. Loaded once at startup; injected wherever needed.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class TheLocksmithData
{
    public string PathToMod { get; }
    public string AssetsPath { get; }
    public string ConfigPath { get; }
    public string DatabasePath { get; }
    public string ImagesPath { get; }
    public TheLocksmithConfig Config { get; }
    public Dictionary<string, MapEntry> MapKeys { get; }

    public TheLocksmithData(ModHelper modHelper)
    {
        PathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        AssetsPath = Path.Combine(PathToMod, "assets");
        ConfigPath = Path.Combine(PathToMod, "config");
        DatabasePath = Path.Combine(AssetsPath, "database", "thelocksmith");
        ImagesPath = Path.Combine(AssetsPath, "images", "thelocksmith");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var configRaw = File.ReadAllText(Path.Combine(ConfigPath, "config.json"));
        Config = JsonSerializer.Deserialize<TheLocksmithConfig>(configRaw, jsonOptions) ?? new TheLocksmithConfig();

        var mapKeysRaw = File.ReadAllText(Path.Combine(ConfigPath, "mapKeys.json"));
        MapKeys = JsonSerializer.Deserialize<Dictionary<string, MapEntry>>(mapKeysRaw, jsonOptions) ?? new();
    }
}
