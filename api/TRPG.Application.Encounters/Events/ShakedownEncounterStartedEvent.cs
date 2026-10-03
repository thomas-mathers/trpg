using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Events;

public record ShakedownEncounterStartedEvent(
    Guid WorldId,
    ShakedownEncounter Encounter,
    bool CanAffordToll
) : GameClientEvent(WorldId);
