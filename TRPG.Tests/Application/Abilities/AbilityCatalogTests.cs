using TRPG.Application.Abilities;

namespace TRPG.Tests.Application.Abilities;

public class AbilityCatalogTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedPrerequisites =
        new Dictionary<string, string[]>
        {
            ["Absolute Zero"] = ["Glacial Spike"],
            ["Arcane Blast"] = [],
            ["Arcane Infusion"] = [],
            ["Armageddon"] = ["Meteor"],
            ["Arrow Shot"] = [],
            ["Arrow Storm"] = ["Rain of Arrows"],
            ["Assassinate"] = ["Phantasm"],
            ["Backstab"] = ["Stab"],
            ["Ball Lightning"] = ["Thunderstorm"],
            ["Barrage"] = ["Multishot"],
            ["Battle Stance"] = [],
            ["Berserker Stance"] = ["Battle Stance"],
            ["Bladestorm"] = ["Whirlwind"],
            ["Blizzard"] = ["Frost Bolt"],
            ["Block"] = [],
            ["Chain Lightning"] = [],
            ["Cleave"] = ["Slash"],
            ["Concussive Shot"] = ["Crippling Arrow"],
            ["Cripple"] = ["Hamstring"],
            ["Crippling Arrow"] = ["Piercing Shot"],
            ["Death Blossom"] = ["Marked for Death"],
            ["Death from Afar"] = ["Pinning Shot"],
            ["Destroy Undead"] = ["Radiant Burst"],
            ["Devastate"] = ["Cleave"],
            ["Disarm"] = ["Hamstring"],
            ["Divine Intervention"] = ["Greater Mend"],
            ["Divine Shield"] = [],
            ["Eternal Whirlwind"] = ["Bladestorm"],
            ["Execute"] = ["Whirlwind"],
            ["Fear"] = [],
            ["Fireball"] = [],
            ["Fortify"] = ["Iron Will"],
            ["Frost Bolt"] = [],
            ["Garrote"] = ["Kidney Shot"],
            ["Glacial Spike"] = ["Ice Lance"],
            ["Greater Mend"] = ["Mend"],
            ["Hamstring"] = ["Stab"],
            ["Haste"] = [],
            ["Hemorrhage"] = ["Hamstring"],
            ["Hemorrhagic Frenzy"] = ["Death Blossom"],
            ["Hundred Blades"] = ["Bladestorm"],
            ["Ice Lance"] = ["Frost Bolt"],
            ["Inferno"] = ["Fireball"],
            ["Iron Will"] = ["Block"],
            ["Kidney Shot"] = ["Backstab"],
            ["Legend's Blow"] = ["Hundred Blades"],
            ["Legion Might"] = ["War Cry"],
            ["Maddening Whispers"] = ["Fear"],
            ["Marked for Death"] = ["Hemorrhage"],
            ["Mass Heal"] = ["Regenerate"],
            ["Mend"] = [],
            ["Meteor"] = ["Scorch"],
            ["Mortal Strike"] = ["Execute"],
            ["Multishot"] = ["Volley"],
            ["Mystic Focus"] = ["Arcane Infusion"],
            ["Overwhelming Terror"] = ["Paralyze"],
            ["Paralyze"] = ["Maddening Whispers"],
            ["Phantasm"] = ["Shadowstep Strike"],
            ["Piercing Shot"] = ["Arrow Shot"],
            ["Pinning Shot"] = ["Crippling Arrow"],
            ["Poison Arrow"] = ["Arrow Shot"],
            ["Poison Cloud"] = [],
            ["Quickdraw"] = ["Arrow Shot"],
            ["Radiant Burst"] = ["Turn Undead"],
            ["Rain of Arrows"] = ["Barrage"],
            ["Rally"] = ["Battle Stance"],
            ["Regenerate"] = ["Mend"],
            ["Resurrection Pulse"] = ["Sacred Ground"],
            ["Riposte"] = ["Shield Bash"],
            ["Sacred Ground"] = ["Mass Heal"],
            ["Scorch"] = ["Fireball"],
            ["Shadowstep Strike"] = ["Backstab"],
            ["Shield Bash"] = ["Block"],
            ["Silencing Word"] = [],
            ["Skull Splitter"] = ["Mortal Strike"],
            ["Slash"] = [],
            ["Smite"] = [],
            ["Spell Ward"] = ["Arcane Infusion"],
            ["Stab"] = [],
            ["Thunderstorm"] = ["Chain Lightning"],
            ["Turn Undead"] = ["Smite"],
            ["Venom Strike"] = ["Poison Arrow"],
            ["Void Bolt"] = ["Arcane Blast"],
            ["Volley"] = ["Arrow Shot"],
            ["War Cry"] = ["Rally"],
            ["Whirlwind"] = ["Cleave"],
            ["Wrath of the Divine"] = ["Destroy Undead"],
        };

    [Fact]
    public void Prerequisites_MatchExpectedAbilityTree()
    {
        // Act
        var actual = AbilityCatalog.Abilities.ToDictionary(
            a => a.Name,
            a => a.Prerequisites.OrderBy(p => p, StringComparer.Ordinal).ToArray()
        );

        // Assert
        Assert.Equal(
            ExpectedPrerequisites.Keys.OrderBy(k => k, StringComparer.Ordinal),
            actual.Keys.OrderBy(k => k, StringComparer.Ordinal)
        );
        foreach (var (name, expectedPrerequisites) in ExpectedPrerequisites)
        {
            Assert.Equal(expectedPrerequisites, actual[name]);
        }
    }
}
