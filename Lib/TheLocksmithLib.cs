using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace TheLocksmith.Lib;

/// <summary>
/// Clone-from-template helpers, shared by the Boxes and Trader modules.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class TheLocksmithLib(CustomItemService customItemService)
{
    /// <summary>
    /// Convert a stable string name (e.g. "locksmith_box_factory") to a deterministic
    /// 24-char MongoId by hashing with SHA-256 and taking the first 12 bytes. Same input
    /// always produces the same MongoId across builds and machines, so saved profiles
    /// keep their references.
    /// </summary>
    public static MongoId MongoIdFromName(string name)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        var sb = new StringBuilder(24);
        for (int i = 0; i < 12; i++) sb.Append(bytes[i].ToString("x2"));
        return new MongoId(sb.ToString());
    }

    /// <summary>
    /// Clone an existing item, override the given properties, and register it
    /// to items DB + handbook + locales + flea price.
    /// </summary>
    public CreateItemResult CreateItem(
        MongoId newId,
        MongoId itemTplToClone,
        MongoId parentId,
        string internalName,
        string longName,
        string shortName,
        string description,
        string handbookCategoryId,
        double priceRoubles,
        TemplateItemProperties overrideProperties)
    {
        overrideProperties.Name = longName;
        overrideProperties.ShortName = shortName;
        overrideProperties.Description = description;

        var details = new NewItemFromCloneDetails
        {
            ItemTplToClone = itemTplToClone,
            OverrideProperties = overrideProperties,
            ParentId = parentId,
            NewId = newId,
            NewItemName = internalName,
            FleaPriceRoubles = priceRoubles,
            HandbookPriceRoubles = priceRoubles,
            HandbookParentId = handbookCategoryId,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new()
                {
                    Name = longName,
                    ShortName = shortName,
                    Description = description,
                }
            }
        };

        return customItemService.CreateItemFromClone(details);
    }
}
