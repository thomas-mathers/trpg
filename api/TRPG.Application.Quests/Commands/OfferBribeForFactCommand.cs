using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Quests.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Commands;

public class OfferBribeForFactCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid NpcId { get; init; }
    public required Guid FactId { get; init; }
    public required int GoldOffered { get; init; }
}

// GoldOffered is a stated number used only to score the attempt in this isolated pass — no gold
// actually changes hands yet. On failure the Bribe approach locks out until a supporting quest
// completes, so raising the offer and asking again does nothing.
internal class OfferBribeForFactCommandHandler(
    FactDisclosureResolver resolver,
    IOptionsMonitor<FactDisclosureOptions> factDisclosureOptions
) : ICommandHandler<OfferBribeForFactCommand, FactDisclosureResult>
{
    public Task<FactDisclosureResult> Handle(
        OfferBribeForFactCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var options = factDisclosureOptions.CurrentValue;
        return resolver.Resolve(
            command.WorldId,
            command.PlayerId,
            command.NpcId,
            command.FactId,
            approach: FactDisclosureApproach.Bribe,
            computeApproachContribution: objective =>
                Math.Min(
                    command.GoldOffered / options.GoldPerBribeScorePoint,
                    objective.BribeWillingness
                ),
            options,
            cancellationToken
        );
    }
}
