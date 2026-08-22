using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Reputations;
using TRPG.Application.Reputations.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Trading.Commands;

public class ProposeTradeCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorkstationId { get; init; }
    public required IReadOnlyList<ItemSelection> PlayerOffer { get; init; }
    public required IReadOnlyList<ItemSelection> ShopOffer { get; init; }
}

internal class ProposeTradeCommandHandler(
    TradeOfferValidator validator,
    TradeOfferEvaluator evaluator,
    IQueryHandler<GetEffectiveReputationQuery, int> getEffectiveReputation
) : ICommandHandler<ProposeTradeCommand, TradeOutcome>
{
    public async Task<TradeOutcome> Handle(
        ProposeTradeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var validation = await validator.Validate(
            command.PlayerId,
            command.WorkstationId,
            command.PlayerOffer,
            command.ShopOffer,
            cancellationToken
        );

        if (validation.AssignedCreatureId is { } shopkeeperId)
        {
            var reputation = await getEffectiveReputation.Handle(
                new GetEffectiveReputationQuery
                {
                    ObserverCreatureId = command.PlayerId,
                    TargetCreatureId = shopkeeperId,
                },
                cancellationToken
            );

            if (ReputationAttitudeCalculator.FromScore(reputation) == ReputationAttitude.Hostile)
            {
                return TradeOutcome.Refused;
            }
        }

        return evaluator.Evaluate(validation);
    }
}
