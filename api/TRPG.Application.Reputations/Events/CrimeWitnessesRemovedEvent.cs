using TRPG.Application.Common.Events;

namespace TRPG.Application.Reputations.Events;

public sealed record CrimeWitnessesRemovedEvent(CrimeKind CrimeKind) : GameClientEvent;
