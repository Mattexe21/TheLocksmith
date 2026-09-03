using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SemVer = SemanticVersioning;

namespace TheLocksmith;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.matteo.thelocksmith";
    public string Name { get; init; } = "The Locksmith";
    public string Author { get; init; } = "Matteo";
    public List<string>? Contributors { get; init; } = new();
    public SemVer.Version Version { get; init; } = new SemVer.Version("1.0.0");
    public SemVer.Range SptVersion { get; init; } = new SemVer.Range("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemVer.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}
