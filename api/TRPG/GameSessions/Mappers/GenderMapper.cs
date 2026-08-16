using ContractGender = TRPG.Contracts.Scenes.Responses.Gender;
using DataGender = TRPG.Data.Models.Gender;

namespace TRPG.GameSessions.Mappers;

internal static class GenderMapper
{
    public static ContractGender ToContract(this DataGender gender) =>
        gender switch
        {
            DataGender.Male => ContractGender.Male,
            DataGender.Female => ContractGender.Female,
            _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
        };
}
