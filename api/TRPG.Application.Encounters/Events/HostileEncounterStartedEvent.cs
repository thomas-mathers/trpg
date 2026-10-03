using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Events;

public record HostileEncounterStartedEvent(Guid WorldId, HostileEncounter Encounter)
    : GameClientEvent(WorldId);
