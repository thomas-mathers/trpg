using Tapper;

namespace TRPG.GameSessions.Responses;

[TranspilationSource]
public record PlayerMovementInput(
    Guid LocationId,
    double Forward,
    double Strafe,
    double Heading,
    double X,
    double Y
);
