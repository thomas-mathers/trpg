using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Mappers;

public static class TheftEncounterMapper
{
    // The narrator is told who was robbed so it never has to invent an owner for the goods.
    public static string ToStolenFrom(this TheftEncounter encounter) =>
        encounter.OwnerCreatureId == encounter.ConfrontingCreatureId ? "you" : encounter.OwnerName;
}
