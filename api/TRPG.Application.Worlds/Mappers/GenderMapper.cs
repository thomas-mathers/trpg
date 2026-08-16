using DataGender = TRPG.Domain.Models.Gender;

namespace TRPG.Application.Worlds.Mappers;

internal static class GenderMapper
{
    public static DataGender ToGender(this PlayerGender gender) =>
        gender switch
        {
            PlayerGender.Male => DataGender.Male,
            PlayerGender.Female => DataGender.Female,
            _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
        };
}
