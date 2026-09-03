using System.Collections.Generic;

namespace TheLocksmith.Models;

public class MapKeyEntry
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? ShortName { get; set; }
}

public class MapEntry
{
    public string Name { get; set; } = "";
    public List<MapKeyEntry> Keys { get; set; } = new();
}
