using TRPG.Data.Models;

namespace TRPG.Definitions;

internal class AbilityDefinitions(
    Dictionary<string, Ability> byName,
    Dictionary<string, HashSet<string>> prerequisites
)
{
    public IReadOnlyCollection<Ability> Abilities => byName.Values;

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

    private static void AddSwordsmanshipAbilities(AbilityBuilder builder)
    {
        var slash = builder.AddAttack(
            "Slash",
            "A basic melee strike.",
            Skill.Swordsmanship,
            1,
            2,
            0,
            TargetType.Single,
            null,
            DamageType.Physical,
            8
        );
        var cleave = builder
            .AddAttack(
                "Cleave",
                "A wide swing that hits nearby enemies.",
                Skill.Swordsmanship,
                2,
                3,
                0,
                TargetType.Aoe,
                2f,
                DamageType.Physical,
                12
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
                TargetType.Single,
                null,
                DamageType.Physical,
                5
            )
            .AddCondition(ConditionType.Stunned, 1)
            .Requires(slash);
        builder
            .AddAttack(
                "Devastate",
                "A crippling strike that leaves the target bleeding.",
                Skill.Swordsmanship,
                4,
                4,
                3,
                TargetType.Single,
                null,
                DamageType.Physical,
                14
            )
            .AddCondition(ConditionType.Bleeding, 2, 3f)
            .Requires(shieldBash);
        builder
            .AddAttack(
                "Whirlwind",
                "A spinning strike that damages all surrounding enemies.",
                Skill.Swordsmanship,
                5,
                5,
                3,
                TargetType.Aoe,
                4f,
                DamageType.Physical,
                18
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
            TargetType.Single,
            null,
            DamageType.Physical,
            24
        );
        builder
            .AddAttack(
                "Riposte",
                "A swift counter-strike following a parry.",
                Skill.Swordsmanship,
                25,
                5,
                5,
                TargetType.Single,
                null,
                DamageType.Physical,
                20
            )
            .AddCondition(ConditionType.Stunned, 1);
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
                TargetType.Single,
                null,
                DamageType.Physical,
                22
            )
            .AddCondition(ConditionType.Bleeding, 3, 5f);
        builder.AddAttack(
            "Bladestorm",
            "A whirling tempest of steel that strikes all nearby foes.",
            Skill.Swordsmanship,
            45,
            10,
            7,
            TargetType.Aoe,
            5f,
            DamageType.Physical,
            18
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
                TargetType.Single,
                null,
                DamageType.Physical,
                32
            )
            .AddCondition(ConditionType.Stunned, 2);
        builder.AddAttack(
            "Hundred Blades",
            "A blinding sequence of precise cuts delivered in an instant.",
            Skill.Swordsmanship,
            65,
            15,
            10,
            TargetType.Single,
            null,
            DamageType.Physical,
            40
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
            TargetType.Single,
            null,
            DamageType.Physical,
            70
        );
        builder.AddAttack(
            "Eternal Whirlwind",
            "A cyclone of steel that never stops turning.",
            Skill.Swordsmanship,
            95,
            22,
            15,
            TargetType.Aoe,
            8f,
            DamageType.Physical,
            45
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
            TargetType.Single,
            null,
            DamageType.Physical,
            5
        );
        var backstab = builder
            .AddAttack(
                "Backstab",
                "A precision strike from an unexpected angle.",
                Skill.Stealth,
                2,
                3,
                0,
                TargetType.Single,
                null,
                DamageType.Physical,
                16
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
                TargetType.Single,
                null,
                DamageType.Physical,
                6
            )
            .AddCondition(ConditionType.Snared, 2)
            .Requires(stab);
        builder
            .AddAttack(
                "Hemorrhage",
                "A deep wound that causes severe bleeding.",
                Skill.Stealth,
                4,
                3,
                2,
                TargetType.Single,
                null,
                DamageType.Physical,
                8
            )
            .AddCondition(ConditionType.Bleeding, 3, 4f)
            .Requires(hamstring);
        builder
            .AddAttack(
                "Shadowstep Strike",
                "A gap-closing attack that blinds the target.",
                Skill.Stealth,
                5,
                4,
                3,
                TargetType.Single,
                null,
                DamageType.Physical,
                20
            )
            .AddCondition(ConditionType.Blinded, 1)
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
                TargetType.Single,
                null,
                DamageType.Physical,
                15
            )
            .AddCondition(ConditionType.Stunned, 2);
        builder
            .AddAttack(
                "Cripple",
                "A precise cut to the tendons that hinders movement.",
                Skill.Stealth,
                25,
                5,
                4,
                TargetType.Single,
                null,
                DamageType.Physical,
                12
            )
            .AddCondition(ConditionType.Snared, 3);
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
                TargetType.Single,
                null,
                DamageType.Physical,
                18
            )
            .AddCondition(ConditionType.Silenced, 3);
        builder
            .AddAttack(
                "Marked for Death",
                "A cursed strike that ensures the target bleeds out.",
                Skill.Stealth,
                45,
                8,
                7,
                TargetType.Single,
                null,
                DamageType.Physical,
                22
            )
            .AddCondition(ConditionType.Bleeding, 4, 6f);
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
                TargetType.Single,
                null,
                DamageType.Physical,
                32
            )
            .AddCondition(ConditionType.Blinded, 2);
        builder
            .AddAttack(
                "Death Blossom",
                "A flurry of cuts that leaves the target hemorrhaging.",
                Skill.Stealth,
                65,
                14,
                10,
                TargetType.Single,
                null,
                DamageType.Physical,
                25
            )
            .AddCondition(ConditionType.Bleeding, 4, 6f);
        builder.AddPrerequisiteByName("Phantasm", "Shadowstep Strike");
        builder.AddPrerequisiteByName("Death Blossom", "Marked for Death");

        builder.AddAttack(
            "Assassinate",
            "A perfectly placed strike at a vital point.",
            Skill.Stealth,
            90,
            20,
            15,
            TargetType.Single,
            null,
            DamageType.Physical,
            65
        );
        builder
            .AddAttack(
                "Hemorrhagic Frenzy",
                "A relentless barrage of bleeding cuts.",
                Skill.Stealth,
                95,
                22,
                16,
                TargetType.Single,
                null,
                DamageType.Physical,
                18
            )
            .AddCondition(ConditionType.Bleeding, 5, 8f);
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
                TargetType.Single,
                null,
                DamageType.Ice,
                10
            )
            .AddCondition(ConditionType.Snared, 1);
        var fireball = builder
            .AddAttack(
                "Fireball",
                "A ball of fire hurled at a single target.",
                Skill.Spellcasting,
                1,
                3,
                0,
                TargetType.Single,
                null,
                DamageType.Fire,
                12
            )
            .AddCondition(ConditionType.Burning, 1, 3f);
        var chainLightning = builder.AddAttack(
            "Chain Lightning",
            "Lightning that arcs between nearby enemies.",
            Skill.Spellcasting,
            2,
            4,
            2,
            TargetType.Aoe,
            3f,
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
                TargetType.Aoe,
                4f,
                DamageType.Poison,
                8
            )
            .AddCondition(ConditionType.Poisoned, 3, 3f);
        builder
            .AddAttack(
                "Arcane Blast",
                "A concussive burst of raw magic that silences the target.",
                Skill.Spellcasting,
                3,
                4,
                1,
                TargetType.Single,
                null,
                DamageType.Magic,
                18
            )
            .AddCondition(ConditionType.Silenced, 1);
        builder
            .AddAttack(
                "Blizzard",
                "A freezing storm that chills all nearby enemies.",
                Skill.Spellcasting,
                4,
                5,
                3,
                TargetType.Aoe,
                5f,
                DamageType.Ice,
                14
            )
            .AddCondition(ConditionType.Frozen, 1)
            .Requires(frostBolt);
        builder
            .AddAttack(
                "Inferno",
                "A massive eruption of flame that engulfs a wide area.",
                Skill.Spellcasting,
                4,
                6,
                4,
                TargetType.Aoe,
                6f,
                DamageType.Fire,
                20
            )
            .AddCondition(ConditionType.Burning, 2, 4f)
            .Requires(fireball);
        builder
            .AddAttack(
                "Thunderstorm",
                "A violent storm that electrocutes all enemies in the area.",
                Skill.Spellcasting,
                6,
                7,
                5,
                TargetType.Aoe,
                8f,
                DamageType.Lightning,
                25
            )
            .AddCondition(ConditionType.Stunned, 1)
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
                TargetType.Single,
                null,
                DamageType.Ice,
                22
            )
            .AddCondition(ConditionType.Frozen, 1);
        builder
            .AddAttack(
                "Scorch",
                "A searing beam that ignites the target.",
                Skill.Spellcasting,
                25,
                6,
                4,
                TargetType.Single,
                null,
                DamageType.Fire,
                24
            )
            .AddCondition(ConditionType.Burning, 2, 4f);
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
                TargetType.Single,
                null,
                DamageType.Ice,
                32
            )
            .AddCondition(ConditionType.Frozen, 2);
        builder
            .AddAttack(
                "Meteor",
                "A blazing rock falls from the heavens.",
                Skill.Spellcasting,
                45,
                12,
                8,
                TargetType.Aoe,
                4f,
                DamageType.Fire,
                28
            )
            .AddCondition(ConditionType.Burning, 2, 3f);
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
                TargetType.Aoe,
                6f,
                DamageType.Ice,
                22
            )
            .AddCondition(ConditionType.Frozen, 2);
        builder
            .AddAttack(
                "Ball Lightning",
                "A rolling sphere of crackling lightning.",
                Skill.Spellcasting,
                65,
                14,
                9,
                TargetType.Aoe,
                5f,
                DamageType.Lightning,
                35
            )
            .AddCondition(ConditionType.Stunned, 1);
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
                TargetType.Aoe,
                10f,
                DamageType.Fire,
                55
            )
            .AddCondition(ConditionType.Burning, 3, 5f);
        builder
            .AddAttack(
                "Void Bolt",
                "A bolt of pure arcane entropy that unmakes what it touches.",
                Skill.Spellcasting,
                100,
                25,
                20,
                TargetType.Single,
                null,
                DamageType.Magic,
                80
            )
            .AddCondition(ConditionType.Silenced, 3);
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
            TargetType.Single,
            null,
            DamageType.Physical,
            8
        );
        builder
            .AddAttack(
                "Piercing Shot",
                "An arrow that drives deep and causes bleeding.",
                Skill.Archery,
                2,
                3,
                0,
                TargetType.Single,
                null,
                DamageType.Physical,
                14
            )
            .AddCondition(ConditionType.Bleeding, 1, 3f)
            .Requires(arrowShot);
        builder
            .AddAttack(
                "Poison Arrow",
                "A barbed arrow coated with toxin.",
                Skill.Archery,
                3,
                3,
                2,
                TargetType.Single,
                null,
                DamageType.Poison,
                6
            )
            .AddCondition(ConditionType.Poisoned, 3, 3f)
            .Requires(arrowShot);
        builder
            .AddAttack(
                "Volley",
                "A spread of arrows targeting a wide area.",
                Skill.Archery,
                4,
                5,
                3,
                TargetType.Aoe,
                4f,
                DamageType.Physical,
                10
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
                TargetType.Single,
                null,
                DamageType.Physical,
                15
            )
            .AddCondition(ConditionType.Snared, 3);
        builder.AddAttack(
            "Multishot",
            "Release a spread of arrows simultaneously.",
            Skill.Archery,
            25,
            7,
            5,
            TargetType.Aoe,
            3f,
            DamageType.Physical,
            12
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
            TargetType.Aoe,
            5f,
            DamageType.Physical,
            10
        );
        builder
            .AddAttack(
                "Venom Strike",
                "A deeply embedded arrow laced with deadly toxin.",
                Skill.Archery,
                45,
                8,
                6,
                TargetType.Single,
                null,
                DamageType.Poison,
                14
            )
            .AddCondition(ConditionType.Poisoned, 4, 6f);
        builder.AddPrerequisiteByName("Barrage", "Multishot");
        builder.AddPrerequisiteByName("Venom Strike", "Poison Arrow");

        builder.AddAttack(
            "Rain of Arrows",
            "A devastating blanket of arrows covering a vast area.",
            Skill.Archery,
            60,
            14,
            9,
            TargetType.Aoe,
            8f,
            DamageType.Physical,
            16
        );
        builder
            .AddAttack(
                "Pinning Shot",
                "An arrow that nails the target in place.",
                Skill.Archery,
                65,
                10,
                8,
                TargetType.Single,
                null,
                DamageType.Physical,
                25
            )
            .AddCondition(ConditionType.Snared, 4);
        builder.AddPrerequisiteByName("Rain of Arrows", "Barrage");
        builder.AddPrerequisiteByName("Pinning Shot", "Crippling Arrow");

        builder.AddAttack(
            "Arrow Storm",
            "An incomprehensible density of arrows raining from the sky.",
            Skill.Archery,
            90,
            20,
            14,
            TargetType.Aoe,
            10f,
            DamageType.Physical,
            35
        );
        builder.AddAttack(
            "Death from Afar",
            "A single perfect shot from extreme range.",
            Skill.Archery,
            95,
            22,
            16,
            TargetType.Single,
            null,
            DamageType.Physical,
            65
        );
        builder.AddPrerequisiteByName("Arrow Storm", "Rain of Arrows");
        builder.AddPrerequisiteByName("Death from Afar", "Pinning Shot");
    }

    private static void AddDevotionAbilities(AbilityBuilder builder)
    {
        var mend = builder
            .AddSupport(
                "Mend",
                "Restores a portion of an ally's health.",
                Skill.Devotion,
                1,
                2,
                0,
                TargetType.Single,
                null,
                null
            )
            .AddModifier(AttributeName.Hp, 15);
        var divineShield = builder
            .AddSupport(
                "Divine Shield",
                "Fortifies an ally with a magical barrier.",
                Skill.Devotion,
                2,
                3,
                3,
                TargetType.Single,
                null,
                2
            )
            .AddModifier(AttributeName.PhysicalResistance, 20)
            .Requires(mend);
        var regenerate = builder
            .AddSupport(
                "Regenerate",
                "Grants an ally health regeneration over time.",
                Skill.Devotion,
                2,
                3,
                3,
                TargetType.Single,
                null,
                3
            )
            .AddModifier(AttributeName.Hp, 8)
            .Requires(mend);
        builder
            .AddSupport(
                "Aura of Protection",
                "Grants nearby allies increased physical resistance.",
                Skill.Devotion,
                4,
                5,
                5,
                TargetType.Aoe,
                5f,
                3
            )
            .AddModifier(AttributeName.PhysicalResistance, 15)
            .Requires(divineShield);
        builder
            .AddSupport(
                "Mass Heal",
                "Restores health to all nearby allies.",
                Skill.Devotion,
                5,
                5,
                4,
                TargetType.Aoe,
                6f,
                null
            )
            .AddModifier(AttributeName.Hp, 12)
            .Requires(regenerate);
    }

    private static void AddDevotionAdvancedAbilities(AbilityBuilder builder)
    {
        builder
            .AddSupport(
                "Greater Mend",
                "A stronger healing touch that restores more vitality.",
                Skill.Devotion,
                20,
                6,
                4,
                TargetType.Single,
                null,
                null
            )
            .AddModifier(AttributeName.Hp, 28);
        builder
            .AddSupport(
                "Fortify",
                "Strengthen an ally's defenses significantly.",
                Skill.Devotion,
                25,
                6,
                5,
                TargetType.Single,
                null,
                3
            )
            .AddModifier(AttributeName.Defense, 15)
            .AddModifier(AttributeName.Endurance, 8);
        builder.AddPrerequisiteByName("Greater Mend", "Mend");
        builder.AddPrerequisiteByName("Fortify", "Divine Shield");

        builder
            .AddSupport(
                "Sacred Ground",
                "Consecrate an area that heals allies who stand within.",
                Skill.Devotion,
                40,
                10,
                7,
                TargetType.Aoe,
                5f,
                3
            )
            .AddModifier(AttributeName.Hp, 12);
        builder
            .AddSupport(
                "Barrier",
                "Wrap an ally in a powerful magical shield.",
                Skill.Devotion,
                45,
                10,
                8,
                TargetType.Single,
                null,
                3
            )
            .AddModifier(AttributeName.PhysicalResistance, 30)
            .AddModifier(AttributeName.MagicResistance, 20);
        builder.AddPrerequisiteByName("Sacred Ground", "Mass Heal");
        builder.AddPrerequisiteByName("Barrier", "Fortify");

        builder
            .AddSupport(
                "Resurrection Pulse",
                "A wave of healing energy that restores all nearby allies.",
                Skill.Devotion,
                60,
                15,
                10,
                TargetType.Aoe,
                6f,
                4
            )
            .AddModifier(AttributeName.Hp, 15);
        builder
            .AddSupport(
                "Aegis",
                "Envelop an ally in an impenetrable divine shell.",
                Skill.Devotion,
                65,
                14,
                9,
                TargetType.Single,
                null,
                2
            )
            .AddModifier(AttributeName.PhysicalResistance, 50);
        builder.AddPrerequisiteByName("Resurrection Pulse", "Sacred Ground");
        builder.AddPrerequisiteByName("Aegis", "Barrier");

        builder
            .AddSupport(
                "Divine Intervention",
                "Call upon divine power to massively restore a single ally.",
                Skill.Devotion,
                90,
                22,
                15,
                TargetType.Single,
                null,
                null
            )
            .AddModifier(AttributeName.Hp, 70);
        builder
            .AddSupport(
                "Sanctuary",
                "Create a holy sanctum that shields all within from harm.",
                Skill.Devotion,
                95,
                22,
                16,
                TargetType.Aoe,
                8f,
                4
            )
            .AddModifier(AttributeName.PhysicalResistance, 40)
            .AddModifier(AttributeName.MagicResistance, 40);
        builder.AddPrerequisiteByName("Divine Intervention", "Greater Mend");
        builder.AddPrerequisiteByName("Sanctuary", "Aegis");
    }

    private static void AddWarfareAbilities(AbilityBuilder builder)
    {
        var battleStance = builder
            .AddSupport(
                "Battle Stance",
                "Braces the caster for combat, increasing strength.",
                Skill.Warfare,
                1,
                1,
                0,
                TargetType.Self,
                null,
                3
            )
            .AddModifier(AttributeName.Strength, 5);
        var arcaneInfusion = builder
            .AddSupport(
                "Arcane Infusion",
                "Infuses an ally with arcane energy, boosting intelligence.",
                Skill.Warfare,
                1,
                2,
                2,
                TargetType.Single,
                null,
                3
            )
            .AddModifier(AttributeName.Intelligence, 10);
        builder
            .AddSupport(
                "Iron Will",
                "Hardens the caster's resolve against physical harm.",
                Skill.Warfare,
                2,
                2,
                3,
                TargetType.Self,
                null,
                2
            )
            .AddModifier(AttributeName.Defense, 10)
            .AddModifier(AttributeName.Endurance, 10)
            .Requires(battleStance);
        builder
            .AddSupport(
                "Haste",
                "Accelerates an ally's movements and reflexes.",
                Skill.Warfare,
                2,
                2,
                3,
                TargetType.Single,
                null,
                2
            )
            .AddModifier(AttributeName.Dexterity, 5)
            .AddModifier(AttributeName.MovementSpeed, 3);
        builder
            .AddSupport(
                "Rally",
                "A battle cry that strengthens nearby allies.",
                Skill.Warfare,
                3,
                3,
                4,
                TargetType.Aoe,
                5f,
                2
            )
            .AddModifier(AttributeName.Strength, 8)
            .Requires(battleStance);
        builder
            .AddSupport(
                "Spell Ward",
                "Erects a magical barrier against incoming spells.",
                Skill.Warfare,
                3,
                3,
                4,
                TargetType.Self,
                null,
                3
            )
            .AddModifier(AttributeName.MagicResistance, 20)
            .Requires(arcaneInfusion);
    }

    private static void AddWarfareAdvancedAbilities(AbilityBuilder builder)
    {
        builder
            .AddSupport(
                "War Cry",
                "A thunderous battle cry that strengthens all nearby allies.",
                Skill.Warfare,
                20,
                6,
                5,
                TargetType.Aoe,
                5f,
                3
            )
            .AddModifier(AttributeName.Strength, 10)
            .AddModifier(AttributeName.Defense, 5);
        builder
            .AddSupport(
                "Mystic Focus",
                "Channel arcane energy for enhanced spellcasting.",
                Skill.Warfare,
                25,
                6,
                5,
                TargetType.Self,
                null,
                3
            )
            .AddModifier(AttributeName.Intelligence, 15)
            .AddModifier(AttributeName.Mana, 10);
        builder.AddPrerequisiteByName("War Cry", "Rally");
        builder.AddPrerequisiteByName("Mystic Focus", "Arcane Infusion");

        builder
            .AddSupport(
                "Berserker Stance",
                "Abandon defense for unbridled offensive power.",
                Skill.Warfare,
                40,
                8,
                6,
                TargetType.Self,
                null,
                3
            )
            .AddModifier(AttributeName.Strength, 20)
            .AddModifier(AttributeName.Dexterity, 10);
        builder
            .AddSupport(
                "Arcane Shield",
                "A barrier of raw magic that absorbs incoming spells.",
                Skill.Warfare,
                45,
                8,
                7,
                TargetType.Self,
                null,
                3
            )
            .AddModifier(AttributeName.MagicResistance, 35);
        builder.AddPrerequisiteByName("Berserker Stance", "Battle Stance");
        builder.AddPrerequisiteByName("Arcane Shield", "Spell Ward");

        builder
            .AddSupport(
                "Warlord's Presence",
                "Your commanding presence bolsters all nearby allies.",
                Skill.Warfare,
                60,
                14,
                9,
                TargetType.Aoe,
                6f,
                3
            )
            .AddModifier(AttributeName.Strength, 15)
            .AddModifier(AttributeName.Defense, 15)
            .AddModifier(AttributeName.Endurance, 10);
        builder
            .AddSupport(
                "Time Warp",
                "Briefly accelerate an ally far beyond their natural speed.",
                Skill.Warfare,
                65,
                12,
                9,
                TargetType.Single,
                null,
                2
            )
            .AddModifier(AttributeName.Dexterity, 25)
            .AddModifier(AttributeName.MovementSpeed, 8);
        builder.AddPrerequisiteByName("Warlord's Presence", "War Cry");
        builder.AddPrerequisiteByName("Time Warp", "Haste");

        builder
            .AddSupport(
                "Invincible",
                "For a brief moment, nothing can stop you.",
                Skill.Warfare,
                90,
                20,
                14,
                TargetType.Self,
                null,
                2
            )
            .AddModifier(AttributeName.PhysicalResistance, 50)
            .AddModifier(AttributeName.MagicResistance, 50)
            .AddModifier(AttributeName.Endurance, 20);
        builder
            .AddSupport(
                "Legion Might",
                "A war cry of legendary power that emboldens all nearby.",
                Skill.Warfare,
                95,
                22,
                15,
                TargetType.Aoe,
                8f,
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
            TargetType targetType,
            float? aoeRadius,
            DamageType damageType,
            float damageAmount
        )
        {
            var attack = new AttackAbility
            {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                Cost = cost,
                Cooldown = cooldown,
                TargetType = targetType,
                AoeRadius = aoeRadius,
                DamageType = damageType,
                DamageAmountType = AmountType.Flat,
                DamageAmount = damageAmount,
            };
            ByName[name] = attack;
            return new AttackAbilityEntry(attack, this);
        }

        public SupportAbilityEntry AddSupport(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int cost,
            int cooldown,
            TargetType targetType,
            float? aoeRadius,
            int? duration
        )
        {
            var support = new SupportAbility
            {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                Cost = cost,
                Cooldown = cooldown,
                TargetType = targetType,
                AoeRadius = aoeRadius,
                Duration = duration,
            };
            ByName[name] = support;
            return new SupportAbilityEntry(support, this);
        }

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
        public AttackAbilityEntry AddCondition(
            ConditionType condition,
            int duration,
            float? amount = null
        )
        {
            attack.Conditions.Add(
                new ConditionEffect
                {
                    Condition = condition,
                    Duration = duration,
                    Amount = amount,
                    Type = amount.HasValue ? AmountType.Flat : null,
                }
            );
            return this;
        }

        public AttackAbilityEntry Requires(AbilityEntry prereq)
        {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }

    private class SupportAbilityEntry(SupportAbility support, AbilityBuilder owner)
        : AbilityEntry(support, owner)
    {
        public SupportAbilityEntry AddModifier(AttributeName attribute, float amount)
        {
            support.Modifiers.Add(
                new AttributeModifier
                {
                    Attribute = attribute,
                    Type = AmountType.Flat,
                    Amount = amount,
                }
            );
            return this;
        }

        public SupportAbilityEntry Requires(AbilityEntry prereq)
        {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }
}
