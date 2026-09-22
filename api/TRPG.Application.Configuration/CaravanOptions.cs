namespace TRPG.Application.Configuration;

public class CaravanOptions
{
    // 3x CreatureGeneratorOptions.WalkingSpeedUnitsPerHour — a scheduled coach with relay stops
    // reads as faster and more comfortable than walking, without implying galloping-horse speed.
    public float SpeedUnitsPerHour { get; init; } =
        CreatureGeneratorOptions.WalkingSpeedUnitsPerHour * 3;

    // Boarding is ticket-anchored (see BoardCaravanCommand), immune to ordinary narration-time
    // drift once purchased, so this no longer needs slack for interaction overhead — 20 minutes.
    public double DefaultLingerHours { get; init; } = 20.0 / 60.0;

    // Evenly phase-offset instances spawned per direction, per stop — e.g. 1 gives one clockwise
    // and one counter-clockwise instance anchored to each capital (2 caravans per capital).
    public int CaravansPerStop { get; init; } = 1;
    public int DefaultTicketFeeGold { get; init; } = 10;
}
