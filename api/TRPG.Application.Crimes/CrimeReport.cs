namespace TRPG.Application.Crimes;

// One priced offence: the factions it wronged, who reported it, and what it costs each of them.
public record CrimeReport(
    IReadOnlyCollection<Guid> FactionIds,
    IReadOnlyCollection<Guid> ReportedWitnessIds,
    int Penalty
);
