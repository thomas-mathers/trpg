using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Mappers;
using TRPG.Application.Inventory.Queries;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Contracts.Creatures.Responses;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Endpoints;

internal static class CreatureEndpoints
{
    public static void MapCreatureEndpoints(this WebApplication app)
    {
        app.MapGet("/creatures/{creatureId:guid}/abilities", GetAbilities)
            .WithName("GetCreatureAbilities");
        app.MapGet("/creatures/{creatureId:guid}/inventory", GetInventory)
            .WithName("GetCreatureInventory");
        app.MapGet("/creatures/{creatureId:guid}/consumables", GetConsumables)
            .WithName("GetCreatureConsumables");
        app.MapGet("/creatures/{creatureId:guid}/attribute-points", GetAttributePoints)
            .WithName("GetCreatureAttributePoints");
        app.MapPatch("/creatures/{creatureId:guid}/attribute-points", AllocateAttributePoints)
            .WithName("AllocateCreatureAttributePoints");
        app.MapGet("/creatures/{creatureId:guid}/base-attributes", GetBaseAttributes)
            .WithName("GetCreatureBaseAttributes");
        app.MapGet("/creatures/{creatureId:guid}/attributes", GetEffectiveStats)
            .WithName("GetCreatureAttributes");
        app.MapGet("/creatures/{creatureId:guid}/basic-attack-damage", GetBasicAttackDamage)
            .WithName("GetCreatureBasicAttackDamage");
        app.MapGet("/creatures/{creatureId:guid}/skills", GetSkills).WithName("GetCreatureSkills");
        app.MapGet("/creatures/{creatureId:guid}/level", GetLevel).WithName("GetCreatureLevel");
        app.MapPut("/creatures/{creatureId:guid}/equipment", EquipItem)
            .WithName("EquipCreatureItem");
        app.MapDelete("/creatures/{creatureId:guid}/equipment/{slot}", UnequipItem)
            .WithName("UnequipCreatureItem");
        app.MapGet("/creatures/{creatureId:guid}/equipment/preview", PreviewEquipItemStats)
            .WithName("PreviewCreatureEquipment");
        app.MapGet(
                "/creatures/{creatureId:guid}/equipment/preview/basic-attack-damage",
                PreviewEquipItemBasicAttackDamage
            )
            .WithName("PreviewCreatureBasicAttackDamage");
        app.MapGet("/players/{playerId:guid}/nearby-corpses", GetNearbyCorpses)
            .WithName("GetNearbyCorpses");
    }

    private static async Task<Ok<AbilitySummary[]>> GetAbilities(
        Guid creatureId,
        GetCreatureAbilitiesQueryHandler getCreatureAbilities,
        CancellationToken cancellationToken
    )
    {
        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(abilities.Select(a => a.ToSummary()).ToArray());
    }

