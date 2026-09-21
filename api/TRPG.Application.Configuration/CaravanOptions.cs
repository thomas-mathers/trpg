namespace TRPG.Application.Configuration;

public class CaravanOptions
{
    // A capital-to-capital loop routes through several intermediate states on the state-hub tree,
    // not one hop, so this needs to read as a dedicated express route rather than a walking pace —
    // measured against real generated worlds, this keeps a full loop in the ~1-2 in-game week
    // range instead of ~3-5 weeks.
    public float SpeedUnitsPerHour { get; init; } = 300;

    // Boarding is ticket-anchored (see BoardCaravanCommand), immune to ordinary narration-time
    // drift once purchased, so this no longer needs slack for interaction overhead — 20 minutes.
    public double DefaultLingerHours { get; init; } = 20.0 / 60.0;

    // Evenly phase-offset instances spawned per direction, per stop — e.g. 1 gives one clockwise
    // and one counter-clockwise instance anchored to each capital (2 caravans per capital).
    public int CaravansPerStop { get; init; } = 1;
    public int DefaultTicketFeeGold { get; init; } = 10;
}
