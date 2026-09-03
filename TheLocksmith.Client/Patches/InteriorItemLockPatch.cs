using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace TheLocksmith.Client.Patches;

/// <summary>
/// Postfixes <see cref="TradingItemView.NewTradingItemView"/>.
/// <br/>
/// Without this, the key items rendered *inside* a trader-sold box behave like their own
/// independently purchasable/selectable offers instead of fixed contents of the box. This
/// flags them as a "mod slot" view (the same display mode used for weapon attachments,
/// which already has the correct "Trader lock" tooltip wired up -- see
/// <c>TradingItemView.ShowTooltip</c>) and disables selecting them directly, for any item
/// that: belongs to a Trader-owned view, sits inside another item (not the top-level
/// "hideout"/"table" slot), and isn't already being treated as a mod slot.
/// </summary>
internal class InteriorItemLockPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TradingItemView), nameof(TradingItemView.NewTradingItemView));
    }

    [PatchPostfix]
    static void Postfix(Item item, IItemOwner itemOwner, ref bool ___ModSlotView, ref bool ____canSelect)
    {
        // Items being dragged from the stash into the sell area have no CurrentAddress yet;
        // Item.Parent throws in that case, so bail out before touching it.
        if (item.CurrentAddress == null || ___ModSlotView)
        {
            return;
        }

        var containerName = item.Parent.ContainerName;
        bool isInsideSomething = containerName != null && containerName != "hideout" && containerName != "table";
        if (itemOwner.OwnerType != EOwnerType.Trader || !isInsideSomething)
        {
            return;
        }

        ___ModSlotView = true;
        ____canSelect = false;
    }
}
