using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
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
    > getCreatureJobsByCreatureIds,
    IQueryHandler<GetTravelTopologyQuery, TravelTopology> getTravelTopology,
    IOptions<WorldClockOptions> clockOptions
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

        var generated = await Generate(command.WorldId, cancellationToken);
        context.Routes.AddRange(generated.Routes);
        context.RouteSteps.AddRange(generated.Steps);
        context.CreatureRouteSchedules.AddRange(generated.Schedules);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<CreatureRouteScheduleGeneratorResult> Generate(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var summaries = await getCreaturesInWorld.Handle(
            new GetCreaturesInWorldQuery { WorldId = worldId },
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

        var topology = await getTravelTopology.Handle(
            new GetTravelTopologyQuery { WorldId = worldId },
            cancellationToken
        );
        return CreatureRouteScheduleGenerator.Generate(
            worldId,
            creatures.Values.ToArray(),
            jobsByCreatureId.Values.SelectMany(jobs => jobs).ToArray(),
            topology.ToGraph(),
            clockOptions.Value.TimeScale
        );
    }
}
