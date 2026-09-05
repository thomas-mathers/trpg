using TRPG.Application.Common.Commands;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateMoveInterceptionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid FromLocationId { get; init; }
    public required Guid ToLocationId { get; init; }
    public required TimeSpan Playtime { get; init; }
}

internal class EvaluateMoveInterceptionCommandHandler(
    ICommandHandler<
        EvaluateOverdueRoomKeyEncounterCommand,
        ConfrontOverdueRoomKeyResult
    > evaluateOverdueRoomKeyEncounter
) : ICommandHandler<EvaluateMoveInterceptionCommand, EncounterEvaluationResult>
{
    public async Task<EncounterEvaluationResult> Handle(
        EvaluateMoveInterceptionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var overdueRoomKeyEncounter = await evaluateOverdueRoomKeyEncounter.Handle(
            new EvaluateOverdueRoomKeyEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                FromLocationId = command.FromLocationId,
                ToLocationId = command.ToLocationId,
                Playtime = command.Playtime,
            },
            cancellationToken
        );

        return new EncounterEvaluationResult(overdueRoomKeyEncounter.Encounter);
    }
}
