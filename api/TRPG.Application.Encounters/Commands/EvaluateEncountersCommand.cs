using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateEncountersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public Guid? OriginLocationId { get; init; }
}

public record EncounterEvaluationResult(
    HostileEncounterState? HostileEncounter,
    GuardEncounterState? GuardEncounter
)
{
    public static readonly EncounterEvaluationResult None = new(null, null);
}

internal class EvaluateEncountersCommandHandler(
    IQueryHandler<GetActiveEncounterQuery, HostileEncounter?> getActiveEncounter,
    IQueryHandler<GetActiveGuardEncounterQuery, GuardEncounter?> getActiveGuardEncounter,
    IQueryHandler<GetActiveFightQuery, Fight?> getActiveFight,
    ICommandHandler<
        EvaluateHostileEncounterCommand,
        HostileEncounterState?
    > evaluateHostileEncounter,
    ICommandHandler<EvaluateGuardEncounterCommand, GuardEncounterState?> evaluateGuardEncounter
) : ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult>
{
    public async Task<EncounterEvaluationResult> Handle(
        EvaluateEncountersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return EncounterEvaluationResult.None;
        }

        var activeGuardEncounter = await getActiveGuardEncounter.Handle(
            new GetActiveGuardEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeGuardEncounter != null)
        {
            return EncounterEvaluationResult.None;
        }

        var activeFight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeFight != null)
        {
            return EncounterEvaluationResult.None;
        }

        var hostileEncounter = await evaluateHostileEncounter.Handle(
            new EvaluateHostileEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                OriginLocationId = command.OriginLocationId,
            },
            cancellationToken
        );
        if (hostileEncounter != null)
        {
            return new EncounterEvaluationResult(hostileEncounter, null);
        }

        var guardEncounter = await evaluateGuardEncounter.Handle(
            new EvaluateGuardEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        return new EncounterEvaluationResult(null, guardEncounter);
    }
}
