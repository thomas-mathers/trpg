using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class ResolveCrimeConsequencesAtLocationCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class ResolveCrimeConsequencesAtLocationCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<
        GetKillWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getKillWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveKillCrimeWitnessesCommand,
        ResolveKillCrimeWitnessesResult
    > resolveKillCrimeWitnesses,
    ICommandHandler<ApplyReputationPenaltyForKillsCommand> applyReputationPenaltyForKills,
    IQueryHandler<
        GetTheftWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getTheftWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveTheftCrimeWitnessesCommand,
        ResolveTheftCrimeWitnessesResult
    > resolveTheftCrimeWitnesses,
    ICommandHandler<ApplyReputationPenaltyForTheftsCommand> applyReputationPenaltyForThefts,
    IQueryHandler<
        GetLockpickingWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getLockpickingWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveLockpickingCrimeWitnessesCommand,
        ResolveLockpickingCrimeWitnessesResult
    > resolveLockpickingCrimeWitnesses,
    ICommandHandler<ApplyReputationPenaltyForLockpickingCommand> applyReputationPenaltyForLockpicking,
    IQueryHandler<
        GetTrespassingWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getTrespassingWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveTrespassingCrimeWitnessesCommand,
        ResolveTrespassingCrimeWitnessesResult
    > resolveTrespassingCrimeWitnesses,
    ICommandHandler<ApplyReputationPenaltyForTrespassingCommand> applyReputationPenaltyForTrespassing,
    IQueryHandler<
        GetAssaultWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getAssaultWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveAssaultCrimeWitnessesCommand,
        ResolveAssaultCrimeWitnessesResult
    > resolveAssaultCrimeWitnesses,
    ICommandHandler<ApplyReputationPenaltyForAssaultCommand> applyReputationPenaltyForAssault
) : ICommandHandler<ResolveCrimeConsequencesAtLocationCommand>
{
    public async Task Handle(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await ResolveKills(command, cancellationToken);
        await ResolveThefts(command, cancellationToken);
        await ResolveLockpickings(command, cancellationToken);
        await ResolveTrespassings(command, cancellationToken);
        await ResolveAssaults(command, cancellationToken);
    }

    private async Task ResolveKills(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await getKillWitnessCandidateCreatureIds.Handle(
            new GetKillWitnessCandidateCreatureIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var resolution = await resolveKillCrimeWitnesses.Handle(
            new ResolveKillCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                LiveWitnessCreatureIds = await ResolveLiveCreatureIds(
                    candidateIds,
                    cancellationToken
                ),
            },
            cancellationToken
        );

        if (resolution.ReportedCrimes.Count == 0)
        {
            return;
        }

        await applyReputationPenaltyForKills.Handle(
            new ApplyReputationPenaltyForKillsCommand
            {
                KillerId = command.PlayerId,
                WorldId = command.WorldId,
                Kills = resolution.ReportedCrimes,
            },
            cancellationToken
        );
    }

    private async Task ResolveThefts(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await getTheftWitnessCandidateCreatureIds.Handle(
            new GetTheftWitnessCandidateCreatureIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var resolution = await resolveTheftCrimeWitnesses.Handle(
            new ResolveTheftCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                LiveWitnessCreatureIds = await ResolveLiveCreatureIds(
                    candidateIds,
                    cancellationToken
                ),
            },
            cancellationToken
        );

        if (resolution.ReportedCrimes.Count == 0)
        {
            return;
        }

        await applyReputationPenaltyForThefts.Handle(
            new ApplyReputationPenaltyForTheftsCommand
            {
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Thefts = resolution.ReportedCrimes,
            },
            cancellationToken
        );
    }

    private async Task ResolveLockpickings(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await getLockpickingWitnessCandidateCreatureIds.Handle(
            new GetLockpickingWitnessCandidateCreatureIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var resolution = await resolveLockpickingCrimeWitnesses.Handle(
            new ResolveLockpickingCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                LiveWitnessCreatureIds = await ResolveLiveCreatureIds(
                    candidateIds,
                    cancellationToken
                ),
            },
            cancellationToken
        );

        if (resolution.ReportedCrimes.Count == 0)
        {
            return;
        }

        await applyReputationPenaltyForLockpicking.Handle(
            new ApplyReputationPenaltyForLockpickingCommand
            {
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Crimes = resolution.ReportedCrimes,
            },
            cancellationToken
        );
    }

    private async Task ResolveTrespassings(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await getTrespassingWitnessCandidateCreatureIds.Handle(
            new GetTrespassingWitnessCandidateCreatureIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var resolution = await resolveTrespassingCrimeWitnesses.Handle(
            new ResolveTrespassingCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                LiveWitnessCreatureIds = await ResolveLiveCreatureIds(
                    candidateIds,
                    cancellationToken
                ),
            },
            cancellationToken
        );

        if (resolution.ReportedCrimes.Count == 0)
        {
            return;
        }

        await applyReputationPenaltyForTrespassing.Handle(
            new ApplyReputationPenaltyForTrespassingCommand
            {
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Crimes = resolution.ReportedCrimes,
            },
            cancellationToken
        );
    }

    private async Task ResolveAssaults(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await getAssaultWitnessCandidateCreatureIds.Handle(
            new GetAssaultWitnessCandidateCreatureIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var resolution = await resolveAssaultCrimeWitnesses.Handle(
            new ResolveAssaultCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                LiveWitnessCreatureIds = await ResolveLiveCreatureIds(
                    candidateIds,
                    cancellationToken
                ),
            },
            cancellationToken
        );

        if (resolution.ReportedCrimes.Count == 0)
        {
            return;
        }

        await applyReputationPenaltyForAssault.Handle(
            new ApplyReputationPenaltyForAssaultCommand
            {
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Assaults = resolution.ReportedCrimes,
            },
            cancellationToken
        );
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveLiveCreatureIds(
        IReadOnlyCollection<Guid> creatureIds,
        CancellationToken cancellationToken
    )
    {
        if (creatureIds.Count == 0)
        {
            return [];
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = creatureIds },
            cancellationToken
        );
        return creaturesById
            .Where(creature => creature.Value.State != CreatureState.Dead)
            .Select(creature => creature.Key)
            .ToArray();
    }
}