    private static async Task<Ok<InventorySummary>> GetInventory(
        Guid creatureId,
        GetInventorySummaryQueryHandler getInventorySummary,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await getInventorySummary.Handle(
            new GetInventorySummaryQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(
            new InventorySummary(snapshot.Gold, snapshot.Items.Select(ToItemDetail).ToArray())
        );
    }

    private static async Task<Ok<ConsumableSummary[]>> GetConsumables(
        Guid creatureId,
        GetInventoryByCreatureIdQueryHandler getInventoryByCreatureId,
        CancellationToken cancellationToken
    )
    {
        var items = await getInventoryByCreatureId.Handle(
            new GetInventoryByCreatureIdQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(items.OfType<Consumable>().Select(ToConsumableSummary).ToArray());
    }

    private static async Task<Ok<NearbyCorpseSummary[]>> GetNearbyCorpses(
        Guid playerId,
        GetNearbyCorpsesQueryHandler getNearbyCorpses,
        CancellationToken cancellationToken
    )
    {
        var corpses = await getNearbyCorpses.Handle(
            new GetNearbyCorpsesQuery { PlayerId = playerId },
            cancellationToken
        );

        return TypedResults.Ok(
            corpses.Select(c => new NearbyCorpseSummary(c.Id, c.Name, c.ItemCount)).ToArray()
        );
    }

    private static async Task<Ok<AttributePointsResponse>> GetAttributePoints(
        Guid creatureId,
        GetUnallocatedAttributePointsQueryHandler getUnallocatedAttributePoints,
        CancellationToken cancellationToken
    )
    {
        var points = await getUnallocatedAttributePoints.Handle(
            new GetUnallocatedAttributePointsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(new AttributePointsResponse(points));
    }

    private static async Task<NoContent> AllocateAttributePoints(
        Guid creatureId,
        AllocateAttributePointsRequest request,
        AllocateAttributePointsCommandHandler allocateAttributePoints,
        CancellationToken cancellationToken
    )
    {
        await allocateAttributePoints.Handle(
            new AllocateAttributePointsCommand
            {
                CreatureId = creatureId,
                Deltas = request.Deltas.ToDictionary(),
            },
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> EquipItem(
        Guid creatureId,
        EquipItemRequest request,
        EquipInventoryItemCommandHandler equipInventoryItem,
        CancellationToken cancellationToken
    )
    {
        await equipInventoryItem.Handle(
            new EquipInventoryItemCommand
            {
                CreatureId = creatureId,
                ItemId = request.ItemId,
                Slot = request.Slot.ToDataModel(),
            },
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<NoContent> UnequipItem(
        Guid creatureId,
        Contracts.Inventory.Responses.EquipmentSlot slot,
        UnequipInventoryItemCommandHandler unequipInventoryItem,
        CancellationToken cancellationToken
    )
    {
        await unequipInventoryItem.Handle(
            new UnequipInventoryItemCommand { CreatureId = creatureId, Slot = slot.ToDataModel() },
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BaseAttributesResponse>> GetBaseAttributes(
        Guid creatureId,
        GetCreatureBaseAttributesQueryHandler getCreatureBaseAttributes,
        CancellationToken cancellationToken
    )
    {
        var attributes = await getCreatureBaseAttributes.Handle(
            new GetCreatureBaseAttributesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(
            new BaseAttributesResponse(
                attributes.Strength,
                attributes.Defense,
                attributes.Dexterity,
                attributes.Endurance,
                attributes.Stamina,
                attributes.Mana,
                attributes.Intelligence
            )
        );
    }

    private static async Task<Ok<EffectiveAttributesResponse>> GetEffectiveStats(
        Guid creatureId,
        GetCreatureEffectiveStatsQueryHandler getCreatureEffectiveStats,
        CancellationToken cancellationToken
    )
    {
        var attributes = await getCreatureEffectiveStats.Handle(
            new GetCreatureEffectiveStatsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(ToEffectiveAttributesResponse(attributes));
    }

    private static async Task<Ok<EffectiveAttributesResponse>> PreviewEquipItemStats(
        Guid creatureId,
        Guid itemId,
        Contracts.Inventory.Responses.EquipmentSlot slot,
        PreviewEquipItemStatsQueryHandler previewEquipItemStats,
        CancellationToken cancellationToken
    )
    {
        var attributes = await previewEquipItemStats.Handle(
            new PreviewEquipItemStatsQuery
            {
                CreatureId = creatureId,
                ItemId = itemId,
                Slot = slot.ToDataModel(),
            },
            cancellationToken
        );

        return TypedResults.Ok(ToEffectiveAttributesResponse(attributes));
    }

    private static async Task<Ok<BasicAttackDamageResponse>> GetBasicAttackDamage(
        Guid creatureId,
        GetCreatureBasicAttackDamageQueryHandler getCreatureBasicAttackDamage,
        CancellationToken cancellationToken
    )
    {
        var damagePerTurn = await getCreatureBasicAttackDamage.Handle(
            new GetCreatureBasicAttackDamageQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(new BasicAttackDamageResponse(damagePerTurn));
    }

    private static async Task<Ok<BasicAttackDamageResponse>> PreviewEquipItemBasicAttackDamage(
        Guid creatureId,
        Guid itemId,
        Contracts.Inventory.Responses.EquipmentSlot slot,
        PreviewEquipItemBasicAttackDamageQueryHandler previewEquipItemBasicAttackDamage,
        CancellationToken cancellationToken
    )
    {
        var damagePerTurn = await previewEquipItemBasicAttackDamage.Handle(
            new PreviewEquipItemBasicAttackDamageQuery
            {
                CreatureId = creatureId,
                ItemId = itemId,
                Slot = slot.ToDataModel(),
            },
            cancellationToken
        );

        return TypedResults.Ok(new BasicAttackDamageResponse(damagePerTurn));
    }

    private static EffectiveAttributesResponse ToEffectiveAttributesResponse(
        Attributes attributes
    ) =>
        new(
            attributes.Strength,
            attributes.Dexterity,
            attributes.Intelligence,
            attributes.Endurance,
            attributes.Stamina,
            attributes.Mana,
            attributes.Defense,
            attributes.MaximumHp,
            attributes.MaximumAp,
            attributes.MaximumMp,
            attributes.MovementSpeed,
            attributes.PhysicalResistance,
            attributes.FireResistance,
            attributes.IceResistance,
            attributes.LightningResistance,
            attributes.PoisonResistance,
            attributes.MagicResistance
        );

    private static async Task<Ok<SkillProgressSummary[]>> GetSkills(
        Guid creatureId,
        GetCreatureSkillsQueryHandler getCreatureSkills,
        CancellationToken cancellationToken
    )
    {
        var skills = await getCreatureSkills.Handle(
            new GetCreatureSkillsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(skills.Select(ToSkillProgressSummary).ToArray());
    }

    private static async Task<Ok<CreatureLevelResponse>> GetLevel(
        Guid creatureId,
        GetCreatureLevelQueryHandler getCreatureLevel,
        CancellationToken cancellationToken
    )
    {
        var level = await getCreatureLevel.Handle(
            new GetCreatureLevelQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(new CreatureLevelResponse(level));
    }

    private static ItemDetail ToItemDetail(Item item)
    {
        var equippedSlot = item.Ownership.EquippedSlot?.ToContract();
        var type = ToItemType(item);
        var rarity = ToRarity(item);
        var modifiers = item.Modifiers.Select(ToItemModifierSummary).ToArray();

        return item switch
        {
            Weapon w => new WeaponDetail(
                w.Id,
                w.Name,
                w.Description,
                w.Weight,
                w.Quantity,
                equippedSlot,
                type,
                rarity,
                w.GoldValue,
                modifiers,
                w.MinDamage,
                w.MaxDamage,
                w.Range,
                w.AttacksPerTurn,
                w.IsTwoHanded,
                w.DurabilityCurrent,
                w.DurabilityMax
            ),
            Armor a => new ArmorDetail(
                a.Id,
                a.Name,
                a.Description,
                a.Weight,
                a.Quantity,
                equippedSlot,
                type,
                rarity,
                a.GoldValue,
                modifiers,
                a.Defense,
                a.ArmorClass.ToContract(),
                a.DurabilityCurrent,
                a.DurabilityMax
            ),
            Shield s => new ShieldDetail(
                s.Id,
                s.Name,
                s.Description,
                s.Weight,
                s.Quantity,
                equippedSlot,
                type,
                rarity,
                s.GoldValue,
                modifiers,
                s.BlockChance,
                s.Defense,
                s.DurabilityCurrent,
                s.DurabilityMax
            ),
            Accessory ac => new AccessoryDetail(
                ac.Id,
                ac.Name,
                ac.Description,
                ac.Weight,
                ac.Quantity,
                equippedSlot,
                type,
                rarity,
                ac.GoldValue,
                modifiers
            ),
            Ammunition am => new AmmunitionDetail(
                am.Id,
                am.Name,
                am.Description,
                am.Weight,
                am.Quantity,
                equippedSlot,
                type,
                rarity,
                am.GoldValue,
                modifiers
            ),
            Consumable c => new ConsumableItemDetail(
                c.Id,
                c.Name,
                c.Description,
                c.Weight,
                c.Quantity,
                equippedSlot,
                type,
                rarity,
                c.GoldValue,
                modifiers,
                c.Resource.ToContract(),
                c.RestoreAmount,
                c.Duration
            ),
            Gold g => new GoldDetail(
                g.Id,
                g.Name,
                g.Description,
                g.Weight,
                g.Quantity,
                equippedSlot,
                type,
                rarity,
                g.GoldValue,
                modifiers
            ),
            Key k => new KeyDetail(
                k.Id,
                k.Name,
                k.Description,
                k.Weight,
                k.Quantity,
                equippedSlot,
                type,
                rarity,
                k.GoldValue,
                modifiers
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };
    }

    private static ItemModifierSummary ToItemModifierSummary(ItemModifier modifier) =>
        modifier switch
        {
            AttributeModifier m => new AttributeModifierSummary(
                m.Amount,
                m.Attribute.ToContract(),
                m.AmountType.ToContract()
            ),
            CombatSpeedModifier m => new CombatSpeedModifierSummary(
                m.Amount,
                m.SpeedType.ToContract()
            ),
            ElementalDamageModifier m => new ElementalDamageModifierSummary(
                m.DamageType.ToContract(),
                m.MinDamage,
                m.MaxDamage
            ),
            LeechModifier m => new LeechModifierSummary(m.LeechType.ToContract(), m.Percent),
            SpecialHitModifier m => new SpecialHitModifierSummary(m.Chance, m.HitType.ToContract()),
            SkillBonusModifier m => new SkillBonusModifierSummary(m.Amount, m.Skill?.ToContract()),
            ProcModifier m => new ProcModifierSummary(
                m.AbilityName,
                m.Chance,
                m.Trigger.ToContract()
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(modifier)),
        };

    private static ItemType ToItemType(Item item) =>
        item switch
        {
            Weapon w => w.Type switch
            {
                WeaponType.Dagger => ItemType.Dagger,
                WeaponType.Sword => ItemType.Sword,
                WeaponType.Axe => ItemType.Axe,
                WeaponType.Mace => ItemType.Mace,
                WeaponType.Hammer => ItemType.Hammer,
                WeaponType.Staff => ItemType.Staff,
                WeaponType.Wand => ItemType.Wand,
                WeaponType.Bow => ItemType.Bow,
                WeaponType.Crossbow => ItemType.Crossbow,
                WeaponType.Javelin => ItemType.Javelin,
                WeaponType.GreatSword => ItemType.GreatSword,
                WeaponType.GreatAxe => ItemType.GreatAxe,
                WeaponType.GreatHammer => ItemType.GreatHammer,
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            },
            Armor a => a.Type switch
            {
                ArmorType.Helm => ItemType.Helm,
                ArmorType.Chest => ItemType.Chest,
                ArmorType.Boots => ItemType.Boots,
                ArmorType.Gloves => ItemType.Gloves,
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            },
            Shield => ItemType.Shield,
            Consumable => ItemType.Consumable,
            Ammunition am => am.Type switch
            {
                AmmoType.Arrow => ItemType.Arrow,
                AmmoType.Bolt => ItemType.Bolt,
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            },
            Accessory ac => ac.Type switch
            {
                AccessoryType.Ring => ItemType.Ring,
                AccessoryType.Necklace => ItemType.Necklace,
                AccessoryType.Belt => ItemType.Belt,
                _ => throw new ArgumentOutOfRangeException(nameof(item)),
            },
            Gold => ItemType.Gold,
            Key => ItemType.Key,
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private static Contracts.Inventory.Responses.ItemRarity? ToRarity(Item item) =>
        item switch
        {
            Weapon w => w.Rarity.ToContract(),
            Armor a => a.Rarity.ToContract(),
            Shield s => s.Rarity.ToContract(),
            Consumable c => c.Rarity.ToContract(),
            Ammunition am => am.Rarity.ToContract(),
            Accessory ac => ac.Rarity.ToContract(),
            Gold => null,
            Key => null,
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private static ConsumableSummary ToConsumableSummary(Consumable item) =>
        new(item.Id, item.Name, item.Quantity, item.Resource.ToContract(), item.RestoreAmount);

    private static SkillProgressSummary ToSkillProgressSummary(CreatureSkillProgress progress) =>
        new(
            progress.Skill.ToContract(),
            progress.Level,
            progress.ExperienceCurrent,
            progress.ExperienceToNextLevel
        );
}
