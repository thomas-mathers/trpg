using TRPG.Application.Inventory;
using TRPG.Inventory.Responses;

namespace TRPG.Inventory.Mappers;

internal static class TradeOutcomeMapper
{
    public static TradeProposalStatus ToStatus(this TradeOutcome outcome) =>
        outcome switch
        {
            TradeOutcome.Accepted => TradeProposalStatus.Accepted,
            TradeOutcome.Rejected => TradeProposalStatus.Rejected,
            TradeOutcome.Refused => TradeProposalStatus.Refused,
        };
}
