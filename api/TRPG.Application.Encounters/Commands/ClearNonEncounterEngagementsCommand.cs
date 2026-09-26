using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ClearNonEncounterEngagementsCommand
{
    public required Guid WorldId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class ClearNonEncounterEngagementsCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<
        GetEngagedCreatureIdsByWorldQuery,
        IReadOnlyCollection<Guid>
    > getEngagedCreatureIds,
    ICommandHandler<ReleaseCreaturesCommand> releaseCreatures
) : ICommandHandler<ClearNonEncounterEngagementsCommand>
{
    public async Task Handle(
        ClearNonEncounterEngagementsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var activeEncounters = await context
            .Encounters.AsNoTracking()
            .Where(encounter =>
                encounter.WorldId == command.WorldId && encounter.State == EncounterState.Active
            )
            .ToArrayAsync(cancellationToken);
        var preservedIds = activeEncounters
            .SelectMany(EncounterEngagementManager.GetParticipantIds)
            .ToHashSet();
        var engagedIds = await getEngagedCreatureIds.Handle(
            new GetEngagedCreatureIdsByWorldQuery { WorldId = command.WorldId },
            cancellationToken
        );
        var releasedIds = engagedIds.Where(id => !preservedIds.Contains(id)).ToArray();
        await releaseCreatures.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = command.WorldId,
                CreatureIds = releasedIds,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
