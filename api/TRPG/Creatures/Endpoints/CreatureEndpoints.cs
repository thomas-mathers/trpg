using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Abilities.Mappers;
using TRPG.Abilities.Responses;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Results;
using TRPG.Application.Quests.Queries;
using TRPG.Creatures.Mappers;
using TRPG.Creatures.Requests;
using TRPG.Creatures.Responses;
using TRPG.Domain.Models;
using TRPG.Inventory.Requests;
using TRPG.Inventory.Responses;

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
        [FromServices]
            IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
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
        [FromServices] IQueryHandler<GetInventoryByOwnerQuery, InventoryResult> getInventoryByOwner,
        [FromServices]
            IQueryHandler<
            GetActiveQuestItemIdsQuery,
            IReadOnlyCollection<Guid>
        > getActiveQuestItemIds,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(creatureId, OwnerType.Creature),
            },
            cancellationToken
        );

        var questItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(
            new InventorySummary(snapshot.Gold, snapshot.Items.ToDetails(questItemIds))
        );
    }

    private static async Task<Ok<ConsumableSummary[]>> GetConsumables(
        Guid creatureId,
        [FromServices]
            IQueryHandler<
            GetInventoryItemsByOwnerQuery,
            IReadOnlyList<Item>
        > getInventoryItemsByOwner,
        CancellationToken cancellationToken
    )
    {
        var items = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(creatureId, OwnerType.Creature),
            },
            cancellationToken
        );

        return TypedResults.Ok(
            items.OfType<Consumable>().Select(item => item.ToSummary()).ToArray()
        );
    }

    private static async Task<Ok<NearbyCorpseSummary[]>> GetNearbyCorpses(
        Guid playerId,
        [FromServices]
            IQueryHandler<GetNearbyCorpsesQuery, IReadOnlyList<CorpseResult>> getNearbyCorpses,
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
        [FromServices]
            IQueryHandler<GetUnallocatedAttributePointsQuery, int> getUnallocatedAttributePoints,
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
        [FromServices] ICommandHandler<AllocateAttributePointsCommand> allocateAttributePoints,
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
        [FromServices] ICommandHandler<EquipInventoryItemCommand> equipInventoryItem,
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
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices] ICommandHandler<UnequipInventoryItemCommand> unequipInventoryItem,
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
        [FromServices]
            IQueryHandler<GetCreatureBaseAttributesQuery, Attributes> getCreatureBaseAttributes,
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
        [FromServices]
            IQueryHandler<GetCreatureEffectiveStatsQuery, Attributes> getCreatureEffectiveStats,
        CancellationToken cancellationToken
    )
    {
        var attributes = await getCreatureEffectiveStats.Handle(
            new GetCreatureEffectiveStatsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(attributes.ToResponse());
    }

    private static async Task<Ok<EffectiveAttributesResponse>> PreviewEquipItemStats(
        Guid creatureId,
        Guid itemId,
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices] IQueryHandler<PreviewEquipItemStatsQuery, Attributes> previewEquipItemStats,
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

        return TypedResults.Ok(attributes.ToResponse());
    }

    private static async Task<Ok<BasicAttackDamageResponse>> GetBasicAttackDamage(
        Guid creatureId,
        [FromServices]
            IQueryHandler<GetCreatureBasicAttackDamageQuery, float> getCreatureBasicAttackDamage,
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
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices]
            IQueryHandler<
            PreviewEquipItemBasicAttackDamageQuery,
            float
        > previewEquipItemBasicAttackDamage,
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

    private static async Task<Ok<SkillProgressSummary[]>> GetSkills(
        Guid creatureId,
        [FromServices]
            IQueryHandler<
            GetCreatureSkillsQuery,
            IReadOnlyCollection<CreatureSkillProgress>
        > getCreatureSkills,
        CancellationToken cancellationToken
    )
    {
        var skills = await getCreatureSkills.Handle(
            new GetCreatureSkillsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(skills.Select(skill => skill.ToSummary()).ToArray());
    }

    private static async Task<Ok<CreatureLevelResponse>> GetLevel(
        Guid creatureId,
        [FromServices] IQueryHandler<GetCreatureLevelQuery, int> getCreatureLevel,
        CancellationToken cancellationToken
    )
    {
        var level = await getCreatureLevel.Handle(
            new GetCreatureLevelQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(new CreatureLevelResponse(level));
    }

    internal static ItemDetail ToItemDetail(Item item, bool isQuestItem = false)
    {
        var equippedSlot = item.Ownership.EquippedSlot?.ToResponse();
        var type = ToItemType(item);
        var rarity = ToRarity(item);
        var modifiers = item.Modifiers.Select(ToItemModifierSummary).ToArray();
        var isStackable = ItemStackability.IsStackable(item);

        ItemDetail detail = item switch
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
                isStackable,
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
                isStackable,
                a.Defense,
                a.ArmorClass.ToResponse(),
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
                isStackable,
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
                modifiers,
                isStackable
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
                modifiers,
                isStackable
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
                isStackable,
                c.Resource.ToResponse(),
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
                modifiers,
                isStackable
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
                modifiers,
                isStackable
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

        return detail with
        {
            IsQuestItem = isQuestItem,
        };
    }

    internal static ItemDetail[] ToItemDetails(
        IEnumerable<Item> items,
        IReadOnlyCollection<Guid> questItemIds
    ) => items.Select(item => ToItemDetail(item, questItemIds.Contains(item.Id))).ToArray();

    private static ItemModifierSummary ToItemModifierSummary(ItemModifier modifier) =>
        modifier switch
        {
            AttributeModifier m => new AttributeModifierSummary(
                m.Amount,
                m.Attribute,
                m.AmountType
            ),
            CombatSpeedModifier m => new CombatSpeedModifierSummary(
                m.Amount,
                m.SpeedType.ToResponse()
            ),
            ElementalDamageModifier m => new ElementalDamageModifierSummary(
                m.DamageType,
                m.MinDamage,
                m.MaxDamage
            ),
            LeechModifier m => new LeechModifierSummary(m.LeechType.ToResponse(), m.Percent),
            SpecialHitModifier m => new SpecialHitModifierSummary(m.Chance, m.HitType.ToResponse()),
            SkillBonusModifier m => new SkillBonusModifierSummary(m.Amount, m.Skill?.ToResponse()),
            ProcModifier m => new ProcModifierSummary(
                m.AbilityName,
                m.Chance,
                m.Trigger.ToResponse()
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

    private static TRPG.Inventory.Responses.ItemRarity? ToRarity(Item item) =>
        item switch
        {
            Weapon w => w.Rarity.ToResponse(),
            Armor a => a.Rarity.ToResponse(),
            Shield s => s.Rarity.ToResponse(),
            Consumable c => c.Rarity.ToResponse(),
            Ammunition am => am.Rarity.ToResponse(),
            Accessory ac => ac.Rarity.ToResponse(),
            Gold => null,
            Key => null,
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private static ConsumableSummary ToConsumableSummary(Consumable item) =>
        new(item.Id, item.Name, item.Quantity, item.Resource.ToResponse(), item.RestoreAmount);

    private static SkillProgressSummary ToSkillProgressSummary(CreatureSkillProgress progress) =>
        new(
            progress.Skill.ToResponse(),
            progress.Level,
            progress.ExperienceCurrent,
            progress.ExperienceToNextLevel
        );
}
