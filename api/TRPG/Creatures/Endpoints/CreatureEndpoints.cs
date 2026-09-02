using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Abilities.Mappers;
using TRPG.Abilities.Responses;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Results;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Creatures.Mappers;
using TRPG.Creatures.Requests;
using TRPG.Creatures.Responses;
using TRPG.Domain.Models;
using TRPG.Inventory.Mappers;
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
        app.MapGet("/creatures/{creatureId:guid}/equipment/preview", GetEquipItemStats)
            .WithName("PreviewCreatureEquipment");
        app.MapGet(
                "/creatures/{creatureId:guid}/equipment/preview/basic-attack-damage",
                GetEquipItemBasicAttackDamage
            )
            .WithName("PreviewCreatureBasicAttackDamage");
        app.MapGet("/players/{playerId:guid}/nearby-corpses", GetNearbyCorpses)
            .WithName("GetNearbyCorpses");
        app.MapGet("/players/{playerId:guid}/world-map", GetWorldMap).WithName("GetWorldMap");
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
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] IQueryHandler<GetInventoryByOwnerQuery, InventoryResult> getInventoryByOwner,
        [FromServices]
            IQueryHandler<
            GetActiveQuestItemIdsQuery,
            IReadOnlyCollection<Guid>
        > getActiveQuestItemIds,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );

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

        return TypedResults.Ok(snapshot.ToSummary(questItemIds, creature?.CarryingCapacity));
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

    private static async Task<Ok<WorldMapResponse>> GetWorldMap(
        Guid playerId,
        [FromServices] IQueryHandler<GetWorldMapQuery, WorldMapResult> getWorldMap,
        CancellationToken cancellationToken
    )
    {
        var map = await getWorldMap.Handle(
            new GetWorldMapQuery { PlayerId = playerId },
            cancellationToken
        );

        return TypedResults.Ok(
            new WorldMapResponse(
                map.Countries.Select(ToCountryMapResponse).ToArray(),
                map.States.Select(ToStateMapResponse).ToArray(),
                map.Cities.Select(ToCityMapResponse).ToArray(),
                map.Roads.Select(ToRoadMapResponse).ToArray(),
                map.PlayerStateId,
                map.Corpses.Select(ToCorpseMapResponse).ToArray(),
                map.QuestMarkers.Select(ToQuestMapResponse).ToArray()
            )
        );
    }

    private static PointResponse ToPointResponse(Point point) => new(point.X, point.Y);

    private static CountryMapResponse ToCountryMapResponse(Country country) =>
        new(country.Id, country.Name, country.Boundary.Points.Select(ToPointResponse).ToArray());

    private static StateMapResponse ToStateMapResponse(State state) =>
        new(
            state.Id,
            state.CountryId,
            state.Name,
            state.Description,
            ToPointResponse(state.Center),
            state.Boundary.Points.Select(ToPointResponse).ToArray()
        );

    private static CityMapResponse ToCityMapResponse(City city) =>
        new(city.Id, city.StateId, city.Name, city.IsCapital);

    private static RoadMapResponse ToRoadMapResponse(WorldMapRoad road) =>
        new(road.Id, road.Name, road.OriginStateId, road.DestinationStateId);

    private static CorpseMapResponse ToCorpseMapResponse(WorldMapCorpse corpse) =>
        new(corpse.Id, corpse.Name, corpse.StateId, corpse.ItemCount);

    private static QuestMapResponse ToQuestMapResponse(WorldMapQuestMarker marker) =>
        new(marker.QuestId, marker.ObjectiveName, marker.StateId);

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
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        CancellationToken cancellationToken
    )
    {
        await EnsureCreatureExists(creatureId, getCreatureById, cancellationToken);

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
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        CancellationToken cancellationToken
    )
    {
        await EnsureCreatureExists(creatureId, getCreatureById, cancellationToken);

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

    private static async Task<Ok<EffectiveAttributesResponse>> GetEquipItemStats(
        Guid creatureId,
        Guid itemId,
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] IQueryHandler<GetEquipItemStatsQuery, Attributes> getEquipItemStats,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );

        var attributes = await getEquipItemStats.Handle(
            new GetEquipItemStatsQuery
            {
                CreatureId = creatureId,
                ItemId = itemId,
                Slot = slot.ToDataModel(),
                BaseAttributes = creature!.BaseAttributes,
                ActiveBuffs = StatFormulas.ToActiveBuffs(creature),
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

    private static async Task<Ok<BasicAttackDamageResponse>> GetEquipItemBasicAttackDamage(
        Guid creatureId,
        Guid itemId,
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices]
            IQueryHandler<GetEquipItemBasicAttackDamageQuery, float> getEquipItemBasicAttackDamage,
        CancellationToken cancellationToken
    )
    {
        var damagePerTurn = await getEquipItemBasicAttackDamage.Handle(
            new GetEquipItemBasicAttackDamageQuery
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

    private static async Task EnsureCreatureExists(
        Guid creatureId,
        IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );
        if (creature == null)
        {
            throw new EntityNotFoundException(nameof(Creature), creatureId);
        }
    }
}
