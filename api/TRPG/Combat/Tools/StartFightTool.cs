using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns;
using TRPG.Domain.Models;
using TRPG.Tools;

namespace TRPG.Combat.Tools;

internal class StartFightTool(
    GameTurnContext turnContext,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreatureByNameAtLocationQuery, Creature?> getCreatureByNameAtLocation,
    IQueryHandler<
        GetEncounterGroupCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getEncounterGroupCreatureIds,
    ICommandHandler<StartFightCommand> startFight,
    ICommandHandler<RecordAssaultCommand> recordAssault,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ILogger<StartFightTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("attack")]
    [Description(
        "Starts combat with a hostile creature by name. If that creature belongs to a pack, the whole pack joins the fight. Combat begins without resolving an attack; the player chooses the first and all later actions from the combat menu."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact name of the creature to attack, copied verbatim from the most recent look result or combat result."
        )]
            string targetName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[attack] targetName={TargetName}", targetName);
        var stopwatch = Stopwatch.StartNew();

        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = turnContext.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return new ToolError(
                "An encounter is already underway — resolve it through the player's combat menu, not this tool."
            );
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );

        var target = await getCreatureByNameAtLocation.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = player!.WorldId,
                LocationId = player.LocationId,
                Name = targetName,
                ExcludingCreatureId = player.Id,
            },
            cancellationToken
        );

        if (target == null || target.State == CreatureState.Dead)
        {
            return new ToolError(
                $"No '{targetName}' found nearby to attack. Call look to see what's around."
            );
        }

        var hasSurpriseRound = player.IsSneaking || target.State == CreatureState.Sleeping;

        var enemyCreatureIds = await getEncounterGroupCreatureIds.Handle(
            new GetEncounterGroupCreatureIdsQuery
            {
                WorldId = turnContext.WorldId,
                CreatureId = target.Id,
            },
            cancellationToken
        );

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = enemyCreatureIds,
                State = CreatureState.Alerted,
            },
            cancellationToken
        );

        // The player chose to attack, which is what separates assault from self-defence.
        await recordAssault.Handle(
            new RecordAssaultCommand
            {
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                VictimId = target.Id,
            },
            cancellationToken
        );

        await startFight.Handle(
            new StartFightCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                EnemyCreatureIds = enemyCreatureIds,
                HasSurpriseRound = hasSurpriseRound,
            },
            cancellationToken
        );

        logger.LogInformation(
            "[perf] [attack] combat started in {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds
        );
        return new
        {
            Message = "Combat has started. The player will choose the first action from the combat menu.",
        };
    }
}
