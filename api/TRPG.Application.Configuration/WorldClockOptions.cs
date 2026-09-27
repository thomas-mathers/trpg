namespace TRPG.Application.Configuration;

public class WorldClockOptions
{
    // Game time passes this many times faster than real time while a world is active; read once at startup.
    public double TimeScale { get; init; } = 1;
}
