using TRPG.Application.Combat.Queries;
using TRPG.Combat.Responses;

namespace TRPG.Combat.Mappers;

internal static class AbilityAvailabilityMapper
{
    public static AbilityAvailabilityResponse ToResponse(this AbilityAvailability availability) =>
        new(availability.Name, availability.IsUsable, availability.Reason);
}
