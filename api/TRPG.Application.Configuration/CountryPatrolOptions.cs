namespace TRPG.Application.Configuration;

public class CountryPatrolOptions
{
    // Guards walk their beat rather than ride an express route, so this stays at a pace far
    // below CaravanOptions.SpeedUnitsPerHour — measured against real generated worlds, this
    // keeps a full country loop in the ~1-3 in-game day range.
    public float SpeedUnitsPerHour { get; init; } = 60;

    // Short enough that the patrol reads as passing through rather than parked.
    public double DefaultLingerHours { get; init; } = 1;

    public int SquadSize { get; init; } = 3;
    public int MinGuardLevel { get; init; } = 25;
    public int MaxGuardLevel { get; init; } = 50;
}
