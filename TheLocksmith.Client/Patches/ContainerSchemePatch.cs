using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace TheLocksmith.Client.Patches;

/// <summary>
/// Postfixes <see cref="Assortment.GetSchemeForItem"/>.
/// <br/>
/// The game's own <c>Item.IsExchangeable</c> extension method returns <c>false</c> for any
/// <c>CompoundItem</c> that is not empty (decompiled from the current client build):
/// <code>
/// if (item is CompoundItem compoundItem &amp;&amp; !compoundItem.IsEmpty) return false;
/// </code>
/// <c>GetSchemeForItem</c> checks that first and bails out to <c>null</c> before ever
/// touching its own <c>_schemes</c> dictionary -- so a trader-sold container that already
/// has items inside it never gets a price/barter scheme, no matter what the server sent.
/// This postfix falls back to the same dictionary lookup <c>GetSchemeForItem</c> would have
/// done itself, for the case where the default check rejected the item for having contents.
/// </summary>
internal class ContainerSchemePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Assortment), nameof(Assortment.GetSchemeForItem));
    }

    [PatchPostfix]
    static void Postfix(ref BarterScheme __result, Assortment __instance, Item item)
    {
        if (__result != null || item == null)
        {
            return;
        }

        __instance._schemes.TryGetValue(item.Id, out __result);
    }
}
