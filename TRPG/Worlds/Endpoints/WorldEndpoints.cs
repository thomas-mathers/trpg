using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TickerQ.Utilities;
using TickerQ.Utilities.Interfaces.Managers;
using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;
using TRPG.Data;
using TRPG.Worlds.Jobs;
using ActiveBuff = TRPG.Contracts.Combat.Responses.ActiveBuff;
using ActiveDot = TRPG.Contracts.Combat.Responses.ActiveDot;
using ActiveHot = TRPG.Contracts.Combat.Responses.ActiveHot;
using CombatantState = TRPG.Contracts.Combat.Responses.CombatantState;

namespace TRPG.Worlds.Endpoints;

internal static class WorldEndpoints
{
    public static void MapWorldEndpoints(this WebApplication app)
    {
        app.MapPost("/worlds", CreateWorld);
        app.MapGet("/worlds", ListWorlds);
        app.MapDelete("/worlds/{worldId:guid}", DropWorld);
        app.MapGet("/worlds/{worldId:guid}/fight", GetFight);
        app.MapGet("/worlds/{worldId:guid}/abilities", GetAbilities);
    }

    private static async Task<IResult> CreateWorld(
        CreateWorldRequest request,
        ITimeTickerManager<TrpgTimeTicker> timeTicker,
        CancellationToken cancellationToken
    )
    {
        var command = new CreateWorldCommand
        {
            WorldInput = new WorldGeneratorInput
            {
                Description = request.Description,
                MinCityStates = request.MinCityStates,
                MaxCityStates = request.MaxCityStates,
                MinRuralStates = request.MinRuralStates,
                MaxRuralStates = request.MaxRuralStates,
                MinBuildingsPerState = request.MinBuildingsPerState,
                MaxBuildingsPerState = request.MaxBuildingsPerState,
                MinFactionMembers = request.MinFactionMembers,
                MaxFactionMembers = request.MaxFactionMembers,
                HousesPerCity = request.HousesPerCity,
                MinHouseholdSize = request.MinHouseholdSize,
                MaxHouseholdSize = request.MaxHouseholdSize,
                FactionCount = request.FactionCount,
            },
            Race = request.Race,
            PlayerClass = request.PlayerClass,
            Name = request.PlayerName,
            Age = request.Age,
            Gender = request.Gender,
        };

        var result = await timeTicker.AddAsync(
            new TrpgTimeTicker
            {
                Request = TickerHelper.CreateTickerRequest(command),
                ExecutionTime = DateTime.UtcNow,
                Function = TickerFunctionProvider.GetFunctionName<CreateWorldJob>(),
                Retries = 3,
                RetryIntervals = [1, 2, 4],
            },
            cancellationToken
        );

        var jobId = result.Result.Id;
        return Results.Accepted($"/jobs/{jobId}", new EnqueueJobResponse(jobId));
    }

    private static async Task<IResult> ListWorlds(
        GetAllWorldsQueryHandler getAllWorlds,
        CancellationToken cancellationToken
    )
    {
        var worlds = await getAllWorlds.Handle(new GetAllWorldsQuery(), cancellationToken);
        return Results.Ok(
            worlds.Select(w => new WorldSummary(w.Id, w.Name, w.PlayerId != null)).ToArray()
        );
    }

    private static async Task<IResult> DropWorld(
        Guid worldId,
        DropWorldCommandHandler dropHandler,
        CancellationToken cancellationToken
    )
    {
        await dropHandler.Handle(new DropWorldCommand { WorldId = worldId }, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetFight(
        Guid worldId,
        GetWorldQueryHandler getWorld,
        GetCombatantsQueryHandler getCombatants,
        CancellationToken cancellationToken
    )
    {
        var world = await getWorld.Handle(
            new GetWorldQuery { WorldId = worldId },
            cancellationToken
        );
        if (world?.PlayerId == null)
        {
            return Results.NotFound();
        }

        var combatants = await getCombatants.Handle(
            new GetCombatantsQuery { WorldId = worldId, PlayerId = world.PlayerId.Value },
            cancellationToken
        );
        if (combatants.Count == 0)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToFightState(combatants));
    }

    private static async Task<IResult> GetAbilities(
        Guid worldId,
        GetWorldQueryHandler getWorld,
        GetPlayerAbilitiesQueryHandler getPlayerAbilities,
        CancellationToken cancellationToken
    )
    {
        var world = await getWorld.Handle(
            new GetWorldQuery { WorldId = worldId },
            cancellationToken
        );
        if (world?.PlayerId == null)
        {
            return Results.NotFound();
        }

        var abilities = await getPlayerAbilities.Handle(
            new GetPlayerAbilitiesQuery { WorldId = worldId, PlayerId = world.PlayerId.Value },
            cancellationToken
        );

        return Results.Ok(abilities.Select(ToAbilitySummary).ToArray());
    }

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

    private static FightState ToFightState(IReadOnlyList<Combatant> combatants) =>
        new(
            combatants
                .Select(c => new CombatantState(
                    Name: c.Name,
                    IsPlayer: c.IsPlayer,
                    IsAlive: c.IsAlive,
                    CurrentHp: c.CurrentHp,
                    MaximumHp: c.MaximumHp,
                    CurrentAp: c.CurrentAp,
                    MaximumAp: c.MaximumAp,
                    CurrentMp: c.CurrentMp,
                    MaximumMp: c.MaximumMp,
                    ActiveConditions: c.ActiveConditions.Where(kv => kv.Value > 0)
                        .ToDictionary(kv => kv.Key.ToContract(), kv => kv.Value),
                    ActiveDots: c.ActiveDots.Select(d => new ActiveDot(
                            d.AbilityName,
                            d.Amount,
                            d.DamageType.ToContract(),
                            d.RemainingTurns
                        ))
                        .ToArray(),
                    ActiveHots: c.ActiveHots.Select(h => new ActiveHot(
                            h.AbilityName,
                            h.Amount,
                            h.RemainingTurns
                        ))
                        .ToArray(),
                    ActiveBuffs: c.ActiveBuffs.Select(b => new ActiveBuff(
                            b.AbilityName,
                            b.Attribute.ToContract(),
                            b.Amount,
                            b.AmountType.ToContract(),
                            b.RemainingTurns
                        ))
                        .ToArray()
                ))
                .ToArray()
        );
}
