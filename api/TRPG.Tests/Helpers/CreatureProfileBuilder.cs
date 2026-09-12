using TRPG.Domain.Models;

namespace TRPG.Tests.Helpers;

internal sealed class CreatureProfileBuilder
{
    private Guid _worldId = Guid.NewGuid();
    private Guid _creatureId = Guid.NewGuid();
    private string _description = "";
    private CreatureAppearance _appearance = new();
    private CreatureBehavior _behavior = new();
    private CreaturePrivateBackground _background = new();

    public CreatureProfileBuilder WithWorldId(Guid worldId)
    {
        _worldId = worldId;
        return this;
    }

    public CreatureProfileBuilder WithCreatureId(Guid creatureId)
    {
        _creatureId = creatureId;
        return this;
    }

    public CreatureProfileBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public CreatureProfileBuilder WithAppearance(params string[] features)
    {
        _appearance = new CreatureAppearance { DistinguishingFeatures = features.ToList() };
        return this;
    }

    public CreatureProfileBuilder WithBehavior(
        string personality,
        string speechStyle = "",
        string hobby = ""
    )
    {
        _behavior = new CreatureBehavior
        {
            Personality = personality,
            SpeechStyle = speechStyle,
            Hobby = hobby,
        };
        return this;
    }

    public CreatureProfileBuilder WithBackground(CreaturePrivateBackground background)
    {
        _background = background;
        return this;
    }

    public CreatureProfile Build() =>
        new()
        {
            WorldId = _worldId,
            CreatureId = _creatureId,
            Description = _description,
            Appearance = _appearance,
            Behavior = _behavior,
            PrivateBackground = _background,
        };
}
