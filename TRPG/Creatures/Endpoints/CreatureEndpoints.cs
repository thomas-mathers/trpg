using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Contracts.Creatures.Responses;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Endpoints;

internal static class CreatureEndpoints
{
    public static void MapCreatureEndpoints(this WebApplication app)
    {
        app.MapGet("/creatures/{creatureId:guid}/abilities", GetAbilities);
        app.MapGet("/creatures/{creatureId:guid}/inventory", GetInventory);
        app.MapGet("/creatures/{creatureId:guid}/consumables", GetConsumables);
        app.MapGet("/creatures/{creatureId:guid}/attribute-points", GetAttributePoints);
        app.MapPost(
            "/creatures/{creatureId:guid}/attribute-points/allocate",
            AllocateAttributePoints
        );
        app.MapGet("/creatures/{creatureId:guid}/attributes", GetBaseAttributes);
        app.MapGet("/creatures/{creatureId:guid}/skills", GetSkills);
        app.MapGet("/creatures/{creatureId:guid}/level", GetLevel);
        app.MapGet("/corpses", GetNearbyCorpses);
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

        return TypedResults.Ok(abilities.Select(ToAbilitySummary).ToArray());
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
            new InventorySummary(snapshot.Gold, snapshot.Items.Select(ToItemSummary).ToArray())
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
        Guid nearPlayerId,
        GetNearbyCorpsesQueryHandler getNearbyCorpses,
        CancellationToken cancellationToken
    )
    {
        var corpses = await getNearbyCorpses.Handle(
            new GetNearbyCorpsesQuery { PlayerId = nearPlayerId },
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
            new AllocateAttributePointsCommand { CreatureId = creatureId, Deltas = request.Deltas },
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

    private static ItemSummary ToItemSummary(Item item) =>
        new(
            item.Id,
            item.Name,
            item.Weight,
            item.Quantity,
            item.Ownership.EquippedSlot?.ToContract(),
            ToItemType(item),
            ToRarity(item)
        );

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
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private static ConsumableSummary ToConsumableSummary(Consumable item) =>
        new(item.Id, item.Name, item.Quantity, item.Resource.ToContract(), item.RestoreAmount);

    private static AbilitySummary ToAbilitySummary(Ability ability) =>
        new(
            ability.Name,
            ability.Skill.ToContract(),
            ability.Description,
            ability.ApCost,
            ability.MpCost,
            ability.Cooldown,
            ability is AttackAbility ? AbilityCategory.Offensive : AbilityCategory.Support
        );

    private static SkillProgressSummary ToSkillProgressSummary(CreatureSkillProgress progress) =>
        new(
            progress.Skill.ToContract(),
            progress.Level,
            progress.ExperienceCurrent,
            progress.ExperienceToNextLevel
        );
}
