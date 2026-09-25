using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public class EnsureCreatureRouteSchedulesCommand
{
    public required Guid WorldId { get; init; }
}

internal class EnsureCreatureRouteSchedulesCommandHandler(
    IRoutingDbContext context,
    IQueryHandler<
        GetCreaturesInWorldQuery,
        IReadOnlyCollection<CreatureSummary>
    > getCreaturesInWorld,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getCreatureJobsByCreatureIds
) : ICommandHandler<EnsureCreatureRouteSchedulesCommand>
{
    public async Task Handle(
        EnsureCreatureRouteSchedulesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (
            await context.CreatureRouteSchedules.AnyAsync(
                schedule => schedule.WorldId == command.WorldId,
                cancellationToken
            )
        )
        {
            return;
        }

        var summaries = await getCreaturesInWorld.Handle(
            new GetCreaturesInWorldQuery { WorldId = command.WorldId },
            cancellationToken
        );
        var creatureIds = summaries.Select(creature => creature.Id).ToArray();
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = creatureIds },
            cancellationToken
        );
        var jobsByCreatureId = await getCreatureJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery { CreatureIds = creatureIds },
            cancellationToken
        );

        var connectors = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == command.WorldId)
            .ToArrayAsync(cancellationToken);
        var travelConnectors = await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == command.WorldId)
            .ToArrayAsync(cancellationToken);
        var generated = CreatureRouteScheduleGenerator.Generate(
            command.WorldId,
            creatures.Values.ToArray(),
            jobsByCreatureId.Values.SelectMany(jobs => jobs).ToArray(),
            connectors,
            travelConnectors
        );
        context.Routes.AddRange(generated.Routes);
        context.RouteSteps.AddRange(generated.Steps);
        context.CreatureRouteSchedules.AddRange(generated.Schedules);
        await context.SaveChangesAsync(cancellationToken);
    }
}
