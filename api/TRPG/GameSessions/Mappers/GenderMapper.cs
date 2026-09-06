using ContractGender = TRPG.GameSessions.Responses.Gender;
using DataGender = TRPG.Domain.Models.Gender;

namespace TRPG.GameSessions.Mappers;

internal static class GenderMapper
{
    public static ContractGender ToResponse(this DataGender gender) =>
        gender switch
        {
            DataGender.Male => ContractGender.Male,
            DataGender.Female => ContractGender.Female,
        };
}
