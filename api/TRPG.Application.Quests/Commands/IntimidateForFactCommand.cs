using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class IntimidateForFactCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid NpcId { get; init; }
    public required Guid FactId { get; init; }
}

internal class IntimidateForFactCommandHandler(
    FactDisclosureResolver resolver,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IOptionsMonitor<FactDisclosureOptions> factDisclosureOptions
) : ICommandHandler<IntimidateForFactCommand, FactDisclosureResult>
{
    public async Task<FactDisclosureResult> Handle(
        IntimidateForFactCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var options = factDisclosureOptions.CurrentValue;

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        var npc = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.NpcId },
            cancellationToken
        );

        var levelAdvantageAboveFloor =
            player!.Level - npc!.Level - options.MinimumLevelAdvantageToIntimidate;

        return await resolver.Resolve(
            new FactDisclosureRequest(
                WorldId: command.WorldId,
                PlayerId: command.PlayerId,
                NpcId: command.NpcId,
                FactId: command.FactId,
                Approach: FactDisclosureApproach.Intimidation
            ),
            objective =>
                FactDisclosureScoreCalculator.CanAttemptIntimidation(
                    player!.Level,
                    npc!.Level,
                    options
                )
                    ? new FactDisclosureAssessment(
                        Math.Min(
                            levelAdvantageAboveFloor * options.IntimidationScorePerLevelAdvantage,
                            objective.IntimidationWillingness
                        )
                    )
                    : new FactDisclosureAssessment(0, FactDisclosureOutcome.TooWeak),
            options,
            cancellationToken
        );
    }
}
