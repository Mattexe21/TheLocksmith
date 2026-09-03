using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace TheLocksmith;

/// <summary>
/// Top-level entry point. Runs OnLoad after the database is ready.
/// The Boxes and Trader modules do the real work as their own [Injectable]+IOnLoad classes.
/// </summary>
// TypePriority must be >= 200_000 — that's the post-DB-load threshold SPTStartupHostedService uses.
[Injectable(InjectionType.Singleton, 400_000)]
public class TheLocksmithPlugin(
    ISptLogger<TheLocksmithPlugin> logger,
    TheLocksmithData modData) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Info($"[TheLocksmith] {nameof(TheLocksmithPlugin)} OnLoad — config loaded from {modData.ConfigPath}, {modData.MapKeys.Count} maps in mapKeys.json");
        return Task.CompletedTask;
    }
}
