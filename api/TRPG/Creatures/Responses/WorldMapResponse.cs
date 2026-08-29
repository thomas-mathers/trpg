namespace TRPG.Creatures.Responses;

public record PointResponse(double X, double Y);

public record CountryMapResponse(Guid Id, string Name, IReadOnlyList<PointResponse> Boundary);

public record StateMapResponse(
    Guid Id,
    Guid CountryId,
    string Name,
    string Description,
    PointResponse Center,
    IReadOnlyList<PointResponse> Boundary
);

public record CityMapResponse(Guid Id, Guid StateId, string Name, bool IsCapital);

public record RoadMapResponse(Guid Id, string Name, Guid OriginStateId, Guid DestinationStateId);

public record CorpseMapResponse(Guid Id, string Name, Guid StateId, int ItemCount);

public record QuestMapResponse(Guid QuestId, string ObjectiveName, Guid StateId);

public record WorldMapResponse(
    IReadOnlyList<CountryMapResponse> Countries,
    IReadOnlyList<StateMapResponse> States,
    IReadOnlyList<CityMapResponse> Cities,
    IReadOnlyList<RoadMapResponse> Roads,
    Guid PlayerStateId,
    IReadOnlyList<CorpseMapResponse> Corpses,
    IReadOnlyList<QuestMapResponse> QuestMarkers
);
