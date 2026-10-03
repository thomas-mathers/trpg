using TRPG.Application.Common.Events;

namespace TRPG.Application.Crimes.Events;

public sealed record CrimeWitnessesRemovedEvent(Guid WorldId, CrimeKind CrimeKind)
    : GameClientEvent(WorldId);
