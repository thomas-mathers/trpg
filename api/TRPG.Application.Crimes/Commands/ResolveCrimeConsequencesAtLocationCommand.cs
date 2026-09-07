using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class ResolveCrimeConsequencesAtLocationCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class ResolveCrimeConsequencesAtLocationCommandHandler(
    IEnumerable<ICrimeConsequenceResolver> resolvers,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<ApplyCrimeReputationPenaltyCommand> applyCrimeReputationPenalty,
    ILogger<ResolveCrimeConsequencesAtLocationCommandHandler> logger
) : ICommandHandler<ResolveCrimeConsequencesAtLocationCommand>
{
    public async Task Handle(
        ResolveCrimeConsequencesAtLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var scope = new CrimeScope(command.WorldId, command.PlayerId, command.LocationId);

        foreach (var resolver in resolvers)
        {
            await ResolveConsequences(resolver, scope, cancellationToken);
        }
    }

    private async Task ResolveConsequences(
        ICrimeConsequenceResolver resolver,
        CrimeScope scope,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await resolver.GetWitnessCandidates(scope, cancellationToken);

        var reports = await resolver.Resolve(
            scope,
            await ResolveLiveCreatureIds(candidateIds, cancellationToken),
            cancellationToken
        );

        if (reports.Count == 0)
        {
            logger.LogDebug(
                "[crime] {Resolver}: nothing reported at location {LocationId}",
                resolver.GetType().Name,
                scope.LocationId
            );
            return;
        }

        logger.LogInformation(
            "[crime] {Resolver}: {Count} reported at location {LocationId}",
            resolver.GetType().Name,
            reports.Count,
            scope.LocationId
        );

        await applyCrimeReputationPenalty.Handle(
            new ApplyCrimeReputationPenaltyCommand
            {
                PlayerId = scope.PlayerId,
                WorldId = scope.WorldId,
                Reports = reports,
                FactionReason = resolver.FactionReason,
                WitnessReason = resolver.WitnessReason,
                VictimReason = resolver.VictimReason,
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
