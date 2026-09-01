using TRPG.Application.Common.Events;

namespace TRPG.Application.Crimes.Events;

public sealed record CrimeWitnessedEvent(CrimeKind CrimeKind) : GameClientEvent;
