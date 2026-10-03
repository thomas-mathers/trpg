using TRPG.Application.Common.Events;

namespace TRPG.Application.Crimes.Events;

public sealed record CrimeWitnessedEvent(Guid WorldId, CrimeKind CrimeKind)
    : GameClientEvent(WorldId);
