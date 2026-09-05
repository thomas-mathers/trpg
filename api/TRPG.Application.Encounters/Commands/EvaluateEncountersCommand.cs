using TRPG.Application.Common.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateEncountersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

public record EncounterEvaluationResult(Encounter? Encounter)
{
    public static readonly EncounterEvaluationResult None = new((Encounter?)null);
}

internal class EvaluateEncountersCommandHandler(
    ICommandHandler<EvaluateHostileEncounterCommand, HostileEncounter?> evaluateHostileEncounter,
    ICommandHandler<EvaluateGuardEncounterCommand, GuardEncounter?> evaluateGuardEncounter,
    ICommandHandler<
        EvaluateSuspicionEncounterCommand,
        SuspicionEncounter?
    > evaluateSuspicionEncounter,
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
            return new EncounterEvaluationResult(hostileEncounter);
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
            return new EncounterEvaluationResult(guardEncounter);
        }

        var suspicionEncounter = await evaluateSuspicionEncounter.Handle(
            new EvaluateSuspicionEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );
        if (suspicionEncounter != null)
        {
            return new EncounterEvaluationResult(suspicionEncounter);
        }

        var trespassingEncounter = await evaluateTrespassingEncounter.Handle(
            new EvaluateTrespassingEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        return new EncounterEvaluationResult(trespassingEncounter);
    }
}
