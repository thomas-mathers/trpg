using TRPG.Application.Inventory;
using TRPG.Application.Trading;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Trading;

public sealed class TradeOfferEvaluatorTests
{
    private readonly TradeOfferEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ReturnsAccepted_WhenPlayerOfferMeetsShopValue()
    {
        // Arrange
        var offer = new ValidatedTradeOffer(
            new ItemOwnerReference(Guid.NewGuid(), OwnerType.Workstation),
            AssignedCreatureId: null,
            PlayerOfferValue: 50,
            ShopOfferValue: 40
        );

        // Act
        var result = _evaluator.Evaluate(offer);

        // Assert
        Assert.Equal(TradeOutcome.Accepted, result);
    }

    [Fact]
    public void Evaluate_ReturnsRejected_WhenPlayerOfferIsWorthLessThanShopOffer()
    {
        // Arrange
        var offer = new ValidatedTradeOffer(
            new ItemOwnerReference(Guid.NewGuid(), OwnerType.Workstation),
            AssignedCreatureId: null,
            PlayerOfferValue: 40,
            ShopOfferValue: 50
        );

        // Act
        var result = _evaluator.Evaluate(offer);

        // Assert
        Assert.Equal(TradeOutcome.Rejected, result);
    }
}
