using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Factions.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class RecordAssaultCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid VictimId { get; init; }
}

internal class RecordAssaultCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetFactionIdsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getFactionIdsByCreatureIds,
    IQueryHandler<
        GetLiveHumanoidWitnessesAtLocationQuery,
        IReadOnlyCollection<LiveHumanoidWitness>
    > getLiveHumanoidWitnessesAtLocation,
    ICommandHandler<AddAssaultCrimesCommand> addAssaultCrimes,
    ICommandHandler<AddCrimeWitnessesCommand> addCrimeWitnesses
) : ICommandHandler<RecordAssaultCommand>
{
    public async Task Handle(
        RecordAssaultCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var victim = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.VictimId },
            cancellationToken
        );
        if (victim == null || !CreatureTypes.Humanoid.Contains(victim.CreatureType))
        {
            return;
        }

        var factionIdsByCreatureId = await getFactionIdsByCreatureIds.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = [victim.Id] },
            cancellationToken
        );
        var victimFactionIds = factionIdsByCreatureId.TryGetValue(victim.Id, out var factionIds)
            ? factionIds
            : [];
        if (victimFactionIds.Count == 0)
        {
            return;
        }

        // Recorded at the first strike so the victim still counts among the witnesses.
        var witnesses = await getLiveHumanoidWitnessesAtLocation.Handle(
            new GetLiveHumanoidWitnessesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = victim.LocationId,
                ExcludeCreatureId = command.PlayerId,
            },
            cancellationToken
        );
        if (witnesses.Count == 0)
        {
            return;
        }

        var crime = new AssaultCrime
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = victim.LocationId,
            VictimId = victim.Id,
            VictimName = victim.Name,
            VictimFactionIds = victimFactionIds.ToList(),
        };

        await addAssaultCrimes.Handle(
            new AddAssaultCrimesCommand { Crimes = [crime] },
            cancellationToken
        );

        await addCrimeWitnesses.Handle(
            new AddCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                CrimeIds = [crime.Id],
                WitnessCreatureIds = witnesses.Select(witness => witness.Id).ToArray(),
            },
            cancellationToken
        );
    }
}
