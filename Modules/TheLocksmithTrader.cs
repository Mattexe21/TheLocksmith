using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using TheLocksmith.Lib;

namespace TheLocksmith.Modules;

/// <summary>
/// Registers The Locksmith. Runs at TypePriority 400_002 (after TheLocksmithBoxes at
/// 400_001) so every box template already exists in the DB before being added to the
/// assort. Builds the assort from modData.MapKeys — single source of truth for content.
/// </summary>
[Injectable(InjectionType.Singleton, 400_002)]
public class TheLocksmithTrader(
    ISptLogger<TheLocksmithTrader> logger,
    TheLocksmithData modData,
    TemplateTable templateTable,
    TradersTable tradersTable,
    LocaleTable localeTable,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    InsuranceConfig insuranceConfig,
    ImageRouter imageRouter,
    ICloner cloner,
    JsonUtil jsonUtil,
    HandbookHelper handbookHelper) : IOnLoad
{
    public static readonly MongoId TraderId = TheLocksmithLib.MongoIdFromName("thelocksmith");
    private const string TraderImageRouteBase = "/files/trader/avatar/thelocksmith";
    private static readonly MongoId Roubles = new("5449016a4bdc2d6f028b456f");

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var traderBase = LoadTraderBase();
        if (traderBase is null)
        {
            logger.Error("[TheLocksmith.Trader] Failed to load base.json — trader not registered");
            return Task.CompletedTask;
        }

        traderBase.Id = TraderId;

        var avatarFile = System.IO.Path.Combine(modData.ImagesPath, "avatar.png");
        imageRouter.AddRoute(TraderImageRouteBase, avatarFile);

        traderConfig.UpdateTime.Add(new UpdateTime
        {
            TraderId = TraderId,
            Seconds = new MinMax<int>(3600, 7200),
        });

        ragfairConfig.Traders[TraderId] = true;
        insuranceConfig.ReturnChancePercent[TraderId] = 85.0;

        var dialogue = LoadDialogue();
        var trader = new Trader
        {
            Base = cloner.Clone(traderBase)!,
            Assort = NewEmptyAssort(),
            Dialogue = dialogue,
            QuestAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
            {
                ["Started"] = new(),
                ["Success"] = new(),
                ["Fail"] = new(),
            },
        };

        if (!tradersTable.TryAdd(TraderId, trader))
        {
            logger.Error($"[TheLocksmith.Trader] Failed to add trader {TraderId} to DB");
            return Task.CompletedTask;
        }

        AddTraderToAllLocales(traderBase, "The Locksmith", "Deals in doors, and what opens them.");

        var assort = BuildAssort();
        trader.Assort = assort;

        logger.Info($"[TheLocksmith.Trader] The Locksmith registered with {assort.Items.Count} items in assort.");
        return Task.CompletedTask;
    }

    private TraderBase? LoadTraderBase()
    {
        var path = System.IO.Path.Combine(modData.DatabasePath, "base.json");
        if (!File.Exists(path)) return null;
        var raw = File.ReadAllText(path);

        var node = JsonNode.Parse(raw)!;
        node["_id"] = TraderId.ToString();
        EnsureItemBuyDataShape(node, "items_buy_prohibited");
        EnsureItemBuyDataShape(node, "items_buy");
        EnsureItemBuyDataShape(node, "transferableItems");
        EnsureItemBuyDataShape(node, "prohibitedTransferableItems");

        try
        {
            return jsonUtil.Deserialize<TraderBase>(node.ToJsonString());
        }
        catch (JsonException ex)
        {
            logger.Error($"[TheLocksmith.Trader] base.json deserialize failed: {ex.Message}");
            return null;
        }
    }

    private static void EnsureItemBuyDataShape(JsonNode root, string fieldName)
    {
        if (root[fieldName] is not JsonObject obj) return;
        if (obj["category"] is null) obj["category"] = new JsonArray();
        if (obj["id_list"] is null) obj["id_list"] = new JsonArray();
    }

    private Dictionary<string, List<string>?> LoadDialogue()
    {
        var path = System.IO.Path.Combine(modData.DatabasePath, "dialogue.json");
        if (!File.Exists(path)) return new();
        var raw = File.ReadAllText(path);
        return jsonUtil.Deserialize<Dictionary<string, List<string>?>>(raw) ?? new();
    }

    private static TraderAssort NewEmptyAssort() => new()
    {
        Items = new List<Item>(),
        BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
        LoyalLevelItems = new Dictionary<MongoId, int>(),
    };

    private void AddTraderToAllLocales(TraderBase baseJson, string firstName, string description)
    {
        var id = baseJson.Id;
        foreach (var (_, lazy) in localeTable.Global)
        {
            lazy.AddTransformer(map =>
            {
                if (map is null) return map!;
                map[$"{id} FullName"] = baseJson.Name ?? "";
                map[$"{id} FirstName"] = firstName;
                map[$"{id} Nickname"] = baseJson.Nickname ?? "";
                map[$"{id} Location"] = baseJson.Location ?? "";
                map[$"{id} Description"] = description;
                return map;
            });
        }
    }

    /// <summary>
    /// Single keys (union across every map, deduped) + one pre-filled box per map.
    /// </summary>
    private TraderAssort BuildAssort()
    {
        var assort = NewEmptyAssort();

        var allKeyIds = modData.MapKeys.Values
            .SelectMany(m => m.Keys)
            .Select(k => new MongoId(k.Id))
            .Distinct()
            .Where(KnownToServer)
            .ToList();

        foreach (var keyTpl in allKeyIds)
        {
            var price = handbookHelper.GetTemplatePrice(keyTpl) * modData.Config.KeyPriceMultiplier;
            AddItemToAssort(assort, keyTpl, price, loyaltyLevel: 1);
        }

        foreach (var (mapKey, map) in modData.MapKeys)
        {
            var boxTpl = TheLocksmithBoxes.BoxTplId(mapKey);
            var keyTplIds = new List<MongoId>();
            foreach (var k in map.Keys)
            {
                var tpl = new MongoId(k.Id);
                if (KnownToServer(tpl))
                {
                    keyTplIds.Add(tpl);
                }
                else
                {
                    logger.Warning($"[TheLocksmith.Trader] '{mapKey}' box: key '{k.Name}' ({k.Id}) doesn't exist in this SPT install's item DB — skipped");
                }
            }
            AddKeyBoxToAssort(assort, boxTpl, keyTplIds);
        }

        return assort;
    }

    /// <summary>
    /// Hand-builds an item tree — a root box item plus one child key item per grid cell,
    /// left-to-right/top-to-bottom — and adds it to the assort as a single purchasable offer.
    /// </summary>
    private bool KnownToServer(MongoId tpl) => templateTable.Items.ContainsKey(tpl);

    private void AddKeyBoxToAssort(TraderAssort assort, MongoId boxTpl, List<MongoId> keyTplIds)
    {
        if (!templateTable.Items.ContainsKey(boxTpl)) return;
        if (keyTplIds.Count == 0)
        {
            logger.Warning($"[TheLocksmith.Trader] box '{boxTpl}' has no known keys left after filtering — not adding it to the assort");
            return;
        }

        var root = new Item
        {
            Id = new MongoId(),
            Template = boxTpl,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = true,
                StackObjectsCount = 9_999_999,
                BuyRestrictionMax = 999,
                BuyRestrictionCurrent = 0,
            },
        };
        assort.Items.Add(root);

        var cellsH = modData.Config.BoxCellsH;
        for (int i = 0; i < keyTplIds.Count; i++)
        {
            assort.Items.Add(new Item
            {
                Id = new MongoId(),
                Template = keyTplIds[i],
                ParentId = root.Id,
                SlotId = "main",
                // NOT SPTarkov's ItemLocation: its R property is [JsonConverter(JsonStringEnumConverter)],
                // serializing "r" as a string ("Horizontal"). Every real client-side location in the game
                // (loot generation, equipment presets, profiles) sends "r" as a plain int (0/1) instead —
                // the EFT client's own LocationInGrid.r is a bare int-backed enum with no string converter.
                // Build the wire shape by hand so "r" round-trips the way the client actually expects.
                Location = new { x = i % cellsH, y = i / cellsH, r = 0 },
            });
        }

        var priceRoubles = keyTplIds.Sum(k => handbookHelper.GetTemplatePrice(k)) * modData.Config.BoxPriceMultiplier;
        assort.BarterScheme[root.Id] = new List<List<BarterScheme>>
        {
            new() { new BarterScheme { Count = priceRoubles, Template = Roubles } },
        };
        assort.LoyalLevelItems[root.Id] = 1;
    }

    private void AddItemToAssort(TraderAssort assort, MongoId itemTpl, double priceRoubles, int loyaltyLevel)
    {
        if (!templateTable.Items.ContainsKey(itemTpl)) return;

        var entry = new Item
        {
            Id = new MongoId(),
            Template = itemTpl,
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = true,
                StackObjectsCount = 9_999_999,
                BuyRestrictionMax = 999,
                BuyRestrictionCurrent = 0,
            },
        };
        assort.Items.Add(entry);

        assort.BarterScheme[entry.Id] = new List<List<BarterScheme>>
        {
            new() { new BarterScheme { Count = priceRoubles, Template = Roubles } },
        };
        assort.LoyalLevelItems[entry.Id] = loyaltyLevel;
    }
}
