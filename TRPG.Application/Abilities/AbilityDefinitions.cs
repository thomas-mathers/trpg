using TRPG.Data.Models;

namespace TRPG.Application.Abilities;

public class AbilityDefinitions(
    Dictionary<string, Ability> byName,
    Dictionary<string, HashSet<string>> prerequisites
)
{
    public IReadOnlyCollection<Ability> Abilities => byName.Values;

    public AttackAbility BasicAttack { get; } =
        new()
        {
            Name = "Strike",
            Description = "A plain attack with whatever is at hand.",
            ApCost = 0,
            Cooldown = 0,
            TargetType = AttackTargetType.Single,
            DamageType = DamageType.Physical,
            DamageAmount = 100,
            DamageAmountType = AmountType.Flat,
        };

    public BuffAbility BlockStance { get; } =
        new()
        {
            Name = "Block",
            Description = "Raise your guard, doubling your effective defense until your next turn.",
            ApCost = 2,
            Cooldown = 0,
            TargetType = TargetType.Self,
            Duration = 1,
            Modifiers =
            [
                new AttributeModifier
                {
                    Attribute = AttributeName.Defense,
                    AmountType = AmountType.Percent,
                    Amount = 100,
                },
            ],
        };

    public static AbilityDefinitions Create()
    {
        var builder = new AbilityBuilder();
        AddSwordsmanshipAbilities(builder);
        AddSwordsmanshipAdvancedAbilities(builder);
        AddStealthAbilities(builder);
        AddStealthAdvancedAbilities(builder);
        AddSpellcastingAbilities(builder);
        AddSpellcastingAdvancedAbilities(builder);
        AddArcheryAbilities(builder);
        AddArcheryAdvancedAbilities(builder);
        AddDevotionAbilities(builder);
        AddDevotionAdvancedAbilities(builder);
        AddWarfareAbilities(builder);
        AddWarfareAdvancedAbilities(builder);
        return new AbilityDefinitions(builder.ByName, builder.Prerequisites);
    }

    public string RandomAttackAbility()
    {
        var attacks = Abilities.OfType<AttackAbility>().ToList();
        return attacks[Random.Shared.Next(attacks.Count)].Name;
    }

    public Ability? GetByName(string name) => byName.GetValueOrDefault(name);

    private static void AddSwordsmanshipAbilities(AbilityBuilder builder)
    {
        var slash = builder.AddAttack(
            "Slash",
            "A basic melee strike.",
            Skill.Swordsmanship,
            1,
            2,
            0,
            AttackTargetType.Single,
            DamageType.Physical,
            150
        );
        var cleave = builder
            .AddAttack(
                "Cleave",
                "A wide swing that hits nearby enemies.",
                Skill.Swordsmanship,
                2,
                3,
                0,
                AttackTargetType.Aoe,
                DamageType.Physical,
                170
            )
            .Requires(slash);
        var shieldBash = builder
            .AddAttack(
                "Shield Bash",
                "A stunning blow with the shield.",
                Skill.Swordsmanship,
                2,
                2,
                2,
                AttackTargetType.Single,
                DamageType.Physical,
                130
            )
            .AddStatus(ConditionType.Stunned, 1)
            .Requires(slash);
        builder
            .AddAttack(
                "Devastate",
                "A crippling strike that leaves the target bleeding.",
                Skill.Swordsmanship,
                4,
                4,
                3,
                AttackTargetType.Single,
                DamageType.Physical,
                185
            )
            .AddDot(2, 3f)
            .AddStatus(ConditionType.Bleeding, 2)
            .Requires(shieldBash);
        builder
            .AddAttack(
                "Whirlwind",
                "A spinning strike that damages all surrounding enemies.",
                Skill.Swordsmanship,
                5,
                5,
                3,
                AttackTargetType.Aoe,
                DamageType.Physical,
                210
            )
            .Requires(cleave);
    }

    private static void AddSwordsmanshipAdvancedAbilities(AbilityBuilder builder)
    {
        builder.AddAttack(
            "Execute",
            "A powerful finishing blow aimed at weakened foes.",
            Skill.Swordsmanship,
            20,
            6,
            4,
            AttackTargetType.Single,
            DamageType.Physical,
            245
        );
        builder
            .AddAttack(
                "Riposte",
                "A swift counter-strike following a parry.",
                Skill.Swordsmanship,
                25,
                5,
                5,
                AttackTargetType.Single,
                DamageType.Physical,
                220
            )
            .AddStatus(ConditionType.Stunned, 1);
        builder.AddPrerequisiteByName("Execute", "Whirlwind");
        builder.AddPrerequisiteByName("Riposte", "Shield Bash");

        builder
            .AddAttack(
                "Mortal Strike",
                "A savage blow that opens a wound refusing to close.",
                Skill.Swordsmanship,
                40,
                8,
                6,
                AttackTargetType.Single,
                DamageType.Physical,
                230
            )
            .AddDot(3, 5f)
            .AddStatus(ConditionType.Bleeding, 3);
        builder.AddAttack(
            "Bladestorm",
            "A whirling tempest of steel that strikes all nearby foes.",
            Skill.Swordsmanship,
            45,
            10,
            7,
            AttackTargetType.Aoe,
            DamageType.Physical,
            210
        );
        builder.AddPrerequisiteByName("Mortal Strike", "Execute");
        builder.AddPrerequisiteByName("Bladestorm", "Whirlwind");

        builder
            .AddAttack(
                "Skull Splitter",
                "A bone-shattering overhead blow that leaves the target reeling.",
                Skill.Swordsmanship,
                60,
                12,
                8,
                AttackTargetType.Single,
                DamageType.Physical,
                290
            )
            .AddStatus(ConditionType.Stunned, 2);
        builder.AddAttack(
            "Hundred Blades",
            "A blinding sequence of precise cuts delivered in an instant.",
            Skill.Swordsmanship,
            65,
            15,
            10,
            AttackTargetType.Single,
            DamageType.Physical,
            340
        );
        builder.AddPrerequisiteByName("Skull Splitter", "Mortal Strike");
        builder.AddPrerequisiteByName("Hundred Blades", "Bladestorm");

        builder.AddAttack(
            "Legend's Blow",
            "A mythic strike of such force it shakes the earth.",
            Skill.Swordsmanship,
            90,
            20,
            15,
            AttackTargetType.Single,
            DamageType.Physical,
            520
        );
        builder.AddAttack(
            "Eternal Whirlwind",
            "A cyclone of steel that never stops turning.",
            Skill.Swordsmanship,
            95,
            22,
            15,
            AttackTargetType.Aoe,
            DamageType.Physical,
            370
        );
        builder.AddPrerequisiteByName("Legend's Blow", "Hundred Blades");
        builder.AddPrerequisiteByName("Eternal Whirlwind", "Bladestorm");
    }

    private static void AddStealthAbilities(AbilityBuilder builder)
    {
        var stab = builder.AddAttack(
            "Stab",
            "A quick jab at a vital point.",
            Skill.Stealth,
            1,
            1,
            0,
            AttackTargetType.Single,
            DamageType.Physical,
            130
        );
        var backstab = builder
            .AddAttack(
                "Backstab",
                "A precision strike from an unexpected angle.",
                Skill.Stealth,
                2,
                3,
                0,
                AttackTargetType.Single,
                DamageType.Physical,
                195
            )
            .Requires(stab);
        var hamstring = builder
            .AddAttack(
                "Hamstring",
                "A cut to the legs that slows the target.",
                Skill.Stealth,
                2,
                2,
                2,
                AttackTargetType.Single,
                DamageType.Physical,
                135
            )
            .AddStatus(ConditionType.Snared, 2)
            .Requires(stab);
        builder
            .AddAttack(
                "Hemorrhage",
                "A deep wound that causes severe bleeding.",
                Skill.Stealth,
                4,
                3,
                2,
                AttackTargetType.Single,
                DamageType.Physical,
                150
            )
            .AddDot(3, 4f)
            .AddStatus(ConditionType.Bleeding, 3)
            .Requires(hamstring);
        builder
            .AddAttack(
                "Shadowstep Strike",
                "A gap-closing attack that blinds the target.",
                Skill.Stealth,
                5,
                4,
                3,
                AttackTargetType.Single,
                DamageType.Physical,
                220
            )
            .AddStatus(ConditionType.Blinded, 1)
            .Requires(backstab);
    }

    private static void AddStealthAdvancedAbilities(AbilityBuilder builder)
    {
        builder
            .AddAttack(
                "Kidney Shot",
                "A blow to vital organs that leaves the target winded.",
                Skill.Stealth,
                20,
                5,
                5,
                AttackTargetType.Single,
                DamageType.Physical,
                190
            )
            .AddStatus(ConditionType.Stunned, 2);
        builder
            .AddAttack(
                "Cripple",
                "A precise cut to the tendons that hinders movement.",
                Skill.Stealth,
                25,
                5,
                4,
                AttackTargetType.Single,
                DamageType.Physical,
                170
            )
            .AddStatus(ConditionType.Snared, 3);
        builder.AddPrerequisiteByName("Kidney Shot", "Backstab");
        builder.AddPrerequisiteByName("Cripple", "Hamstring");

        builder
            .AddAttack(
                "Garrote",
                "A choking hold that silences the target.",
                Skill.Stealth,
                40,
                7,
                6,
                AttackTargetType.Single,
                DamageType.Physical,
                210
            )
            .AddStatus(ConditionType.Silenced, 3);
        builder
            .AddAttack(
                "Marked for Death",
                "A cursed strike that ensures the target bleeds out.",
                Skill.Stealth,
                45,
                8,
                7,
                AttackTargetType.Single,
                DamageType.Physical,
                230
            )
            .AddDot(4, 6f)
            .AddStatus(ConditionType.Bleeding, 4);
        builder.AddPrerequisiteByName("Garrote", "Kidney Shot");
        builder.AddPrerequisiteByName("Marked for Death", "Hemorrhage");

        builder
            .AddAttack(
                "Phantasm",
                "Move faster than the eye can follow, then strike from shadow.",
                Skill.Stealth,
                60,
                12,
                9,
                AttackTargetType.Single,
                DamageType.Physical,
                290
            )
            .AddStatus(ConditionType.Blinded, 2);
        builder
            .AddAttack(
                "Death Blossom",
                "A flurry of cuts that leaves the target hemorrhaging.",
                Skill.Stealth,
                65,
                14,
                10,
                AttackTargetType.Single,
                DamageType.Physical,
                250
            )
            .AddDot(4, 6f)
            .AddStatus(ConditionType.Bleeding, 4);
        builder.AddPrerequisiteByName("Phantasm", "Shadowstep Strike");
        builder.AddPrerequisiteByName("Death Blossom", "Marked for Death");

        builder.AddAttack(
            "Assassinate",
            "A perfectly placed strike at a vital point.",
            Skill.Stealth,
            90,
            20,
            15,
            AttackTargetType.Single,
            DamageType.Physical,
            490
        );
        builder
            .AddAttack(
                "Hemorrhagic Frenzy",
                "A relentless barrage of bleeding cuts.",
                Skill.Stealth,
                95,
                22,
                16,
                AttackTargetType.Single,
                DamageType.Physical,
                210
            )
            .AddDot(5, 8f)
            .AddStatus(ConditionType.Bleeding, 5);
        builder.AddPrerequisiteByName("Assassinate", "Phantasm");
        builder.AddPrerequisiteByName("Hemorrhagic Frenzy", "Death Blossom");
    }

    private static void AddSpellcastingAbilities(AbilityBuilder builder)
    {
        var frostBolt = builder
            .AddAttack(
                "Frost Bolt",
                "A bolt of ice that damages and snares the target.",
                Skill.Spellcasting,
                1,
                3,
                0,
                AttackTargetType.Single,
                DamageType.Ice,
                10
            )
            .AddStatus(ConditionType.Snared, 1);
        var fireball = builder
            .AddAttack(
                "Fireball",
                "A ball of fire hurled at a single target.",
                Skill.Spellcasting,
                1,
                3,
                0,
                AttackTargetType.Single,
                DamageType.Fire,
                12
            )
            .AddDot(1, 3f)
            .AddStatus(ConditionType.Burning, 1);
        var chainLightning = builder.AddAttack(
            "Chain Lightning",
            "Lightning that arcs between nearby enemies.",
            Skill.Spellcasting,
            2,
            4,
            2,
            AttackTargetType.Aoe,
            DamageType.Lightning,
            15
        );
        builder
            .AddAttack(
                "Poison Cloud",
                "A cloud of toxic vapour that poisons all within.",
                Skill.Spellcasting,
                2,
                3,
                2,
                AttackTargetType.Aoe,
                DamageType.Poison,
                8
            )
            .AddDot(3, 3f)
            .AddStatus(ConditionType.Poisoned, 3);
        builder
            .AddAttack(
                "Arcane Blast",
                "A concussive burst of raw magic that silences the target.",
                Skill.Spellcasting,
                3,
                4,
                1,
                AttackTargetType.Single,
                DamageType.Magic,
                18
            )
            .AddStatus(ConditionType.Silenced, 1);
        builder
            .AddAttack(
                "Blizzard",
                "A freezing storm that chills all nearby enemies.",
                Skill.Spellcasting,
                4,
                5,
                3,
                AttackTargetType.Aoe,
                DamageType.Ice,
                14
            )
            .AddStatus(ConditionType.Frozen, 1)
            .Requires(frostBolt);
        builder
            .AddAttack(
                "Inferno",
                "A massive eruption of flame that engulfs a wide area.",
                Skill.Spellcasting,
                4,
                6,
                4,
                AttackTargetType.Aoe,
                DamageType.Fire,
                20
            )
            .AddDot(2, 4f)
            .AddStatus(ConditionType.Burning, 2)
            .Requires(fireball);
        builder
            .AddAttack(
                "Thunderstorm",
                "A violent storm that electrocutes all enemies in the area.",
                Skill.Spellcasting,
                6,
                7,
                5,
                AttackTargetType.Aoe,
                DamageType.Lightning,
                25
            )
            .AddStatus(ConditionType.Stunned, 1)
            .Requires(chainLightning);
    }

    private static void AddSpellcastingAdvancedAbilities(AbilityBuilder builder)
    {
        builder
            .AddAttack(
                "Ice Lance",
                "A piercing lance of concentrated ice.",
                Skill.Spellcasting,
                20,
                6,
                3,
                AttackTargetType.Single,
                DamageType.Ice,
                22
            )
            .AddStatus(ConditionType.Frozen, 1);
        builder
            .AddAttack(
                "Scorch",
                "A searing beam that ignites the target.",
                Skill.Spellcasting,
                25,
                6,
                4,
                AttackTargetType.Single,
                DamageType.Fire,
                24
            )
            .AddDot(2, 4f)
            .AddStatus(ConditionType.Burning, 2);
        builder.AddPrerequisiteByName("Ice Lance", "Frost Bolt");
        builder.AddPrerequisiteByName("Scorch", "Fireball");

        builder
            .AddAttack(
                "Glacial Spike",
                "A massive spike of ice erupts from the earth.",
                Skill.Spellcasting,
                40,
                10,
                6,
                AttackTargetType.Single,
                DamageType.Ice,
                32
            )
            .AddStatus(ConditionType.Frozen, 2);
        builder
            .AddAttack(
                "Meteor",
                "A blazing rock falls from the heavens.",
                Skill.Spellcasting,
                45,
                12,
                8,
                AttackTargetType.Aoe,
                DamageType.Fire,
                28
            )
            .AddDot(2, 3f)
            .AddStatus(ConditionType.Burning, 2);
        builder.AddPrerequisiteByName("Glacial Spike", "Ice Lance");
        builder.AddPrerequisiteByName("Meteor", "Scorch");

        builder
            .AddAttack(
                "Absolute Zero",
                "A field of absolute cold that freezes all within.",
                Skill.Spellcasting,
                60,
                15,
                10,
                AttackTargetType.Aoe,
                DamageType.Ice,
                22
            )
            .AddStatus(ConditionType.Frozen, 2);
        builder
            .AddAttack(
                "Ball Lightning",
                "A rolling sphere of crackling lightning.",
                Skill.Spellcasting,
                65,
                14,
                9,
                AttackTargetType.Aoe,
                DamageType.Lightning,
                35
            )
            .AddStatus(ConditionType.Stunned, 1);
        builder.AddPrerequisiteByName("Absolute Zero", "Glacial Spike");
        builder.AddPrerequisiteByName("Ball Lightning", "Thunderstorm");

        builder
            .AddAttack(
                "Armageddon",
                "A cataclysmic firestorm that consumes everything.",
                Skill.Spellcasting,
                90,
                22,
                15,
                AttackTargetType.Aoe,
                DamageType.Fire,
                55
            )
            .AddDot(3, 5f)
            .AddStatus(ConditionType.Burning, 3);
        builder
            .AddAttack(
                "Void Bolt",
                "A bolt of pure arcane entropy that unmakes what it touches.",
                Skill.Spellcasting,
                100,
                25,
                20,
                AttackTargetType.Single,
                DamageType.Magic,
                80
            )
            .AddStatus(ConditionType.Silenced, 3);
        builder.AddPrerequisiteByName("Armageddon", "Meteor");
        builder.AddPrerequisiteByName("Void Bolt", "Arcane Blast");
    }

    private static void AddArcheryAbilities(AbilityBuilder builder)
    {
        var arrowShot = builder.AddAttack(
            "Arrow Shot",
            "A standard ranged attack.",
            Skill.Archery,
            1,
            2,
            0,
            AttackTargetType.Single,
            DamageType.Physical,
            150
        );
        builder
            .AddAttack(
                "Piercing Shot",
                "An arrow that drives deep and causes bleeding.",
                Skill.Archery,
                2,
                3,
                0,
                AttackTargetType.Single,
                DamageType.Physical,
                185
            )
            .AddDot(1, 3f)
            .AddStatus(ConditionType.Bleeding, 1)
            .Requires(arrowShot);
        builder
            .AddAttack(
                "Poison Arrow",
                "A barbed arrow coated with toxin.",
                Skill.Archery,
                3,
                3,
                2,
                AttackTargetType.Single,
                DamageType.Poison,
                6
            )
            .AddDot(3, 3f)
            .AddStatus(ConditionType.Poisoned, 3)
            .Requires(arrowShot);
        builder
            .AddAttack(
                "Volley",
                "A spread of arrows targeting a wide area.",
                Skill.Archery,
                4,
                5,
                3,
                AttackTargetType.Aoe,
                DamageType.Physical,
                160
            )
            .Requires(arrowShot);
    }

    private static void AddArcheryAdvancedAbilities(AbilityBuilder builder)
    {
        builder
            .AddAttack(
                "Crippling Arrow",
                "An arrow that buries deep and hinders movement.",
                Skill.Archery,
                20,
                5,
                4,
                AttackTargetType.Single,
                DamageType.Physical,
                190
            )
            .AddStatus(ConditionType.Snared, 3);
        builder.AddAttack(
            "Multishot",
            "Release a spread of arrows simultaneously.",
            Skill.Archery,
            25,
            7,
            5,
            AttackTargetType.Aoe,
            DamageType.Physical,
            170
        );
        builder.AddPrerequisiteByName("Crippling Arrow", "Piercing Shot");
        builder.AddPrerequisiteByName("Multishot", "Volley");

        builder.AddAttack(
            "Barrage",
            "An unending rain of arrows over a wide area.",
            Skill.Archery,
            40,
            10,
            7,
            AttackTargetType.Aoe,
            DamageType.Physical,
            160
        );
        builder
            .AddAttack(
                "Venom Strike",
                "A deeply embedded arrow laced with deadly toxin.",
                Skill.Archery,
                45,
                8,
                6,
                AttackTargetType.Single,
                DamageType.Poison,
                14
            )
            .AddDot(4, 6f)
            .AddStatus(ConditionType.Poisoned, 4);
        builder.AddPrerequisiteByName("Barrage", "Multishot");
        builder.AddPrerequisiteByName("Venom Strike", "Poison Arrow");

        builder.AddAttack(
            "Rain of Arrows",
            "A devastating blanket of arrows covering a vast area.",
            Skill.Archery,
            60,
            14,
            9,
            AttackTargetType.Aoe,
            DamageType.Physical,
            195
        );
        builder
            .AddAttack(
                "Pinning Shot",
                "An arrow that nails the target in place.",
                Skill.Archery,
                65,
                10,
                8,
                AttackTargetType.Single,
                DamageType.Physical,
                250
            )
            .AddStatus(ConditionType.Snared, 4);
        builder.AddPrerequisiteByName("Rain of Arrows", "Barrage");
        builder.AddPrerequisiteByName("Pinning Shot", "Crippling Arrow");

        builder.AddAttack(
            "Arrow Storm",
            "An incomprehensible density of arrows raining from the sky.",
            Skill.Archery,
            90,
            20,
            14,
            AttackTargetType.Aoe,
            DamageType.Physical,
            310
        );
        builder.AddAttack(
            "Death from Afar",
            "A single perfect shot from extreme range.",
            Skill.Archery,
            95,
            22,
            16,
            AttackTargetType.Single,
            DamageType.Physical,
            490
        );
        builder.AddPrerequisiteByName("Arrow Storm", "Rain of Arrows");
        builder.AddPrerequisiteByName("Death from Afar", "Pinning Shot");
    }

    private static void AddDevotionAbilities(AbilityBuilder builder)
    {
        var mend = builder.AddInstantHeal(
            "Mend",
            "Restores a portion of an ally's health.",
            Skill.Devotion,
            1,
            2,
            0,
            TargetType.Single,
            15
        );
        var divineShield = builder
            .AddBuff(
                "Divine Shield",
                "Fortifies an ally with a magical barrier.",
                Skill.Devotion,
                2,
                3,
                3,
                TargetType.Single,
                2
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.2f)
            .Requires(mend);
        var regenerate = builder
            .AddHealOverTime(
                "Regenerate",
                "Grants an ally health regeneration over time.",
                Skill.Devotion,
                2,
                3,
                3,
                TargetType.Single,
                8,
                3
            )
            .Requires(mend);
        builder
            .AddBuff(
                "Aura of Protection",
                "Grants nearby allies increased physical resistance.",
                Skill.Devotion,
                4,
                5,
                5,
                TargetType.Aoe,
                3
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.15f)
            .Requires(divineShield);
        builder
            .AddInstantHeal(
                "Mass Heal",
                "Restores health to all nearby allies.",
                Skill.Devotion,
                5,
                5,
                4,
                TargetType.Aoe,
                12
            )
            .Requires(regenerate);
    }

    private static void AddDevotionAdvancedAbilities(AbilityBuilder builder)
    {
        builder.AddInstantHeal(
            "Greater Mend",
            "A stronger healing touch that restores more vitality.",
            Skill.Devotion,
            20,
            6,
            4,
            TargetType.Single,
            28
        );
        builder
            .AddBuff(
                "Fortify",
                "Strengthen an ally's defenses significantly.",
                Skill.Devotion,
                25,
                6,
                5,
                TargetType.Single,
                3
            )
            .AddModifier(AttributeName.Defense, 15)
            .AddModifier(AttributeName.Endurance, 8);
        builder.AddPrerequisiteByName("Greater Mend", "Mend");
        builder.AddPrerequisiteByName("Fortify", "Divine Shield");

        builder.AddHealOverTime(
            "Sacred Ground",
            "Consecrate an area that heals allies who stand within.",
            Skill.Devotion,
            40,
            10,
            7,
            TargetType.Aoe,
            12,
            3
        );
        builder
            .AddBuff(
                "Barrier",
                "Wrap an ally in a powerful magical shield.",
                Skill.Devotion,
                45,
                10,
                8,
                TargetType.Single,
                3
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.3f)
            .AddModifier(AttributeName.MagicResistance, 0.2f);
        builder.AddPrerequisiteByName("Sacred Ground", "Mass Heal");
        builder.AddPrerequisiteByName("Barrier", "Fortify");

        builder.AddHealOverTime(
            "Resurrection Pulse",
            "A wave of healing energy that restores all nearby allies.",
            Skill.Devotion,
            60,
            15,
            10,
            TargetType.Aoe,
            15,
            4
        );
        builder
            .AddBuff(
                "Aegis",
                "Envelop an ally in an impenetrable divine shell.",
                Skill.Devotion,
                65,
                14,
                9,
                TargetType.Single,
                2
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.5f);
        builder.AddPrerequisiteByName("Resurrection Pulse", "Sacred Ground");
        builder.AddPrerequisiteByName("Aegis", "Barrier");

        builder.AddInstantHeal(
            "Divine Intervention",
            "Call upon divine power to massively restore a single ally.",
            Skill.Devotion,
            90,
            22,
            15,
            TargetType.Single,
            70
        );
        builder
            .AddBuff(
                "Sanctuary",
                "Create a holy sanctum that shields all within from harm.",
                Skill.Devotion,
                95,
                22,
                16,
                TargetType.Aoe,
                4
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.4f)
            .AddModifier(AttributeName.MagicResistance, 0.4f);
        builder.AddPrerequisiteByName("Divine Intervention", "Greater Mend");
        builder.AddPrerequisiteByName("Sanctuary", "Aegis");
    }

    private static void AddWarfareAbilities(AbilityBuilder builder)
    {
        var battleStance = builder
            .AddBuff(
                "Battle Stance",
                "Braces the caster for combat, increasing strength.",
                Skill.Warfare,
                1,
                1,
                0,
                TargetType.Self,
                3
            )
            .AddModifier(AttributeName.Strength, 5);
        var arcaneInfusion = builder
            .AddBuff(
                "Arcane Infusion",
                "Infuses an ally with arcane energy, boosting intelligence.",
                Skill.Warfare,
                1,
                2,
                2,
                TargetType.Single,
                3
            )
            .AddModifier(AttributeName.Intelligence, 10);
        builder
            .AddBuff(
                "Iron Will",
                "Hardens the caster's resolve against physical harm.",
                Skill.Warfare,
                2,
                2,
                3,
                TargetType.Self,
                2
            )
            .AddModifier(AttributeName.Defense, 10)
            .AddModifier(AttributeName.Endurance, 10)
            .Requires(battleStance);
        builder
            .AddBuff(
                "Haste",
                "Accelerates an ally's movements and reflexes.",
                Skill.Warfare,
                2,
                2,
                3,
                TargetType.Single,
                2
            )
            .AddModifier(AttributeName.Dexterity, 5)
            .AddModifier(AttributeName.MovementSpeed, 3);
        builder
            .AddBuff(
                "Rally",
                "A battle cry that strengthens nearby allies.",
                Skill.Warfare,
                3,
                3,
                4,
                TargetType.Aoe,
                2
            )
            .AddModifier(AttributeName.Strength, 8)
            .Requires(battleStance);
        builder
            .AddBuff(
                "Spell Ward",
                "Erects a magical barrier against incoming spells.",
                Skill.Warfare,
                3,
                3,
                4,
                TargetType.Self,
                3
            )
            .AddModifier(AttributeName.MagicResistance, 0.2f)
            .Requires(arcaneInfusion);
    }

    private static void AddWarfareAdvancedAbilities(AbilityBuilder builder)
    {
        builder
            .AddBuff(
                "War Cry",
                "A thunderous battle cry that strengthens all nearby allies.",
                Skill.Warfare,
                20,
                6,
                5,
                TargetType.Aoe,
                3
            )
            .AddModifier(AttributeName.Strength, 10)
            .AddModifier(AttributeName.Defense, 5);
        builder
            .AddBuff(
                "Mystic Focus",
                "Channel arcane energy for enhanced spellcasting.",
                Skill.Warfare,
                25,
                6,
                5,
                TargetType.Self,
                3
            )
            .AddModifier(AttributeName.Intelligence, 15)
            .AddModifier(AttributeName.Mana, 10);
        builder.AddPrerequisiteByName("War Cry", "Rally");
        builder.AddPrerequisiteByName("Mystic Focus", "Arcane Infusion");

        builder
            .AddBuff(
                "Berserker Stance",
                "Abandon defense for unbridled offensive power.",
                Skill.Warfare,
                40,
                8,
                6,
                TargetType.Self,
                3
            )
            .AddModifier(AttributeName.Strength, 20)
            .AddModifier(AttributeName.Dexterity, 10);
        builder
            .AddBuff(
                "Arcane Shield",
                "A barrier of raw magic that absorbs incoming spells.",
                Skill.Warfare,
                45,
                8,
                7,
                TargetType.Self,
                3
            )
            .AddModifier(AttributeName.MagicResistance, 0.35f);
        builder.AddPrerequisiteByName("Berserker Stance", "Battle Stance");
        builder.AddPrerequisiteByName("Arcane Shield", "Spell Ward");

        builder
            .AddBuff(
                "Warlord's Presence",
                "Your commanding presence bolsters all nearby allies.",
                Skill.Warfare,
                60,
                14,
                9,
                TargetType.Aoe,
                3
            )
            .AddModifier(AttributeName.Strength, 15)
            .AddModifier(AttributeName.Defense, 15)
            .AddModifier(AttributeName.Endurance, 10);
        builder
            .AddBuff(
                "Time Warp",
                "Briefly accelerate an ally far beyond their natural speed.",
                Skill.Warfare,
                65,
                12,
                9,
                TargetType.Single,
                2
            )
            .AddModifier(AttributeName.Dexterity, 25)
            .AddModifier(AttributeName.MovementSpeed, 8);
        builder.AddPrerequisiteByName("Warlord's Presence", "War Cry");
        builder.AddPrerequisiteByName("Time Warp", "Haste");

        builder
            .AddBuff(
                "Invincible",
                "For a brief moment, nothing can stop you.",
                Skill.Warfare,
                90,
                20,
                14,
                TargetType.Self,
                2
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.5f)
            .AddModifier(AttributeName.MagicResistance, 0.5f)
            .AddModifier(AttributeName.Endurance, 20);
        builder
            .AddBuff(
                "Legion Might",
                "A war cry of legendary power that emboldens all nearby.",
                Skill.Warfare,
                95,
                22,
                15,
                TargetType.Aoe,
                3
            )
            .AddModifier(AttributeName.Strength, 25)
            .AddModifier(AttributeName.Defense, 20);
        builder.AddPrerequisiteByName("Invincible", "Iron Will");
        builder.AddPrerequisiteByName("Legion Might", "Warlord's Presence");
    }

    public Ability? GetAbility(string name)
    {
        return byName.GetValueOrDefault(name);
    }

    public IReadOnlyCollection<string> GetPrerequisites(string abilityName)
    {
        return prerequisites.TryGetValue(abilityName, out var prereqs) ? prereqs : [];
    }

    private class AbilityBuilder
    {
        public Dictionary<string, Ability> ByName { get; } = [];
        public Dictionary<string, HashSet<string>> Prerequisites { get; } = [];

        public AttackAbilityEntry AddAttack(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int cost,
            int cooldown,
            AttackTargetType targetType,
            DamageType damageType,
            float damageAmount
        )
        {
            var (apCost, mpCost) = ResourceCost(skill, cost);
            var attack = new AttackAbility
            {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                ApCost = apCost,
                MpCost = mpCost,
                Cooldown = cooldown,
                TargetType = targetType,
                DamageType = damageType,
                DamageAmountType = AmountType.Flat,
                DamageAmount = damageAmount,
            };
            ByName[name] = attack;
            return new AttackAbilityEntry(attack, this);
        }

        public InstantHealAbilityEntry AddInstantHeal(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int cost,
            int cooldown,
            TargetType targetType,
            int amount
        )
        {
            var (apCost, mpCost) = ResourceCost(skill, cost);
            var heal = new InstantHealAbility
            {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                ApCost = apCost,
                MpCost = mpCost,
                Cooldown = cooldown,
                TargetType = targetType,
                Amount = amount,
            };
            ByName[name] = heal;
            return new InstantHealAbilityEntry(heal, this);
        }

        public HealOverTimeAbilityEntry AddHealOverTime(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int cost,
            int cooldown,
            TargetType targetType,
            int amountPerTurn,
            int duration
        )
        {
            var (apCost, mpCost) = ResourceCost(skill, cost);
            var heal = new HealOverTimeAbility
            {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                ApCost = apCost,
                MpCost = mpCost,
                Cooldown = cooldown,
                TargetType = targetType,
                AmountPerTurn = amountPerTurn,
                Duration = duration,
            };
            ByName[name] = heal;
            return new HealOverTimeAbilityEntry(heal, this);
        }

        public BuffAbilityEntry AddBuff(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int cost,
            int cooldown,
            TargetType targetType,
            int duration
        )
        {
            var (apCost, mpCost) = ResourceCost(skill, cost);
            var buff = new BuffAbility
            {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                ApCost = apCost,
                MpCost = mpCost,
                Cooldown = cooldown,
                TargetType = targetType,
                Duration = duration,
            };
            ByName[name] = buff;
            return new BuffAbilityEntry(buff, this);
        }

        private static (int ApCost, int MpCost) ResourceCost(Skill skill, int cost) =>
            skill is Skill.Spellcasting or Skill.Devotion ? (0, cost) : (cost, 0);

        public void AddPrerequisiteByName(string abilityName, string prerequisiteName)
        {
            if (!Prerequisites.TryGetValue(abilityName, out var set))
            {
                set = [];
                Prerequisites[abilityName] = set;
            }

            set.Add(prerequisiteName);
        }

        internal void AddPrerequisite(Ability ability, Ability prereq)
        {
            if (!Prerequisites.TryGetValue(ability.Name, out var set))
            {
                set = [];
                Prerequisites[ability.Name] = set;
            }

            set.Add(prereq.Name);
        }
    }

    private abstract class AbilityEntry(Ability ability, AbilityBuilder owner)
    {
        protected AbilityBuilder Owner { get; } = owner;
        internal Ability Ability { get; } = ability;
    }

    private class AttackAbilityEntry(AttackAbility attack, AbilityBuilder owner)
        : AbilityEntry(attack, owner)
    {
        public AttackAbilityEntry AddDot(
            int duration,
            float amount,
            AmountType amountType = AmountType.Flat
        )
        {
            attack.Dots.Add(
                new DotEffect
                {
                    Duration = duration,
                    Amount = amount,
                    AmountType = amountType,
                }
            );
            return this;
        }

        public AttackAbilityEntry AddStatus(ConditionType condition, int duration)
        {
            attack.Conditions.Add(new StatusEffect { Condition = condition, Duration = duration });
            return this;
        }

        public AttackAbilityEntry Requires(AbilityEntry prereq)
        {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }

    private class InstantHealAbilityEntry(InstantHealAbility heal, AbilityBuilder owner)
        : AbilityEntry(heal, owner)
    {
        public InstantHealAbilityEntry Requires(AbilityEntry prereq)
        {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }

    private class HealOverTimeAbilityEntry(HealOverTimeAbility heal, AbilityBuilder owner)
        : AbilityEntry(heal, owner)
    {
        public HealOverTimeAbilityEntry Requires(AbilityEntry prereq)
        {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }

    private class BuffAbilityEntry(BuffAbility buff, AbilityBuilder owner)
        : AbilityEntry(buff, owner)
    {
        public BuffAbilityEntry AddModifier(AttributeName attribute, float amount)
        {
            buff.Modifiers.Add(
                new AttributeModifier
                {
                    Attribute = attribute,
                    AmountType = AmountType.Flat,
                    Amount = amount,
                }
            );
            return this;
        }

        public BuffAbilityEntry Requires(AbilityEntry prereq)
        {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }
}
