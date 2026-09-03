using TRPG.Application.Common.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateEncountersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

public record EncounterEvaluationResult(
    HostileEncounter? HostileEncounter,
    GuardEncounter? GuardEncounter,
    HostileEncounter? TrespassingEncounter = null
)
{
    public static readonly EncounterEvaluationResult None = new(null, null);
}

internal class EvaluateEncountersCommandHandler(
    ICommandHandler<EvaluateHostileEncounterCommand, HostileEncounter?> evaluateHostileEncounter,
    ICommandHandler<EvaluateGuardEncounterCommand, GuardEncounter?> evaluateGuardEncounter,
    ICommandHandler<
        EvaluateTrespassingEncounterCommand,
        HostileEncounter?
    > evaluateTrespassingEncounter
) : ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult>
{
    public async Task<EncounterEvaluationResult> Handle(
        EvaluateEncountersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var hostileEncounter = await evaluateHostileEncounter.Handle(
            new EvaluateHostileEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
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
        if (guardEncounter != null)
        {
            return new EncounterEvaluationResult(null, guardEncounter);
        }

        var trespassingEncounter = await evaluateTrespassingEncounter.Handle(
            new EvaluateTrespassingEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        return new EncounterEvaluationResult(null, null, trespassingEncounter);
    }
}
