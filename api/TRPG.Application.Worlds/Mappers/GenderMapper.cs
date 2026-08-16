using ContractGender = TRPG.Contracts.Worlds.Requests.Gender;
using DataGender = TRPG.Data.Models.Gender;

namespace TRPG.Application.Worlds.Mappers;

internal static class GenderMapper
{
    public static DataGender ToGender(this ContractGender gender) =>
        gender switch
        {
            ContractGender.Male => DataGender.Male,
            ContractGender.Female => DataGender.Female,
            _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
        };
}
