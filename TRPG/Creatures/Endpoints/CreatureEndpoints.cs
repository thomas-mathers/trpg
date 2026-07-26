using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

    private static async Task<IResult> GetAbilities(
        Guid creatureId,
        GetCreatureAbilitiesQueryHandler getCreatureAbilities,
        CancellationToken cancellationToken
    )
    {
        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(abilities.Select(ToAbilitySummary).ToArray());
    }

    private static async Task<IResult> GetInventory(
        Guid creatureId,
        bool? consumableOnly,
        GetInventorySummaryQueryHandler getInventorySummary,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await getInventorySummary.Handle(
            new GetInventorySummaryQuery
            {
                CreatureId = creatureId,
                ConsumableOnly = consumableOnly ?? false,
            },
            cancellationToken
        );

        return Results.Ok(
            new InventorySummary(
                snapshot.Gold,
                snapshot.Items.Select(ToInventoryItemSummary).ToArray()
            )
        );
    }

    private static async Task<IResult> GetNearbyCorpses(
        Guid nearPlayerId,
        GetNearbyCorpsesQueryHandler getNearbyCorpses,
        CancellationToken cancellationToken
    )
    {
        var corpses = await getNearbyCorpses.Handle(
            new GetNearbyCorpsesQuery { PlayerId = nearPlayerId },
            cancellationToken
        );

        return Results.Ok(corpses.Select(c => new NearbyCorpseSummary(c.Id, c.Name)).ToArray());
    }

    private static async Task<IResult> GetAttributePoints(
        Guid creatureId,
        GetUnallocatedAttributePointsQueryHandler getUnallocatedAttributePoints,
        CancellationToken cancellationToken
    )
    {
        var points = await getUnallocatedAttributePoints.Handle(
            new GetUnallocatedAttributePointsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(new AttributePointsResponse(points));
    }

    private static async Task<IResult> AllocateAttributePoints(
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

        return Results.NoContent();
    }

    private static async Task<IResult> GetBaseAttributes(
        Guid creatureId,
        GetCreatureBaseAttributesQueryHandler getCreatureBaseAttributes,
        CancellationToken cancellationToken
    )
    {
        var attributes = await getCreatureBaseAttributes.Handle(
            new GetCreatureBaseAttributesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(
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

    private static async Task<IResult> GetSkills(
        Guid creatureId,
        GetCreatureSkillsQueryHandler getCreatureSkills,
        CancellationToken cancellationToken
    )
    {
        var skills = await getCreatureSkills.Handle(
            new GetCreatureSkillsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(skills.Select(ToSkillProgressSummary).ToArray());
    }

    private static async Task<IResult> GetLevel(
        Guid creatureId,
        GetCreatureLevelQueryHandler getCreatureLevel,
        CancellationToken cancellationToken
    )
    {
        var level = await getCreatureLevel.Handle(
            new GetCreatureLevelQuery { CreatureId = creatureId },
            cancellationToken
        );

        return Results.Ok(new CreatureLevelResponse(level));
    }

    private static InventoryItemSummary ToInventoryItemSummary(InventoryItem inventoryItem) =>
        inventoryItem.Item switch
        {
            WeaponItem w => ToSummary(w, inventoryItem.Quantity),
            ArmorItem a => ToSummary(a, inventoryItem.Quantity),
            ShieldItem s => ToSummary(s, inventoryItem.Quantity),
            ConsumableItem c => ToSummary(c, inventoryItem.Quantity),
            AmmunitionItem am => ToSummary(am, inventoryItem.Quantity),
            AccessoryItem ac => ToSummary(ac, inventoryItem.Quantity),
            _ => throw new ArgumentOutOfRangeException(nameof(inventoryItem)),
        };

    private static WeaponItemSummary ToSummary(WeaponItem item, int quantity) =>
        new(
            item.Id,
            item.Name,
            quantity,
            item.Rarity.ToContract(),
            item.Type.ToContract(),
            item.MinDamage,
            item.MaxDamage
        );

    private static ArmorItemSummary ToSummary(ArmorItem item, int quantity) =>
        new(
            item.Id,
            item.Name,
            quantity,
            item.Rarity.ToContract(),
            item.Type.ToContract(),
            item.Defense
        );

    private static ShieldItemSummary ToSummary(ShieldItem item, int quantity) =>
        new(item.Id, item.Name, quantity, item.Rarity.ToContract(), item.Defense, item.BlockChance);

    private static ConsumableItemSummary ToSummary(ConsumableItem item, int quantity) =>
        new(
            item.Id,
            item.Name,
            quantity,
            item.Rarity.ToContract(),
            item.Resource.ToContract(),
            item.Amount
        );

    private static AmmunitionItemSummary ToSummary(AmmunitionItem item, int quantity) =>
        new(item.Id, item.Name, quantity, item.Rarity.ToContract(), item.Type.ToContract());

    private static AccessoryItemSummary ToSummary(AccessoryItem item, int quantity) =>
        new(item.Id, item.Name, quantity, item.Rarity.ToContract(), item.Type.ToContract());

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
