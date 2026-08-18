namespace TRPG.Domain.Models;

public class NpcProfile
{
    public NpcAppearance Appearance { get; init; } = new();
    public NpcBehavior Behavior { get; init; } = new();
    public Guid CreatureId { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public NpcPrivateBackground PrivateBackground { get; init; } = new();
    public Guid WorldId { get; init; }
}

public class NpcAppearance
{
    public List<string> DistinguishingFeatures { get; init; } = [];
}

public class NpcBehavior
{
    public string Hobby { get; init; } = "";
    public string Personality { get; init; } = "";
    public string SpeechStyle { get; init; } = "";
}

public class NpcPrivateBackground
{
    public IReadOnlyCollection<NpcFamilyMember> Family { get; init; } = [];
    public IReadOnlyCollection<NpcFaction> Factions { get; init; } = [];
    public string? Home { get; init; }
    public string Origin { get; init; } = "";
    public string? Profession { get; init; }
    public NpcWorkBackground? Work { get; init; }
}

public record NpcFamilyMember(string Name, string Relationship);

public record NpcFaction(Guid Id, string Name, bool IsCityFaction = false);

public class NpcWorkBackground
{
    public IReadOnlyCollection<string> DaysOff { get; init; } = [];
    public string Building { get; init; } = "";
    public string Hours { get; init; } = "";
    public bool IsOwner { get; init; }
}
