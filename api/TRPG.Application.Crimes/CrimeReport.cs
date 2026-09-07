namespace TRPG.Application.Crimes;

// One priced offence: who it wronged, who reported it, and what the faction tier costs.
public record CrimeReport(
    IReadOnlyCollection<Guid> FactionIds,
    IReadOnlyCollection<Guid> ReportedWitnessIds,
    Guid? VictimId,
    int Penalty
);
