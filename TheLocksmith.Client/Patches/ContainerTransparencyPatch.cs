using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace TheLocksmith.Client.Patches;

/// <summary>
/// Postfixes <see cref="TradingItemView.SetContainerCanvasGroup"/>.
/// <br/>
/// Decompiled from the current client build:
/// <code>
/// public void SetContainerCanvasGroup(CompoundItem item)
/// {
///     if (!item.Grids.SelectMany(g => g.ContainedItems).All(ci => ci.Key == null))
///         CanvasGroup.SetUnlockStatus(value: false, setRaycast: false);
/// }
/// </code>
/// Any container with at least one non-empty grid cell gets locked/greyed out -- this runs
/// for every <c>TradingItemView</c>, not just trader-owned ones. We undo it, but only for
/// items owned by a Trader (<c>_ownerType</c>, private field), so player stash/ground
/// containers keep their normal "has stuff inside" indication.
/// </summary>
internal class ContainerTransparencyPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TradingItemView), nameof(TradingItemView.SetContainerCanvasGroup));
    }

    [PatchPostfix]
    static void Postfix(TradingItemView __instance, EOwnerType ____ownerType)
    {
        if (____ownerType != EOwnerType.Trader)
        {
            return;
        }

        __instance.CanvasGroup.SetUnlockStatus(true, false);
    }
}
