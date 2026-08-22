using TRPG.Application.Common.Events;

namespace TRPG.Application.Reputations.Events;

public enum CrimeKind
{
    Theft,
    Killing,
}

public sealed record CrimeWitnessedEvent(CrimeKind CrimeKind) : GameClientEvent;

public sealed record CrimeWitnessesRemovedEvent(CrimeKind CrimeKind) : GameClientEvent;
