using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using TheLocksmith.Lib;

namespace TheLocksmith.Modules;

/// <summary>
/// Creates one "key box" item template per map — a clone of the S I C C organizational
/// pouch (its grid filter already allows the Key category), resized to a fixed
/// BoxCellsH x BoxCellsV grid. The Trader module fills each box's assort offer with
/// the actual key items afterwards.
/// </summary>
[Injectable(InjectionType.Singleton, 400_001)]
public class TheLocksmithBoxes(
    ISptLogger<TheLocksmithBoxes> logger,
    TheLocksmithData modData,
    TheLocksmithLib lib,
    HandbookHelper handbookHelper,
    TemplateTable templateTable) : IOnLoad
{
    private const string SiccPouchTpl = "5d235bb686f77443f4331278";

    // "RandomLootContainer" node -- same category new_year_gift_*/event_container_airdrop_*
    // use. Parenting the box here (instead of the plain "Simple container" category) is what
    // makes the client show the native "Open" action on it; see TheLocksmithBoxOpenPatch,
    // which intercepts that action for these specific tpls.
    private const string RandomLootContainerParent = "62f109593b54472778797866";
    private const string HandbookCategory = "5b5f6fa186f77409407a7eb7";

    public static string BoxNameId(string mapKey) => $"locksmith_box_{mapKey}";
    public static MongoId BoxTplId(string mapKey) => TheLocksmithLib.MongoIdFromName(BoxNameId(mapKey));

    /// <summary>Populated as boxes are created; read by TheLocksmithBoxOpenPatch to know which
    /// item templates should get the "give real contents, not a random roll" treatment.</summary>
    public static readonly HashSet<MongoId> BoxTplIds = new();

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        int created = 0;
        foreach (var (mapKey, map) in modData.MapKeys)
        {
            var newId = BoxTplId(mapKey);
            var contentsValue = map.Keys.Sum(k => handbookHelper.GetTemplatePrice(new MongoId(k.Id)));

            var props = new TemplateItemProperties();
            var result = lib.CreateItem(
                newId: newId,
                itemTplToClone: new MongoId(SiccPouchTpl),
                parentId: new MongoId(RandomLootContainerParent),
                internalName: BoxNameId(mapKey),
                longName: $"The Locksmith's {map.Name} Keybox",
                shortName: $"{map.Name} Keys",
                description: $"A reinforced key pouch, pre-loaded with every door and access key needed on {map.Name}.",
                handbookCategoryId: HandbookCategory,
                priceRoubles: contentsValue,
                overrideProperties: props);

            if (!result.Success)
            {
                logger.Warning($"[TheLocksmith.Boxes] CreateItem failed for '{mapKey}': {string.Join("; ", result.Errors)}");
                continue;
            }
            created++;
            BoxTplIds.Add(newId);

            if (templateTable.Items.TryGetValue(newId, out var item))
            {
                var grid = item.Properties?.Grids?.FirstOrDefault();
                if (grid?.Properties is not null)
                {
                    grid.Properties.CellsH = modData.Config.BoxCellsH;
                    grid.Properties.CellsV = modData.Config.BoxCellsV;
                    // Grid filters are inherited from the S I C C pouch clone (already allows Keys).
                }
            }
        }

        logger.Info($"[TheLocksmith.Boxes] registered {created}/{modData.MapKeys.Count} key boxes");
        return Task.CompletedTask;
    }
}
