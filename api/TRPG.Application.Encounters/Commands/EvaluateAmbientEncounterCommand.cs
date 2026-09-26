using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateAmbientEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> SpawnedGroupIds { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class EvaluateAmbientEncounterCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<EvaluateEncounterGroupCommand, Encounter?> evaluateEncounterGroup,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted
) : ICommandHandler<EvaluateAmbientEncounterCommand>
{
    public async Task Handle(
        EvaluateAmbientEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.SpawnedGroupIds.Count == 0)
        {
            return;
        }

        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);
        if (player.State == CreatureState.Dead)
        {
            return;
        }

        // An unresolved encounter or fight already owns the player's attention.
        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var encounter = await evaluateEncounterGroup.Handle(
            new EvaluateEncounterGroupCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GroupIds = command.SpawnedGroupIds,
            },
            cancellationToken
        );
        if (encounter != null)
        {
            await publishEncounterStarted.Handle(
                new PublishEncounterStartedCommand
                {
                    PlayerId = command.PlayerId,
                    Encounter = encounter,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );
        }

        transaction.Complete();
    }
}
