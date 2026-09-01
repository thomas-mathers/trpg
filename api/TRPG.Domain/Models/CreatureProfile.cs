namespace TRPG.Domain.Models;

public class CreatureProfile
{
    public CreatureAppearance Appearance { get; init; } = new();
    public CreatureBehavior Behavior { get; init; } = new();
    public Guid CreatureId { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public CreaturePrivateBackground PrivateBackground { get; init; } = new();
    public Guid WorldId { get; init; }
}

public class CreatureAppearance
{
    public List<string> DistinguishingFeatures { get; init; } = [];
}

public class CreatureBehavior
{
    public string Hobby { get; init; } = "";
    public string Personality { get; init; } = "";
    public string SpeechStyle { get; init; } = "";
}

public class CreaturePrivateBackground
{
    public IReadOnlyCollection<CreatureFamilyMember> Family { get; init; } = [];
    public IReadOnlyCollection<CreatureFaction> Factions { get; init; } = [];
    public string? Home { get; init; }
    public string Origin { get; init; } = "";
    public string? Profession { get; init; }
    public CreatureWorkBackground? Work { get; init; }
}

public record CreatureFamilyMember(string Name, string Relationship);

public record CreatureFaction(Guid Id, string Name, bool IsCityFaction = false);

public class CreatureWorkBackground
{
    public IReadOnlyCollection<string> DaysOff { get; init; } = [];
    public string Building { get; init; } = "";
    public string Hours { get; init; } = "";
    public bool IsOwner { get; init; }
}
