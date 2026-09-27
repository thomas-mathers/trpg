namespace TRPG.Application.LocationSimulation;

public record ActiveLocationPlayer(Guid LocationId, Guid PlayerId, int PlayerLevel);

internal static class ActiveLocations
{
    // Several players can watch one location; it is simulated once, on behalf of the highest-level one.
    public static IReadOnlyList<ActiveLocationPlayer> Distinct(
        IReadOnlyCollection<ActiveLocationPlayer> players
    ) =>
        players
            .GroupBy(player => player.LocationId)
            .Select(group =>
                group
                    .OrderByDescending(player => player.PlayerLevel)
                    .ThenBy(player => player.PlayerId)
                    .First()
            )
            .OrderBy(player => player.LocationId)
            .ToArray();
}
