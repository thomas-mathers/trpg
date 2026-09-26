using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Commands;

public class MaterializeScheduledRouteTravelersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class MaterializeScheduledRouteTravelersCommandHandler(
    IRoutingDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getCreatureJobsByCreatureIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
) : ICommandHandler<MaterializeScheduledRouteTravelersCommand, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        MaterializeScheduledRouteTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var locationSchedules = await LoadLocationSchedules(command, cancellationToken);
        if (locationSchedules.Count == 0)
        {
            return [];
        }

        var schedules = await LoadCreatureSchedules(locationSchedules, cancellationToken);
        var occurrences = await ResolveOccurrences(command, schedules, cancellationToken);
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var existing = await ReconcileStaleTravelers(
            schedules,
            occurrences,
            command.GameTime,
            cancellationToken
        );
        var locationScheduleIds = locationSchedules.Select(schedule => schedule.Id).ToHashSet();
        var relevantOccurrences = occurrences
            .Values.Where(occurrence =>
                locationScheduleIds.Contains(occurrence.Schedule.Id)
                && IsAtLocation(occurrence.Position, command.LocationId)
            )
            .ToArray();
        var materializedCreatureIds = await MaterializeOccurrences(
            command.WorldId,
            relevantOccurrences,
            existing,
            cancellationToken
        );
        await context.SaveChangesAsync(cancellationToken);
        transaction.Complete();
        return materializedCreatureIds;
    }

    private async Task<IReadOnlyList<ExistingScheduledTraveler>> ReconcileStaleTravelers(
        IReadOnlyCollection<CreatureRouteSchedule> candidates,
        IReadOnlyDictionary<Guid, ScheduledOccurrence> occurrences,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        var existing = await LoadExisting(
            candidates.Select(schedule => schedule.Id).ToArray(),
            cancellationToken
        );
        var staleCreatureIds = existing
            .Where(entry => IsStale(entry.Traveler, occurrences))
            .Select(entry => entry.CreatureId)
            .ToArray();
        await CreatureRouteCleaner.Remove(context, staleCreatureIds, cancellationToken);
        await RelocateCreaturesWithStaleSchedules(staleCreatureIds, gameTime, cancellationToken);
        return existing;
    }

    private async Task<IReadOnlyCollection<Guid>> MaterializeOccurrences(
        Guid worldId,
        IReadOnlyCollection<ScheduledOccurrence> occurrences,
        IReadOnlyCollection<ExistingScheduledTraveler> existing,
        CancellationToken cancellationToken
    )
    {
        var relevantCreatureIds = occurrences
            .Select(occurrence => occurrence.Schedule.CreatureId)
            .Distinct()
            .ToArray();
        var occupiedCreatureIds = await context
            .RouteTravelerMembers.AsNoTracking()
            .Where(member => relevantCreatureIds.AsEnumerable().Contains(member.CreatureId))
            .Select(member => member.CreatureId)
            .ToArrayAsync(cancellationToken);
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = relevantCreatureIds },
            cancellationToken
        );

        var materializedCreatureIds = new List<Guid>();
        foreach (var occurrence in occurrences)
        {
            var creatureId = occurrence.Schedule.CreatureId;
            if (Matches(existing, occurrence))
            {
                materializedCreatureIds.Add(creatureId);
                continue;
            }
            if (
                occupiedCreatureIds.Contains(creatureId)
                || !creatures.TryGetValue(creatureId, out var creature)
                || !CanFollowSchedule(creature)
            )
            {
                continue;
            }

            AddTraveler(worldId, occurrence, creature);
            materializedCreatureIds.Add(creatureId);
        }
        return materializedCreatureIds.Distinct().ToArray();
    }

    private void AddTraveler(Guid worldId, ScheduledOccurrence occurrence, Creature creature)
    {
        var traveler = new RouteTraveler
        {
            WorldId = worldId,
            RouteId = occurrence.Schedule.RouteId,
            StartedAtGameTime = occurrence.StartedAtGameTime,
            SpeedUnitsPerHour = creature.MovementSpeed,
            Purpose = occurrence.Schedule.Purpose,
            CreatureRouteScheduleId = occurrence.Schedule.Id,
        };
        context.RouteTravelers.Add(traveler);
        context.RouteTravelerMembers.Add(
            new RouteTravelerMember
            {
                WorldId = worldId,
                RouteTravelerId = traveler.Id,
                CreatureId = creature.Id,
            }
        );
    }

    private static bool IsStale(
        RouteTraveler traveler,
        IReadOnlyDictionary<Guid, ScheduledOccurrence> occurrences
    ) =>
        !occurrences.TryGetValue(traveler.CreatureRouteScheduleId!.Value, out var occurrence)
        || traveler.StartedAtGameTime != occurrence.StartedAtGameTime;

    private static bool Matches(
        IReadOnlyCollection<ExistingScheduledTraveler> existing,
        ScheduledOccurrence occurrence
    ) =>
        existing.Any(entry =>
            entry.CreatureId == occurrence.Schedule.CreatureId
            && entry.Traveler.CreatureRouteScheduleId == occurrence.Schedule.Id
            && entry.Traveler.StartedAtGameTime == occurrence.StartedAtGameTime
        );

    private async Task RelocateCreaturesWithStaleSchedules(
        IReadOnlyCollection<Guid> creatureIds,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        if (creatureIds.Count == 0)
        {
            return;
        }

        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = creatureIds },
            cancellationToken
        );
        var jobsByCreatureId = await getCreatureJobsByCreatureIds.Handle(
            new GetCreatureJobsByCreatureIdsQuery { CreatureIds = creatureIds },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(gameTime);
        var relocations = creatures
            .Values.Where(CanFollowSchedule)
            .Select(creature =>
            {
                var job = jobsByCreatureId.TryGetValue(creature.Id, out var jobs)
                    ? CreatureJobScheduling.FindDueJob(jobs, currentDate.Weekday, currentDate.Hour)
                    : null;
                return new ScheduledRelocation(creature.Id, job?.LocationId);
            })
            .Where(relocation => relocation.LocationId != null)
            .GroupBy(relocation => relocation.LocationId!.Value);
        foreach (var relocation in relocations)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = relocation.Select(entry => entry.CreatureId).ToArray(),
                    LocationId = relocation.Key,
                    State = CreatureState.Idle,
                },
                cancellationToken
            );
        }
    }

    private async Task<IReadOnlyList<CreatureRouteSchedule>> LoadLocationSchedules(
        MaterializeScheduledRouteTravelersCommand command,
        CancellationToken cancellationToken
    ) =>
        await context
            .CreatureRouteSchedules.AsNoTracking()
            .Where(schedule =>
                schedule.WorldId == command.WorldId
                && context.RouteSteps.Any(step =>
                    step.WorldId == command.WorldId
                    && step.RouteId == schedule.RouteId
                    && step.LocationId == command.LocationId
                )
            )
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyList<CreatureRouteSchedule>> LoadCreatureSchedules(
        IReadOnlyCollection<CreatureRouteSchedule> locationSchedules,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = locationSchedules
            .Select(schedule => schedule.CreatureId)
            .Distinct()
            .ToArray();
        return await context
            .CreatureRouteSchedules.AsNoTracking()
            .Where(schedule => creatureIds.AsEnumerable().Contains(schedule.CreatureId))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, ScheduledOccurrence>> ResolveOccurrences(
        MaterializeScheduledRouteTravelersCommand command,
        IReadOnlyCollection<CreatureRouteSchedule> schedules,
        CancellationToken cancellationToken
    )
    {
        var currentDateTime = GameClock.GetCurrentInGameDateTime(command.GameTime);
        var active = schedules
            .Select(schedule => ResolveOccurrence(schedule, currentDateTime, command.GameTime))
            .Where(occurrence => occurrence != null)
            .Select(occurrence => occurrence!)
            .ToArray();
        var routeIds = active
            .Select(occurrence => occurrence.Schedule.RouteId)
            .Distinct()
            .ToArray();
        var steps = await context
            .RouteSteps.AsNoTracking()
            .Where(step => routeIds.AsEnumerable().Contains(step.RouteId))
            .OrderBy(step => step.SequenceIndex)
            .ToArrayAsync(cancellationToken);
        var connectorIds = steps
            .Where(step => step.ConnectorId != null)
            .Select(step => step.ConnectorId!.Value)
            .Distinct()
            .ToArray();
        var distances = await context
            .TravelConnectors.AsNoTracking()
            .Where(connector => connectorIds.AsEnumerable().Contains(connector.ConnectorId))
            .ToDictionaryAsync(
                connector => connector.ConnectorId,
                connector => connector.Distance,
                cancellationToken
            );
        var stepsByRouteId = steps
            .GroupBy(step => step.RouteId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<RouteTimelineStep>)
                        group
                            .Select(step => new RouteTimelineStep(
                                step.LocationId,
                                step.ConnectorId,
                                step.ConnectorId == null ? 0 : distances[step.ConnectorId.Value],
                                step.DwellHours
                            ))
                            .ToArray()
            );

        return active.ToDictionary(
            occurrence => occurrence.Schedule.Id,
            occurrence =>
                occurrence with
                {
                    Position = RouteTimeline.Resolve(
                        stepsByRouteId[occurrence.Schedule.RouteId],
                        RouteTraversal.Finite,
                        occurrence.Schedule.DurationHours == 0
                            ? 1
                            : stepsByRouteId[occurrence.Schedule.RouteId].Sum(step => step.Distance)
                                / occurrence.Schedule.DurationHours,
                        occurrence.StartedAtGameTime,
                        command.GameTime
                    ),
                }
        );
    }

    private async Task<IReadOnlyList<ExistingScheduledTraveler>> LoadExisting(
        IReadOnlyCollection<Guid> scheduleIds,
        CancellationToken cancellationToken
    ) =>
        await (
            from member in context.RouteTravelerMembers.AsNoTracking()
            join traveler in context.RouteTravelers.AsNoTracking()
                on member.RouteTravelerId equals traveler.Id
            where
                traveler.CreatureRouteScheduleId != null
                && scheduleIds.AsEnumerable().Contains(traveler.CreatureRouteScheduleId.Value)
            select new ExistingScheduledTraveler(member.CreatureId, traveler)
        ).ToArrayAsync(cancellationToken);

    private static ScheduledOccurrence? ResolveOccurrence(
        CreatureRouteSchedule schedule,
        DateTime currentDateTime,
        GameInstant gameTime
    )
    {
        var daysSinceDeparture =
            ((int)currentDateTime.DayOfWeek - (int)schedule.DepartureDay + 7) % 7;
        var startedAt = currentDateTime
            .Date.AddDays(-daysSinceDeparture)
            .AddHours(schedule.DepartureHour);
        if (startedAt > currentDateTime)
        {
            startedAt = startedAt.AddDays(-7);
        }
        if (startedAt.AddHours(schedule.DurationHours) <= currentDateTime)
        {
            startedAt = startedAt.AddDays(7);
            var nextHour = currentDateTime.Date.AddHours(currentDateTime.Hour + 1);
            if (startedAt >= nextHour)
            {
                return null;
            }
        }

        var startedAtGameTime =
            gameTime + TimeSpan.FromHours(1) * (startedAt - currentDateTime).TotalHours;
        return new ScheduledOccurrence(schedule, startedAtGameTime, Position: null!);
    }

    private static bool IsAtLocation(RouteTimelinePosition position, Guid locationId) =>
        position switch
        {
            RouteTimelinePosition.Pending pending => pending.LocationId == locationId,
            RouteTimelinePosition.Lingering lingering => lingering.LocationId == locationId,
            RouteTimelinePosition.InTransit inTransit => inTransit.FromLocationId == locationId,
            RouteTimelinePosition.Arrived arrived => arrived.LocationId == locationId,
            _ => false,
        };

    private static bool CanFollowSchedule(Creature creature) =>
        creature.State
            is not CreatureState.Alerted
                and not CreatureState.Dead
                and not CreatureState.Restrained;

    private record ScheduledOccurrence(
        CreatureRouteSchedule Schedule,
        GameInstant StartedAtGameTime,
        RouteTimelinePosition Position
    );

    private record ExistingScheduledTraveler(Guid CreatureId, RouteTraveler Traveler);

    private record ScheduledRelocation(Guid CreatureId, Guid? LocationId);
}
