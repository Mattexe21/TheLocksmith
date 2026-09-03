using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace TheLocksmith.Modules;

/// <summary>
/// Registers <see cref="OpenBoxLootPatch"/> via SPT's server-side Harmony wrapper
/// (SPTarkov.Reflection.Patching, the server-side twin of the client's SPT.Reflection.Patching).
/// Runs after TheLocksmithBoxes (400_001) so TheLocksmithBoxes.BoxTplIds is fully populated
/// before the patch can go live.
/// </summary>
[Injectable(InjectionType.Singleton, 400_003)]
public class TheLocksmithBoxOpenPatch(
    ISptLogger<TheLocksmithBoxOpenPatch> logger,
    InventoryHelper inventoryHelper) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        OpenBoxLootPatch.InventoryHelperRef = inventoryHelper;

        // Enabling directly (instead of going through PatchManager.EnablePatches()) because
        // that method swallows patch-application exceptions into a bare Console.WriteLine
        // that never reaches this log file -- doing it ourselves means a real failure here
        // (wrong target method, signature mismatch, etc.) is actually visible.
        var patch = new OpenBoxLootPatch();
        try
        {
            patch.Enable();
            logger.Info($"[TheLocksmith.BoxOpenPatch] enabled for {TheLocksmithBoxes.BoxTplIds.Count} box template(s), IsActive={patch.IsActive}.");
        }
        catch (System.Exception ex)
        {
            logger.Error($"[TheLocksmith.BoxOpenPatch] FAILED to enable: {ex}");
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Prefixes <see cref="InventoryController.OpenRandomLootContainer"/>.
/// <br/>
/// Decompiled from the current server build: the vanilla handler (used for the Halloween
/// jack-o-lantern / New Year gift / airdrop-crate "Open" action) ignores whatever the
/// container item actually contains. It looks up a reward table keyed by the item's
/// *template id*, weighted-randomly rolls N items from that pool (with repeats possible,
/// no guarantee of covering the whole pool), adds the roll to the player's stash via
/// InventoryHelper.AddItemsToStash, then deletes the original container (and whatever was
/// really inside it, discarded) via InventoryHelper.RemoveItem.
/// <br/>
/// That roll is wrong for us: a Locksmith box must hand back exactly the keys it was sold
/// with, not a random subset with possible duplicates. This prefix intercepts only our own
/// box template ids (TheLocksmithBoxes.BoxTplIds) and replicates the vanilla method's two
/// side effects itself, using the box's *real* current children instead of a random pick,
/// then returns false to skip the vanilla body entirely. Any other item (real event
/// containers, other mods' boxes) falls through to vanilla behavior untouched.
/// </summary>
internal class OpenBoxLootPatch : AbstractPatch
{
    // Static because Harmony patch methods are static and have no DI of their own; wired up
    // once by TheLocksmithBoxOpenPatch.OnLoadAsync before the patch is enabled.
    internal static InventoryHelper? InventoryHelperRef;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(InventoryController), nameof(InventoryController.OpenRandomLootContainer));
    }

    [PatchPrefix]
    static bool Prefix(PmcData pmcData, OpenRandomLootContainerRequestData request, MongoId sessionId, ItemEventRouterResponse output)
    {
        var items = pmcData.Inventory?.Items;
        var box = items?.FirstOrDefault(i => i.Id == request.Item);
        if (items == null || box == null || !TheLocksmithBoxes.BoxTplIds.Contains(box.Template) || InventoryHelperRef == null)
        {
            return true; // not one of ours (or not wired up yet) -- let vanilla handle it
        }

        var keys = items.Where(i => i.ParentId == box.Id).ToList();
        var itemsToAdd = keys
            .Select(k => new System.Collections.Generic.List<Item> { new() { Id = new MongoId(), Template = k.Template } })
            .ToList();

        if (itemsToAdd.Count > 0)
        {
            InventoryHelperRef.AddItemsToStash(sessionId, new AddItemsDirectRequest
            {
                ItemsWithModsToAdd = itemsToAdd,
                FoundInRaid = box.Upd?.SpawnedInSession,
                Callback = null,
                UseSortingTable = true,
            }, pmcData, output);

            if (output.Warnings is { Count: > 0 })
            {
                return false; // e.g. stash full -- report the error, leave the box intact
            }
        }

        InventoryHelperRef.RemoveItem(pmcData, request.Item, sessionId, output);
        return false;
    }
}
