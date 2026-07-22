using TRPG.Contracts.Combat.Responses;

namespace TRPG.Contracts.Creatures.Requests;

public record AllocateAttributePointsRequest(IReadOnlyDictionary<AttributeName, int> Deltas);
