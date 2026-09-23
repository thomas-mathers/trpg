using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Events;

public record ShakedownEncounterStartedEvent(ShakedownEncounter Encounter, bool CanAffordToll)
    : GameClientEvent;
