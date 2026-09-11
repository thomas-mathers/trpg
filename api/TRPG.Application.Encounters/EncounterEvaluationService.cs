using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal class EncounterEvaluationService(
    ICommandHandler<EvaluateHostileEncounterCommand, HostileEncounter?> evaluateHostileEncounter,
    ICommandHandler<EvaluateJailbreakEncounterCommand, GuardEncounter?> evaluateJailbreakEncounter,
    ICommandHandler<EvaluateGuardEncounterCommand, GuardEncounter?> evaluateGuardEncounter,
    ICommandHandler<
        EvaluateSuspicionEncounterCommand,
        SuspicionEncounter?
    > evaluateSuspicionEncounter,
    ICommandHandler<EvaluateTrapEncounterCommand, TrapEncounter?> evaluateTrapEncounter,
    ICommandHandler<
        EvaluateTrespassingEncounterCommand,
        HostileEncounter?
    > evaluateTrespassingEncounter
)
{
    private async Task<EncounterEvaluationResult> EvaluateConfrontation(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken = default
    )
    {
        var hostileEncounter = await evaluateHostileEncounter.Handle(
            new EvaluateHostileEncounterCommand { WorldId = worldId, PlayerId = playerId },
            cancellationToken
        );
        if (hostileEncounter != null)
        {
            return new EncounterEvaluationResult(hostileEncounter);
        }

        // Being caught escaping outranks a routine stop, and does not wait on standing reputation.
        var jailbreakEncounter = await evaluateJailbreakEncounter.Handle(
            new EvaluateJailbreakEncounterCommand { WorldId = worldId, PlayerId = playerId },
            cancellationToken
        );
        if (jailbreakEncounter != null)
        {
            return new EncounterEvaluationResult(jailbreakEncounter);
        }

        return await EvaluateGuardConfrontation(worldId, playerId, cancellationToken);
    }

    private async Task<EncounterEvaluationResult> EvaluateGuardConfrontation(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var guardEncounter = await evaluateGuardEncounter.Handle(
            new EvaluateGuardEncounterCommand { WorldId = worldId, PlayerId = playerId },
            cancellationToken
        );
        if (guardEncounter != null)
        {
            return new EncounterEvaluationResult(guardEncounter);
        }

        var suspicionEncounter = await evaluateSuspicionEncounter.Handle(
            new EvaluateSuspicionEncounterCommand { WorldId = worldId, PlayerId = playerId },
            cancellationToken
        );
        if (suspicionEncounter != null)
        {
            return new EncounterEvaluationResult(suspicionEncounter);
        }

        return EncounterEvaluationResult.None;
    }

    public async Task<EncounterEvaluationResult> EvaluateArrival(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken = default
    )
    {
        var confrontation = await EvaluateConfrontation(worldId, playerId, cancellationToken);
        if (confrontation.Encounter != null)
        {
            return confrontation;
        }

        var trapEncounter = await evaluateTrapEncounter.Handle(
            new EvaluateTrapEncounterCommand { WorldId = worldId, PlayerId = playerId },
            cancellationToken
        );
        if (trapEncounter != null)
        {
            return new EncounterEvaluationResult(trapEncounter);
        }

        return await EvaluateTrespassing(worldId, playerId, cancellationToken);
    }

    public async Task<EncounterEvaluationResult> EvaluateDeparture(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken = default
    )
    {
        var confrontation = await EvaluateConfrontation(worldId, playerId, cancellationToken);
        return confrontation.Encounter != null
            ? confrontation
            : await EvaluateTrespassing(worldId, playerId, cancellationToken);
    }

    private async Task<EncounterEvaluationResult> EvaluateTrespassing(
        Guid worldId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var trespassingEncounter = await evaluateTrespassingEncounter.Handle(
            new EvaluateTrespassingEncounterCommand { WorldId = worldId, PlayerId = playerId },
            cancellationToken
        );

        return new EncounterEvaluationResult(trespassingEncounter);
    }
}
