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
            Skill = Skill.General,
            ApCost = 0,
            Cooldown = 0,
            TargetType = AttackTargetType.Single,
            DamageType = DamageType.Physical,
            DamageAmount = 100,
            DamageAmountType = AmountType.Percent,
        };

    // Registered by AddBlockingAbilities as a normal learnable ability (Skill.Blocking); this
    // property just exposes the same instance by name for call sites that need "the" Block
    // ability directly rather than looking it up.
    public BuffAbility BlockStance => (BuffAbility)byName["Block"];

    public static AbilityDefinitions Create()
    {
        var builder = new AbilityBuilder();
        AddMeleeAbilities(builder);
        AddMeleeAdvancedAbilities(builder);
        AddBlockingAbilities(builder);
        AddSneakAbilities(builder);
        AddSneakAdvancedAbilities(builder);
        AddDestructionAbilities(builder);
        AddDestructionAdvancedAbilities(builder);
        AddIllusionAbilities(builder);
        AddArcheryAbilities(builder);
        AddArcheryAdvancedAbilities(builder);
        AddRestorationAbilities(builder);
        AddRestorationAdvancedAbilities(builder);
        AddAlterationAbilities(builder);
        return new AbilityDefinitions(builder.ByName, builder.Prerequisites);
    }

    public string RandomAttackAbility()
    {
        var attacks = Abilities.OfType<AttackAbility>().ToList();
        return attacks[Random.Shared.Next(attacks.Count)].Name;
    }

    public Ability? GetByName(string name) => byName.GetValueOrDefault(name);

    private static void AddMeleeAbilities(AbilityBuilder builder)
    {
        var battleStance = builder
            .AddBuff(
                "Battle Stance",
                "Braces the caster for combat, increasing strength.",
                Skill.Melee,
                1,
                1,
                12,
                TargetType.Self,
                10
            )
            .AddModifier(AttributeName.Strength, 5);
        var slash = builder.AddAttack(
            "Slash",
            "A basic melee strike.",
            Skill.Melee,
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
                Skill.Melee,
                2,
                3,
                0,
                AttackTargetType.Aoe,
                DamageType.Physical,
                170
            )
            .Requires(slash);
        builder
            .AddBuff(
                "Rally",
                "A battle cry that strengthens nearby allies.",
                Skill.Melee,
                3,
                3,
                13,
                TargetType.Aoe,
                11
            )
            .AddModifier(AttributeName.Strength, 8)
            .Requires(battleStance);
        builder
            .AddAttack(
                "Devastate",
                "A crippling strike that leaves the target bleeding.",
                Skill.Melee,
                4,
                4,
                3,
                AttackTargetType.Single,
                DamageType.Physical,
                185
            )
            .AddDot(2, 3f)
            .AddStatus(ConditionType.Bleeding, 2)
            .Requires(cleave);
        builder
            .AddAttack(
                "Whirlwind",
                "A spinning strike that damages all surrounding enemies.",
                Skill.Melee,
                5,
                5,
                3,
                AttackTargetType.Aoe,
                DamageType.Physical,
                210
            )
            .Requires(cleave);
    }

    private static void AddMeleeAdvancedAbilities(AbilityBuilder builder)
    {
        builder.AddAttack(
            "Execute",
            "A powerful finishing blow aimed at weakened foes.",
            Skill.Melee,
            20,
            6,
            4,
            AttackTargetType.Single,
            DamageType.Physical,
            245
        );
        builder.AddPrerequisiteByName("Execute", "Whirlwind");

        builder
            .AddBuff(
                "War Cry",
                "A thunderous battle cry that strengthens all nearby allies.",
                Skill.Melee,
                20,
                6,
                18,
                TargetType.Aoe,
                16
            )
            .AddModifier(AttributeName.Strength, 10)
            .AddModifier(AttributeName.Defense, 5);
        builder.AddPrerequisiteByName("War Cry", "Rally");

        builder
            .AddBuff(
                "Berserker Stance",
                "Abandon defense for unbridled offensive power.",
                Skill.Melee,
                40,
                8,
                25,
                TargetType.Self,
                23
            )
            .AddModifier(AttributeName.Strength, 20)
            .AddModifier(AttributeName.Dexterity, 10);
        builder.AddPrerequisiteByName("Berserker Stance", "Battle Stance");

        builder
            .AddAttack(
                "Mortal Strike",
                "A savage blow that opens a wound refusing to close.",
                Skill.Melee,
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
            Skill.Melee,
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
                Skill.Melee,
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
            Skill.Melee,
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
            Skill.Melee,
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
            Skill.Melee,
            95,
            22,
            15,
            AttackTargetType.Aoe,
            DamageType.Physical,
            370
        );
        builder
            .AddBuff(
                "Legion Might",
                "A war cry of legendary power that emboldens all nearby.",
                Skill.Melee,
                95,
                22,
                42,
                TargetType.Aoe,
                40
            )
            .AddModifier(AttributeName.Strength, 25)
            .AddModifier(AttributeName.Defense, 20);
        builder.AddPrerequisiteByName("Legend's Blow", "Hundred Blades");
        builder.AddPrerequisiteByName("Eternal Whirlwind", "Bladestorm");
        builder.AddPrerequisiteByName("Legion Might", "War Cry");
    }

    private static void AddBlockingAbilities(AbilityBuilder builder)
    {
        var block = builder
            .AddBuff(
                "Block",
                "Raise your guard: a shield or melee weapon lets you parry, sharply reducing "
                    + "your chance to be hit; without one, you brace instead, blunting the "
                    + "damage of blows that land.",
                Skill.Blocking,
                1,
                2,
                0,
                TargetType.Self,
                1
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.15f)
            .AddParryModifier(AttributeName.Defense, 100, AmountType.Percent);

        var ironWill = builder
            .AddBuff(
                "Iron Will",
                "Hardens the caster's resolve against physical harm.",
                Skill.Blocking,
                2,
                2,
                13,
                TargetType.Self,
                11
            )
            .AddModifier(AttributeName.Defense, 10)
            .AddModifier(AttributeName.Endurance, 10)
            .Requires(block);

        builder
            .AddAttack(
                "Shield Bash",
                "A stunning blow with the shield.",
                Skill.Blocking,
                2,
                2,
                2,
                AttackTargetType.Single,
                DamageType.Physical,
                130
            )
            .AddStatus(ConditionType.Stunned, 1)
            .Requires(block);

        builder
            .AddBuff(
                "Fortify",
                "Strengthen an ally's defenses significantly.",
                Skill.Blocking,
                25,
                6,
                20,
                TargetType.Single,
                18
            )
            .AddModifier(AttributeName.Defense, 15)
            .AddModifier(AttributeName.Endurance, 8)
            .Requires(ironWill);

        builder
            .AddAttack(
                "Riposte",
                "A swift counter-strike following a parry.",
                Skill.Blocking,
                25,
                5,
                5,
                AttackTargetType.Single,
                DamageType.Physical,
                220
            )
            .AddStatus(ConditionType.Stunned, 1);
        builder.AddPrerequisiteByName("Riposte", "Shield Bash");
    }

    private static void AddSneakAbilities(AbilityBuilder builder)
    {
        var stab = builder.AddAttack(
            "Stab",
            "A quick jab at a vital point.",
            Skill.Sneak,
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
                Skill.Sneak,
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
                Skill.Sneak,
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
            .AddBuff(
                "Haste",
                "Accelerates an ally's movements and reflexes.",
                Skill.Sneak,
                2,
                2,
                13,
                TargetType.Single,
                11
            )
            .AddModifier(AttributeName.Dexterity, 5)
            .AddModifier(AttributeName.MovementSpeed, 3);
        builder
            .AddAttack(
                "Hemorrhage",
                "A deep wound that causes severe bleeding.",
                Skill.Sneak,
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
                "Disarm",
                "A precise strike to the wrist that knocks the weapon from the target's grip.",
                Skill.Sneak,
                4,
                3,
                3,
                AttackTargetType.Single,
                DamageType.Physical,
                120
            )
            .AddStatus(ConditionType.Disarmed, 3)
            .Requires(hamstring);
        builder
            .AddAttack(
                "Shadowstep Strike",
                "A gap-closing attack that blinds the target.",
                Skill.Sneak,
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

    private static void AddSneakAdvancedAbilities(AbilityBuilder builder)
    {
        builder
            .AddAttack(
                "Kidney Shot",
                "A blow to vital organs that leaves the target winded.",
                Skill.Sneak,
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
                Skill.Sneak,
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
                Skill.Sneak,
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
                Skill.Sneak,
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
                Skill.Sneak,
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
                Skill.Sneak,
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
            Skill.Sneak,
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
                Skill.Sneak,
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

    private static void AddDestructionAbilities(AbilityBuilder builder)
    {
        var frostBolt = builder.AddAttack(
            "Frost Bolt",
            "A bolt of ice that damages the target.",
            Skill.Destruction,
            1,
            3,
            0,
            AttackTargetType.Single,
            DamageType.Ice,
            10
        );
        var fireball = builder
            .AddAttack(
                "Fireball",
                "A ball of fire hurled at a single target.",
                Skill.Destruction,
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
            Skill.Destruction,
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
                Skill.Destruction,
                2,
                3,
                2,
                AttackTargetType.Aoe,
                DamageType.Poison,
                8
            )
            .AddDot(3, 3f)
            .AddStatus(ConditionType.Poisoned, 3);
        builder.AddAttack(
            "Arcane Blast",
            "A concussive burst of raw magic.",
            Skill.Destruction,
            3,
            4,
            1,
            AttackTargetType.Single,
            DamageType.Magic,
            18
        );
        builder
            .AddAttack(
                "Blizzard",
                "A freezing storm that chills all nearby enemies.",
                Skill.Destruction,
                4,
                5,
                3,
                AttackTargetType.Aoe,
                DamageType.Ice,
                14
            )
            .Requires(frostBolt);
        builder
            .AddAttack(
                "Inferno",
                "A massive eruption of flame that engulfs a wide area.",
                Skill.Destruction,
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
                Skill.Destruction,
                6,
                7,
                5,
                AttackTargetType.Aoe,
                DamageType.Lightning,
                25
            )
            .Requires(chainLightning);
    }

    private static void AddDestructionAdvancedAbilities(AbilityBuilder builder)
    {
        builder.AddAttack(
            "Ice Lance",
            "A piercing lance of concentrated ice.",
            Skill.Destruction,
            20,
            6,
            3,
            AttackTargetType.Single,
            DamageType.Ice,
            22
        );
        builder
            .AddAttack(
                "Scorch",
                "A searing beam that ignites the target.",
                Skill.Destruction,
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
                Skill.Destruction,
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
                Skill.Destruction,
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
                Skill.Destruction,
                60,
                15,
                10,
                AttackTargetType.Aoe,
                DamageType.Ice,
                22
            )
            .AddStatus(ConditionType.Frozen, 2);
        builder.AddAttack(
            "Ball Lightning",
            "A rolling sphere of crackling lightning.",
            Skill.Destruction,
            65,
            14,
            9,
            AttackTargetType.Aoe,
            DamageType.Lightning,
            35
        );
        builder.AddPrerequisiteByName("Absolute Zero", "Glacial Spike");
        builder.AddPrerequisiteByName("Ball Lightning", "Thunderstorm");

        builder
            .AddAttack(
                "Armageddon",
                "A cataclysmic firestorm that consumes everything.",
                Skill.Destruction,
                90,
                22,
                15,
                AttackTargetType.Aoe,
                DamageType.Fire,
                55
            )
            .AddDot(3, 5f)
            .AddStatus(ConditionType.Burning, 3);
        builder.AddAttack(
            "Void Bolt",
            "A bolt of pure arcane entropy that unmakes what it touches.",
            Skill.Destruction,
            100,
            25,
            20,
            AttackTargetType.Single,
            DamageType.Magic,
            80
        );
        builder.AddPrerequisiteByName("Armageddon", "Meteor");
        builder.AddPrerequisiteByName("Void Bolt", "Arcane Blast");
    }

    private static void AddIllusionAbilities(AbilityBuilder builder)
    {
        var fear = builder
            .AddAttack(
                "Fear",
                "A whisper of dread that roots the target in place.",
                Skill.Illusion,
                1,
                3,
                2,
                AttackTargetType.Single,
                DamageType.Magic,
                14
            )
            .AddStatus(ConditionType.Snared, 2);
        var maddeningWhispers = builder
            .AddAttack(
                "Maddening Whispers",
                "Unrelenting voices that drown out the target's own thoughts.",
                Skill.Illusion,
                20,
                6,
                5,
                AttackTargetType.Single,
                DamageType.Magic,
                24
            )
            .AddStatus(ConditionType.Silenced, 2)
            .Requires(fear);
        var paralyze = builder
            .AddAttack(
                "Paralyze",
                "Locks the target's muscles in place with pure dread.",
                Skill.Illusion,
                45,
                10,
                7,
                AttackTargetType.Single,
                DamageType.Magic,
                30
            )
            .AddStatus(ConditionType.Stunned, 2)
            .Requires(maddeningWhispers);
        builder
            .AddAttack(
                "Overwhelming Terror",
                "A wave of primal horror that freezes all who feel it.",
                Skill.Illusion,
                65,
                14,
                9,
                AttackTargetType.Aoe,
                DamageType.Magic,
                28
            )
            .AddStatus(ConditionType.Stunned, 2)
            .Requires(paralyze);
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
            .AddBuff(
                "Quickdraw",
                "Draw and loose with practiced speed, sharpening the caster's reflexes.",
                Skill.Archery,
                3,
                3,
                13,
                TargetType.Self,
                11
            )
            .AddModifier(AttributeName.Dexterity, 5)
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
        builder
            .AddAttack(
                "Concussive Shot",
                "A blunt-tipped arrow driven hard enough to daze the target.",
                Skill.Archery,
                25,
                5,
                5,
                AttackTargetType.Single,
                DamageType.Physical,
                190
            )
            .AddStatus(ConditionType.Stunned, 2);
        builder.AddPrerequisiteByName("Crippling Arrow", "Piercing Shot");
        builder.AddPrerequisiteByName("Multishot", "Volley");
        builder.AddPrerequisiteByName("Concussive Shot", "Crippling Arrow");

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

    private static void AddRestorationAbilities(AbilityBuilder builder)
    {
        var mend = builder.AddInstantHeal(
            "Mend",
            "Restores a portion of an ally's health.",
            Skill.Restoration,
            1,
            2,
            0,
            TargetType.Single,
            15
        );
        builder
            .AddAttack(
                "Silencing Word",
                "A sharp rebuke of divine power that silences the target.",
                Skill.Restoration,
                2,
                3,
                2,
                AttackTargetType.Single,
                DamageType.Magic,
                8
            )
            .AddStatus(ConditionType.Silenced, 2);
        var regenerate = builder
            .AddHealOverTime(
                "Regenerate",
                "Grants an ally health regeneration over time.",
                Skill.Restoration,
                2,
                3,
                3,
                TargetType.Single,
                8,
                3
            )
            .Requires(mend);
        builder.AddAttack(
            "Smite",
            "A bolt of divine judgment.",
            Skill.Restoration,
            3,
            4,
            1,
            AttackTargetType.Single,
            DamageType.Magic,
            16
        );
        builder
            .AddInstantHeal(
                "Mass Heal",
                "Restores health to all nearby allies.",
                Skill.Restoration,
                5,
                5,
                4,
                TargetType.Aoe,
                12
            )
            .Requires(regenerate);
    }

    private static void AddRestorationAdvancedAbilities(AbilityBuilder builder)
    {
        builder.AddInstantHeal(
            "Greater Mend",
            "A stronger healing touch that restores more vitality.",
            Skill.Restoration,
            20,
            6,
            4,
            TargetType.Single,
            28
        );
        builder.AddPrerequisiteByName("Greater Mend", "Mend");

        builder
            .AddAttack(
                "Turn Undead",
                "Divine power that sears the undead and staggers all else.",
                Skill.Restoration,
                20,
                6,
                4,
                AttackTargetType.Single,
                DamageType.Magic,
                15
            )
            .AddStatus(ConditionType.Stunned, 1)
            .AddCreatureTypeBonus(CreatureType.Undead, 3f);
        builder.AddPrerequisiteByName("Turn Undead", "Smite");

        builder.AddAttack(
            "Radiant Burst",
            "An expanding sphere of holy light.",
            Skill.Restoration,
            25,
            7,
            5,
            AttackTargetType.Aoe,
            DamageType.Magic,
            20
        );
        builder.AddPrerequisiteByName("Radiant Burst", "Turn Undead");

        builder.AddHealOverTime(
            "Sacred Ground",
            "Consecrate an area that heals allies who stand within.",
            Skill.Restoration,
            40,
            10,
            7,
            TargetType.Aoe,
            12,
            3
        );
        builder.AddPrerequisiteByName("Sacred Ground", "Mass Heal");

        builder.AddHealOverTime(
            "Resurrection Pulse",
            "A wave of healing energy that restores all nearby allies.",
            Skill.Restoration,
            60,
            15,
            10,
            TargetType.Aoe,
            15,
            4
        );
        builder.AddPrerequisiteByName("Resurrection Pulse", "Sacred Ground");

        builder
            .AddAttack(
                "Destroy Undead",
                "Divine annihilation that unmakes the undead and shakes all else.",
                Skill.Restoration,
                60,
                14,
                9,
                AttackTargetType.Single,
                DamageType.Magic,
                28
            )
            .AddStatus(ConditionType.Stunned, 2)
            .AddCreatureTypeBonus(CreatureType.Undead, 3f);
        builder.AddPrerequisiteByName("Destroy Undead", "Radiant Burst");

        builder.AddAttack(
            "Wrath of the Divine",
            "An unrelenting torrent of divine judgment.",
            Skill.Restoration,
            65,
            15,
            10,
            AttackTargetType.Single,
            DamageType.Magic,
            45
        );
        builder.AddPrerequisiteByName("Wrath of the Divine", "Destroy Undead");

        builder.AddInstantHeal(
            "Divine Intervention",
            "Call upon divine power to massively restore a single ally.",
            Skill.Restoration,
            90,
            22,
            15,
            TargetType.Single,
            70
        );
        builder.AddPrerequisiteByName("Divine Intervention", "Greater Mend");
    }

    private static void AddAlterationAbilities(AbilityBuilder builder)
    {
        var arcaneInfusion = builder
            .AddBuff(
                "Arcane Infusion",
                "Infuses an ally with arcane energy, boosting intelligence.",
                Skill.Alteration,
                1,
                2,
                12,
                TargetType.Single,
                10
            )
            .AddModifier(AttributeName.Intelligence, 10);
        builder
            .AddBuff(
                "Divine Shield",
                "Fortifies an ally with a magical barrier.",
                Skill.Alteration,
                2,
                3,
                13,
                TargetType.Single,
                11
            )
            .AddModifier(AttributeName.PhysicalResistance, 0.2f);
        builder
            .AddBuff(
                "Spell Ward",
                "Erects a magical barrier against incoming spells.",
                Skill.Alteration,
                3,
                3,
                13,
                TargetType.Self,
                11
            )
            .AddModifier(AttributeName.MagicResistance, 0.2f)
            .Requires(arcaneInfusion);
        builder
            .AddBuff(
                "Mystic Focus",
                "Channel arcane energy for enhanced spellcasting.",
                Skill.Alteration,
                25,
                6,
                20,
                TargetType.Self,
                18
            )
            .AddModifier(AttributeName.Intelligence, 15)
            .AddModifier(AttributeName.Mana, 10)
            .Requires(arcaneInfusion);
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
            float damageAmount,
            AmountType? damageAmountTypeOverride = null
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
                // Physical damage defaults to a percentage of weapon damage (see
                // DamageCalculator.CalculatePhysicalRawDamage); every other damage type defaults
                // to a flat amount. A natural-weapon attack (no real weapon behind it, e.g. Claw)
                // overrides this to Flat so it adds its own damage on top of the unarmed roll
                // instead of taking a percentage of it.
                DamageAmountType =
                    damageAmountTypeOverride
                    ?? (damageType == DamageType.Physical ? AmountType.Percent : AmountType.Flat),
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
            skill is Skill.Destruction or Skill.Illusion or Skill.Restoration
                ? (0, cost)
                : (cost, 0);

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

        public AttackAbilityEntry AddCreatureTypeBonus(CreatureType creatureType, float multiplier)
        {
            attack.BonusTargetCreatureType = creatureType;
            attack.BonusDamageMultiplier = multiplier;
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
        public BuffAbilityEntry AddModifier(
            AttributeName attribute,
            float amount,
            AmountType amountType = AmountType.Flat
        )
        {
            buff.Modifiers.Add(
                new AttributeModifier
                {
                    Attribute = attribute,
                    AmountType = amountType,
                    Amount = amount,
                }
            );
            return this;
        }

        public BuffAbilityEntry AddParryModifier(
            AttributeName attribute,
            float amount,
            AmountType amountType = AmountType.Flat
        )
        {
            buff.ParryCapableModifiers.Add(
                new AttributeModifier
                {
                    Attribute = attribute,
                    AmountType = amountType,
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
