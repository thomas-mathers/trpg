using Tapper;

namespace TRPG.GameSessions.Responses;

[TranspilationSource]
public record PlacementWire(double X, double Y, double Angle);

[TranspilationSource]
public record FootprintWire(double Width, double Depth);

[TranspilationSource]
public record BoxLayoutWire(Guid Id, PlacementWire Placement, FootprintWire Footprint);

[TranspilationSource]
public record ConnectorLayoutWire(
    Guid ConnectorId,
    Guid DestinationLocationId,
    double ExitX,
    double ExitY
);

[TranspilationSource]
public record CreatureLayoutWire(Guid Id, PlacementWire Placement);

[TranspilationSource]
public record LocationLayoutWire(
    FootprintWire Size,
    IReadOnlyCollection<BoxLayoutWire> Props,
    IReadOnlyCollection<BoxLayoutWire> Buildings,
    IReadOnlyCollection<ConnectorLayoutWire> Connectors,
    IReadOnlyCollection<CreatureLayoutWire> Creatures
);
