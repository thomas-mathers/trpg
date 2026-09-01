using TRPG.Application.Common.Events;

namespace TRPG.Application.Crimes.Events;

public sealed record CrimeWitnessesRemovedEvent(CrimeKind CrimeKind) : GameClientEvent;
