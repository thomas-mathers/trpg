using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Events;

public record TheftEncounterStartedEvent(TheftEncounter Encounter) : GameClientEvent;
