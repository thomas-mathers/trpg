using TRPG.Application.Encounters;
using TRPG.Encounters.Requests;

namespace TRPG.Encounters.Mappers;

internal static class EncounterActionRequestMapper
{
    public static PlayerEncounterAction ToAction(this EncounterActionRequest request) =>
        request switch
        {
            AttackEncounterActionRequest => new AttackEncounterAction(),
            EvadeEncounterActionRequest => new EvadeEncounterAction(),
            RetreatEncounterActionRequest => new RetreatEncounterAction(),
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };
}
