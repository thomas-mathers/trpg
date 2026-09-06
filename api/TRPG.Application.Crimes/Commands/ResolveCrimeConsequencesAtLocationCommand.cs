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
    IQueryHandler<
        GetAssaultWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getAssaultWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveAssaultCrimeWitnessesCommand,
        ResolveAssaultCrimeWitnessesResult
    > resolveAssaultCrimeWitnesses,
    IQueryHandler<
        GetTheftWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getTheftWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveTheftCrimeWitnessesCommand,
        ResolveTheftCrimeWitnessesResult
    > resolveTheftCrimeWitnesses,
    IQueryHandler<
        GetLockpickingWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getLockpickingWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveLockpickingCrimeWitnessesCommand,
        ResolveLockpickingCrimeWitnessesResult
    > resolveLockpickingCrimeWitnesses,
    IQueryHandler<
        GetTrespassingWitnessCandidateCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getTrespassingWitnessCandidateCreatureIds,
    ICommandHandler<
        ResolveTrespassingCrimeWitnessesCommand,
        ResolveTrespassingCrimeWitnessesResult
    > resolveTrespassingCrimeWitnesses,
    ICommandHandler<ApplyCrimeReputationPenaltyCommand> applyCrimeReputationPenalty
) : ICommandHandler<ResolveCrimeConsequencesAtLocationCommand>
{
    public async Task Handle(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await ResolveKills(command, cancellationToken);
        await ResolveAssaults(command, cancellationToken);
        await ResolveThefts(command, cancellationToken);
        await ResolveLockpickings(command, cancellationToken);
        await ResolveTrespassings(command, cancellationToken);
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

        await ApplyPenalty(
            command,
            resolution.ReportedCrimes,
            ReputationReason.KilledFactionMember,
            ReputationReason.WitnessedKilling,
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

        await ApplyPenalty(
            command,
            resolution.ReportedCrimes,
            ReputationReason.AssaultedFactionMember,
            ReputationReason.WitnessedAssault,
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

        await ApplyPenalty(
            command,
            resolution.ReportedCrimes,
            ReputationReason.StoleFromFactionMember,
            ReputationReason.WitnessedTheft,
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

        await ApplyPenalty(
            command,
            resolution.ReportedCrimes,
            ReputationReason.PickedFactionLock,
            ReputationReason.WitnessedLockpicking,
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

        await ApplyPenalty(
            command,
            resolution.ReportedCrimes,
            ReputationReason.TrespassedOnFactionProperty,
            ReputationReason.WitnessedTrespassing,
            cancellationToken
        );
    }

    private async Task ApplyPenalty(
        ResolveCrimeConsequencesAtLocationCommand command,
        IReadOnlyCollection<CrimeReport> reports,
        ReputationReason factionReason,
        ReputationReason witnessReason,
        CancellationToken cancellationToken
    )
    {
        if (reports.Count == 0)
        {
            return;
        }

        await applyCrimeReputationPenalty.Handle(
            new ApplyCrimeReputationPenaltyCommand
            {
                PlayerId = command.PlayerId,
                WorldId = command.WorldId,
                Reports = reports,
                FactionReason = factionReason,
                WitnessReason = witnessReason,
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
