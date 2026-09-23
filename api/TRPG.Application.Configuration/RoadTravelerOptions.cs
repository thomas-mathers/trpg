namespace TRPG.Application.Configuration;

public class RoadTravelerOptions
{
    public float SpeedUnitsPerHour { get; init; } =
        CreatureGeneratorOptions.WalkingSpeedUnitsPerHour;

    public double LingerHours { get; init; } = 1;
    public int PilgrimsPerCountry { get; init; } = 1;
    public int AdventurersPerCountry { get; init; } = 2;
    public int MinimumPilgrimLevel { get; init; } = 5;
    public int MaximumPilgrimLevel { get; init; } = 20;
    public int MinimumAdventurerLevel { get; init; } = 10;
    public int MaximumAdventurerLevel { get; init; } = 35;
}
