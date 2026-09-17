using System.Transactions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
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

// A player can't offer more gold than they actually have (checked up front, before any scoring),
// and a successful bribe actually spends it — a failed one doesn't, since the NPC never took it.
internal class OfferBribeForFactCommandHandler(
    FactDisclosureResolver resolver,
    IQueryHandler<GetGoldQuantityQuery, int> getGoldQuantity,
    ICommandHandler<RemoveGoldCommand> removeGold,
    IOptionsMonitor<FactDisclosureOptions> factDisclosureOptions
) : ICommandHandler<OfferBribeForFactCommand, FactDisclosureResult>
{
    public async Task<FactDisclosureResult> Handle(
        OfferBribeForFactCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var options = factDisclosureOptions.CurrentValue;
        var owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature);

        var currentGold = await getGoldQuantity.Handle(
            new GetGoldQuantityQuery { Owner = owner },
            cancellationToken
        );
        if (command.GoldOffered > currentGold)
        {
            return new FactDisclosureResult(FactDisclosureOutcome.CannotAfford);
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var result = await resolver.Resolve(
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

        if (result.Outcome == FactDisclosureOutcome.Disclosed && command.GoldOffered > 0)
        {
            await removeGold.Handle(
                new RemoveGoldCommand { Owner = owner, Amount = command.GoldOffered },
                cancellationToken
            );
        }

        transaction.Complete();
        return result;
    }
}
