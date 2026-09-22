namespace TRPG.Application.Configuration;

public class CountryPatrolOptions
{
    // Guards walk their beat at an ordinary walking pace rather than riding an express route
    // like CaravanOptions.SpeedUnitsPerHour.
    public float SpeedUnitsPerHour { get; init; } =
        CreatureGeneratorOptions.WalkingSpeedUnitsPerHour;

    // No rest stop: a patrol is always mid-leg (Creature.State.Patrolling), never genuinely
    // stationary, so there's nothing for a linger window to represent.
    public double DefaultLingerHours { get; init; } = 0;

    public int SquadSize { get; init; } = 3;
    public int MinGuardLevel { get; init; } = 25;
    public int MaxGuardLevel { get; init; } = 50;
}
