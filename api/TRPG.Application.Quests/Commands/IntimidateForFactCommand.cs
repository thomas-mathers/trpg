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

// An overwhelmingly weaker player can't intimidate anyone, no matter the score — checked before
// resolving so a hopeless attempt returns TooWeak rather than a plain Failed, and never locks out
// the approach (nothing about the NPC's disposition changed, only the player's level might).
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

        if (
            !FactDisclosureScoreCalculator.CanAttemptIntimidation(
                player!.Level,
                npc!.Level,
                options
            )
        )
        {
            return new FactDisclosureResult(FactDisclosureOutcome.TooWeak);
        }

        var levelAdvantageAboveFloor =
            player.Level - npc.Level - options.MinimumLevelAdvantageToIntimidate;

        return await resolver.Resolve(
            command.WorldId,
            command.PlayerId,
            command.NpcId,
            command.FactId,
            approach: FactDisclosureApproach.Intimidation,
            computeApproachContribution: objective =>
                Math.Min(
                    levelAdvantageAboveFloor * options.IntimidationScorePerLevelAdvantage,
                    objective.IntimidationWillingness
                ),
            options,
            cancellationToken
        );
    }
}
