using TRPG.Data.Models;

namespace TRPG.Application.Abilities;

public static class AbilityCatalog
{
    public static AttackAbility Strike { get; } =
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

    public static GuardStanceAbility Block => Catalog.Block;

    public static IEnumerable<Ability> Abilities => Catalog.ByName.Values;

    private static readonly CatalogResult Catalog = BuildCatalog();

    private sealed record CatalogResult(
        Dictionary<string, Ability> ByName,
        GuardStanceAbility Block
    );

    private static CatalogResult BuildCatalog()
    {
        var builder = new AbilityBuilder();
        AddMeleeAbilities(builder);
        var block = AddBlockingAbilities(builder);
        AddSneakAbilities(builder);
        AddDestructionAbilities(builder);
        AddIllusionAbilities(builder);
        AddArcheryAbilities(builder);
        AddRestorationAbilities(builder);
        AddAlterationAbilities(builder);
        return new CatalogResult(builder.ByName, block);
    }

    private static void AddMeleeAbilities(AbilityBuilder builder)
    {
        var battleStance = builder
            .AddBuff(
                "Battle Stance",
                "Braces the caster for combat, increasing strength.",
                Skill.Melee,
                1,
                1,
                0,
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
            0,
            AttackTargetType.Single,
            DamageType.Physical,
            150,
            AmountType.Percent,
            gearRequirement: GearRequirement.MeleeWeapon
        );
        var cleave = builder
            .AddAttack(
                "Cleave",
                "A wide swing that hits nearby enemies.",
                Skill.Melee,
                2,
                3,
                0,
                0,
                AttackTargetType.Aoe,
                DamageType.Physical,
                170,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .Requires(slash);
        var rally = builder
            .AddBuff(
                "Rally",
                "A battle cry that strengthens nearby allies.",
                Skill.Melee,
                3,
                3,
                0,
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
                0,
                3,
                AttackTargetType.Single,
                DamageType.Physical,
                185,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .AddDot(2, 3f)
            .AddStatus(ConditionType.Bleeding, 2)
            .Requires(cleave);
        var whirlwind = builder
            .AddAttack(
                "Whirlwind",
                "A spinning strike that damages all surrounding enemies.",
                Skill.Melee,
                5,
                5,
                0,
                3,
                AttackTargetType.Aoe,
                DamageType.Physical,
                210,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .Requires(cleave);

        var execute = builder
            .AddAttack(
                "Execute",
                "A powerful finishing blow aimed at weakened foes.",
                Skill.Melee,
                20,
                6,
                0,
                4,
                AttackTargetType.Single,
                DamageType.Physical,
                245,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .Requires(whirlwind);

        var warCry = builder
            .AddBuff(
                "War Cry",
                "A thunderous battle cry that strengthens all nearby allies.",
                Skill.Melee,
                20,
                6,
                0,
                18,
                TargetType.Aoe,
                16
            )
            .AddModifier(AttributeName.Strength, 10)
            .AddModifier(AttributeName.Defense, 5)
            .Requires(rally);

        builder
            .AddBuff(
                "Berserker Stance",
                "Abandon defense for unbridled offensive power.",
                Skill.Melee,
                40,
                8,
                0,
                25,
                TargetType.Self,
                23
            )
            .AddModifier(AttributeName.Strength, 20)
            .AddModifier(AttributeName.Dexterity, 10)
            .Requires(battleStance);

        var mortalStrike = builder
            .AddAttack(
                "Mortal Strike",
                "A savage blow that opens a wound refusing to close.",
                Skill.Melee,
                40,
                8,
                0,
                6,
                AttackTargetType.Single,
                DamageType.Physical,
                230,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .AddDot(3, 5f)
            .AddStatus(ConditionType.Bleeding, 3)
            .Requires(execute);

        var bladestorm = builder
            .AddAttack(
                "Bladestorm",
                "A whirling tempest of steel that strikes all nearby foes.",
                Skill.Melee,
                45,
                10,
                0,
                7,
                AttackTargetType.Aoe,
                DamageType.Physical,
                210,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .Requires(whirlwind);

        var skullSplitter = builder
            .AddAttack(
                "Skull Splitter",
                "A bone-shattering overhead blow that leaves the target reeling.",
                Skill.Melee,
                60,
                12,
                0,
                8,
                AttackTargetType.Single,
                DamageType.Physical,
                290,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .AddStatus(ConditionType.Stunned, 2)
            .Requires(mortalStrike);

        var hundredBlades = builder
            .AddAttack(
                "Hundred Blades",
                "A blinding sequence of precise cuts delivered in an instant.",
                Skill.Melee,
                65,
                15,
                0,
                10,
                AttackTargetType.Single,
                DamageType.Physical,
                340,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .Requires(bladestorm);

        builder
            .AddAttack(
                "Legend's Blow",
                "A mythic strike of such force it shakes the earth.",
                Skill.Melee,
                90,
                20,
                0,
                15,
                AttackTargetType.Single,
                DamageType.Physical,
                520,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .Requires(hundredBlades);

        builder
            .AddAttack(
                "Eternal Whirlwind",
                "A cyclone of steel that never stops turning.",
                Skill.Melee,
                95,
                22,
                0,
                15,
                AttackTargetType.Aoe,
                DamageType.Physical,
                370,
                AmountType.Percent,
                gearRequirement: GearRequirement.MeleeWeapon
            )
            .Requires(bladestorm);

        builder
            .AddBuff(
                "Legion Might",
                "A war cry of legendary power that emboldens all nearby.",
                Skill.Melee,
                95,
                22,
                0,
                42,
                TargetType.Aoe,
                40
            )
            .AddModifier(AttributeName.Strength, 25)
            .AddModifier(AttributeName.Defense, 20)
            .Requires(warCry);
    }

    private static GuardStanceAbility AddBlockingAbilities(AbilityBuilder builder)
    {
        var block = builder
            .AddGuardStance(
                "Block",
                "Raise your guard: a shield or melee weapon lets you parry, sharply reducing "
                    + "your chance to be hit; without one, you brace instead, blunting the "
                    + "damage of blows that land.",
                Skill.Blocking,
                1,
                2,
                0,
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
                0,
                13,
                TargetType.Self,
                11
            )
            .AddModifier(AttributeName.Defense, 10)
            .AddModifier(AttributeName.Endurance, 10)
            .Requires(block);

        var shieldBash = builder
            .AddAttack(
                "Shield Bash",
                "A stunning blow with the shield.",
                Skill.Blocking,
                2,
                2,
                0,
                2,
                AttackTargetType.Single,
                DamageType.Physical,
                130,
                AmountType.Percent,
                gearRequirement: GearRequirement.Shield
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
                0,
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
                0,
                5,
                AttackTargetType.Single,
                DamageType.Physical,
                220,
                AmountType.Percent
            )
            .AddStatus(ConditionType.Stunned, 1)
            .Requires(shieldBash);

        return (GuardStanceAbility)block.Ability;
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
            0,
            AttackTargetType.Single,
            DamageType.Physical,
            130,
            AmountType.Percent
        );
        var backstab = builder
            .AddAttack(
                "Backstab",
                "A precision strike from an unexpected angle.",
                Skill.Sneak,
                2,
                3,
                0,
                0,
                AttackTargetType.Single,
                DamageType.Physical,
                195,
                AmountType.Percent
            )
            .Requires(stab);
        var hamstring = builder
            .AddAttack(
                "Hamstring",
                "A cut to the legs that slows the target.",
                Skill.Sneak,
                2,
                2,
                0,
                2,
                AttackTargetType.Single,
                DamageType.Physical,
                135,
                AmountType.Percent
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
                0,
                13,
                TargetType.Single,
                11
            )
            .AddModifier(AttributeName.Dexterity, 5)
            .AddModifier(AttributeName.MovementSpeed, 3);
        var hemorrhage = builder
            .AddAttack(
                "Hemorrhage",
                "A deep wound that causes severe bleeding.",
                Skill.Sneak,
                4,
                3,
                0,
                2,
                AttackTargetType.Single,
                DamageType.Physical,
                150,
                AmountType.Percent
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
                0,
                3,
                AttackTargetType.Single,
                DamageType.Physical,
                120,
                AmountType.Percent
            )
            .AddStatus(ConditionType.Disarmed, 3)
            .Requires(hamstring);
        var shadowstepStrike = builder
            .AddAttack(
                "Shadowstep Strike",
                "A gap-closing attack that blinds the target.",
                Skill.Sneak,
                5,
                4,
                0,
                3,
                AttackTargetType.Single,
                DamageType.Physical,
                220,
                AmountType.Percent
            )
            .AddStatus(ConditionType.Blinded, 1)
            .Requires(backstab);

        var kidneyShot = builder
            .AddAttack(
                "Kidney Shot",
                "A blow to vital organs that leaves the target winded.",
                Skill.Sneak,
                20,
                5,
                0,
                5,
                AttackTargetType.Single,
                DamageType.Physical,
                190,
                AmountType.Percent
            )
            .AddStatus(ConditionType.Stunned, 2)
            .Requires(backstab);
        builder
            .AddAttack(
                "Cripple",
                "A precise cut to the tendons that hinders movement.",
                Skill.Sneak,
                25,
                5,
                0,
                4,
                AttackTargetType.Single,
                DamageType.Physical,
                170,
                AmountType.Percent
            )
            .AddStatus(ConditionType.Snared, 3)
            .Requires(hamstring);

        builder
            .AddAttack(
                "Garrote",
                "A choking hold that silences the target.",
                Skill.Sneak,
                40,
                7,
                0,
                6,
                AttackTargetType.Single,
                DamageType.Physical,
                210,
                AmountType.Percent
            )
            .AddStatus(ConditionType.Silenced, 3)
            .Requires(kidneyShot);
        var markedForDeath = builder
            .AddAttack(
                "Marked for Death",
                "A cursed strike that ensures the target bleeds out.",
                Skill.Sneak,
                45,
                8,
                0,
                7,
                AttackTargetType.Single,
                DamageType.Physical,
                230,
                AmountType.Percent
            )
            .AddDot(4, 6f)
            .AddStatus(ConditionType.Bleeding, 4)
            .Requires(hemorrhage);

        var phantasm = builder
            .AddAttack(
                "Phantasm",
                "Move faster than the eye can follow, then strike from shadow.",
                Skill.Sneak,
                60,
                12,
                0,
                9,
                AttackTargetType.Single,
                DamageType.Physical,
                290,
                AmountType.Percent
            )
            .AddStatus(ConditionType.Blinded, 2)
            .Requires(shadowstepStrike);
        var deathBlossom = builder
            .AddAttack(
                "Death Blossom",
                "A flurry of cuts that leaves the target hemorrhaging.",
                Skill.Sneak,
                65,
                14,
                0,
                10,
                AttackTargetType.Single,
                DamageType.Physical,
                250,
                AmountType.Percent
            )
            .AddDot(4, 6f)
            .AddStatus(ConditionType.Bleeding, 4)
            .Requires(markedForDeath);

        builder
            .AddAttack(
                "Assassinate",
                "A perfectly placed strike at a vital point.",
                Skill.Sneak,
                90,
                20,
                0,
                15,
                AttackTargetType.Single,
                DamageType.Physical,
                490,
                AmountType.Percent
            )
            .Requires(phantasm);
        builder
            .AddAttack(
                "Hemorrhagic Frenzy",
                "A relentless barrage of bleeding cuts.",
                Skill.Sneak,
                95,
                22,
                0,
                16,
                AttackTargetType.Single,
                DamageType.Physical,
                210,
                AmountType.Percent
            )
            .AddDot(5, 8f)
            .AddStatus(ConditionType.Bleeding, 5)
            .Requires(deathBlossom);
    }

    private static void AddDestructionAbilities(AbilityBuilder builder)
    {
        var frostBolt = builder.AddAttack(
            "Frost Bolt",
            "A bolt of ice that damages the target.",
            Skill.Destruction,
            1,
            0,
            3,
            0,
            AttackTargetType.Single,
            DamageType.Ice,
            10,
            AmountType.Flat
        );
        var fireball = builder
            .AddAttack(
                "Fireball",
                "A ball of fire hurled at a single target.",
                Skill.Destruction,
                1,
                0,
                3,
                0,
                AttackTargetType.Single,
                DamageType.Fire,
                12,
                AmountType.Flat
            )
            .AddDot(1, 3f)
            .AddStatus(ConditionType.Burning, 1);
        var chainLightning = builder.AddAttack(
            "Chain Lightning",
            "Lightning that arcs between nearby enemies.",
            Skill.Destruction,
            2,
            0,
            4,
            2,
            AttackTargetType.Aoe,
            DamageType.Lightning,
            15,
            AmountType.Flat
        );
        builder
            .AddAttack(
                "Poison Cloud",
                "A cloud of toxic vapour that poisons all within.",
                Skill.Destruction,
                2,
                0,
                3,
                2,
                AttackTargetType.Aoe,
                DamageType.Poison,
                8,
                AmountType.Flat
            )
            .AddDot(3, 3f)
            .AddStatus(ConditionType.Poisoned, 3);
        var arcaneBlast = builder.AddAttack(
            "Arcane Blast",
            "A concussive burst of raw magic.",
            Skill.Destruction,
            3,
            0,
            4,
            1,
            AttackTargetType.Single,
            DamageType.Magic,
            18,
            AmountType.Flat
        );
        builder
            .AddAttack(
                "Blizzard",
                "A freezing storm that chills all nearby enemies.",
                Skill.Destruction,
                4,
                0,
                5,
                3,
                AttackTargetType.Aoe,
                DamageType.Ice,
                14,
                AmountType.Flat
            )
            .Requires(frostBolt);
        builder
            .AddAttack(
                "Inferno",
                "A massive eruption of flame that engulfs a wide area.",
                Skill.Destruction,
                4,
                0,
                6,
                4,
                AttackTargetType.Aoe,
                DamageType.Fire,
                20,
                AmountType.Flat
            )
            .AddDot(2, 4f)
            .AddStatus(ConditionType.Burning, 2)
            .Requires(fireball);
        var thunderstorm = builder
            .AddAttack(
                "Thunderstorm",
                "A violent storm that electrocutes all enemies in the area.",
                Skill.Destruction,
                6,
                0,
                7,
                5,
                AttackTargetType.Aoe,
                DamageType.Lightning,
                25,
                AmountType.Flat
            )
            .Requires(chainLightning);

        var iceLance = builder
            .AddAttack(
                "Ice Lance",
                "A piercing lance of concentrated ice.",
                Skill.Destruction,
                20,
                0,
                6,
                3,
                AttackTargetType.Single,
                DamageType.Ice,
                22,
                AmountType.Flat
            )
            .Requires(frostBolt);
        var scorch = builder
            .AddAttack(
                "Scorch",
                "A searing beam that ignites the target.",
                Skill.Destruction,
                25,
                0,
                6,
                4,
                AttackTargetType.Single,
                DamageType.Fire,
                24,
                AmountType.Flat
            )
            .AddDot(2, 4f)
            .AddStatus(ConditionType.Burning, 2)
            .Requires(fireball);

        var glacialSpike = builder
            .AddAttack(
                "Glacial Spike",
                "A massive spike of ice erupts from the earth.",
                Skill.Destruction,
                40,
                0,
                10,
                6,
                AttackTargetType.Single,
                DamageType.Ice,
                32,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Frozen, 2)
            .Requires(iceLance);
        var meteor = builder
            .AddAttack(
                "Meteor",
                "A blazing rock falls from the heavens.",
                Skill.Destruction,
                45,
                0,
                12,
                8,
                AttackTargetType.Aoe,
                DamageType.Fire,
                28,
                AmountType.Flat
            )
            .AddDot(2, 3f)
            .AddStatus(ConditionType.Burning, 2)
            .Requires(scorch);

        builder
            .AddAttack(
                "Absolute Zero",
                "A field of absolute cold that freezes all within.",
                Skill.Destruction,
                60,
                0,
                15,
                10,
                AttackTargetType.Aoe,
                DamageType.Ice,
                22,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Frozen, 2)
            .Requires(glacialSpike);
        builder
            .AddAttack(
                "Ball Lightning",
                "A rolling sphere of crackling lightning.",
                Skill.Destruction,
                65,
                0,
                14,
                9,
                AttackTargetType.Aoe,
                DamageType.Lightning,
                35,
                AmountType.Flat
            )
            .Requires(thunderstorm);

        builder
            .AddAttack(
                "Armageddon",
                "A cataclysmic firestorm that consumes everything.",
                Skill.Destruction,
                90,
                0,
                22,
                15,
                AttackTargetType.Aoe,
                DamageType.Fire,
                55,
                AmountType.Flat
            )
            .AddDot(3, 5f)
            .AddStatus(ConditionType.Burning, 3)
            .Requires(meteor);
        builder
            .AddAttack(
                "Void Bolt",
                "A bolt of pure arcane entropy that unmakes what it touches.",
                Skill.Destruction,
                100,
                0,
                25,
                20,
                AttackTargetType.Single,
                DamageType.Magic,
                80,
                AmountType.Flat
            )
            .Requires(arcaneBlast);
    }

    private static void AddIllusionAbilities(AbilityBuilder builder)
    {
        var fear = builder
            .AddAttack(
                "Fear",
                "A whisper of dread that roots the target in place.",
                Skill.Illusion,
                1,
                0,
                3,
                2,
                AttackTargetType.Single,
                DamageType.Magic,
                14,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Snared, 2);
        var maddeningWhispers = builder
            .AddAttack(
                "Maddening Whispers",
                "Unrelenting voices that drown out the target's own thoughts.",
                Skill.Illusion,
                20,
                0,
                6,
                5,
                AttackTargetType.Single,
                DamageType.Magic,
                24,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Silenced, 2)
            .Requires(fear);
        var paralyze = builder
            .AddAttack(
                "Paralyze",
                "Locks the target's muscles in place with pure dread.",
                Skill.Illusion,
                45,
                0,
                10,
                7,
                AttackTargetType.Single,
                DamageType.Magic,
                30,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Stunned, 2)
            .Requires(maddeningWhispers);
        builder
            .AddAttack(
                "Overwhelming Terror",
                "A wave of primal horror that freezes all who feel it.",
                Skill.Illusion,
                65,
                0,
                14,
                9,
                AttackTargetType.Aoe,
                DamageType.Magic,
                28,
                AmountType.Flat
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
            0,
            AttackTargetType.Single,
            DamageType.Physical,
            150,
            AmountType.Percent,
            gearRequirement: GearRequirement.RangedWeapon
        );
        var piercingShot = builder
            .AddAttack(
                "Piercing Shot",
                "An arrow that drives deep and causes bleeding.",
                Skill.Archery,
                2,
                3,
                0,
                0,
                AttackTargetType.Single,
                DamageType.Physical,
                185,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
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
                0,
                13,
                TargetType.Self,
                11
            )
            .AddModifier(AttributeName.Dexterity, 5)
            .Requires(arrowShot);
        var poisonArrow = builder
            .AddAttack(
                "Poison Arrow",
                "A barbed arrow coated with toxin.",
                Skill.Archery,
                3,
                3,
                0,
                2,
                AttackTargetType.Single,
                DamageType.Poison,
                6,
                AmountType.Flat,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .AddDot(3, 3f)
            .AddStatus(ConditionType.Poisoned, 3)
            .Requires(arrowShot);
        var volley = builder
            .AddAttack(
                "Volley",
                "A spread of arrows targeting a wide area.",
                Skill.Archery,
                4,
                5,
                0,
                3,
                AttackTargetType.Aoe,
                DamageType.Physical,
                160,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .Requires(arrowShot);

        var cripplingArrow = builder
            .AddAttack(
                "Crippling Arrow",
                "An arrow that buries deep and hinders movement.",
                Skill.Archery,
                20,
                5,
                0,
                4,
                AttackTargetType.Single,
                DamageType.Physical,
                190,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .AddStatus(ConditionType.Snared, 3)
            .Requires(piercingShot);
        var multishot = builder
            .AddAttack(
                "Multishot",
                "Release a spread of arrows simultaneously.",
                Skill.Archery,
                25,
                7,
                0,
                5,
                AttackTargetType.Aoe,
                DamageType.Physical,
                170,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .Requires(volley);
        builder
            .AddAttack(
                "Concussive Shot",
                "A blunt-tipped arrow driven hard enough to daze the target.",
                Skill.Archery,
                25,
                5,
                0,
                5,
                AttackTargetType.Single,
                DamageType.Physical,
                190,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .AddStatus(ConditionType.Stunned, 2)
            .Requires(cripplingArrow);

        var barrage = builder
            .AddAttack(
                "Barrage",
                "An unending rain of arrows over a wide area.",
                Skill.Archery,
                40,
                10,
                0,
                7,
                AttackTargetType.Aoe,
                DamageType.Physical,
                160,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .Requires(multishot);
        builder
            .AddAttack(
                "Venom Strike",
                "A deeply embedded arrow laced with deadly toxin.",
                Skill.Archery,
                45,
                8,
                0,
                6,
                AttackTargetType.Single,
                DamageType.Poison,
                14,
                AmountType.Flat,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .AddDot(4, 6f)
            .AddStatus(ConditionType.Poisoned, 4)
            .Requires(poisonArrow);

        var rainOfArrows = builder
            .AddAttack(
                "Rain of Arrows",
                "A devastating blanket of arrows covering a vast area.",
                Skill.Archery,
                60,
                14,
                0,
                9,
                AttackTargetType.Aoe,
                DamageType.Physical,
                195,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .Requires(barrage);
        var pinningShot = builder
            .AddAttack(
                "Pinning Shot",
                "An arrow that nails the target in place.",
                Skill.Archery,
                65,
                10,
                0,
                8,
                AttackTargetType.Single,
                DamageType.Physical,
                250,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .AddStatus(ConditionType.Snared, 4)
            .Requires(cripplingArrow);

        builder
            .AddAttack(
                "Arrow Storm",
                "An incomprehensible density of arrows raining from the sky.",
                Skill.Archery,
                90,
                20,
                0,
                14,
                AttackTargetType.Aoe,
                DamageType.Physical,
                310,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .Requires(rainOfArrows);
        builder
            .AddAttack(
                "Death from Afar",
                "A single perfect shot from extreme range.",
                Skill.Archery,
                95,
                22,
                0,
                16,
                AttackTargetType.Single,
                DamageType.Physical,
                490,
                AmountType.Percent,
                gearRequirement: GearRequirement.RangedWeapon
            )
            .Requires(pinningShot);
    }

    private static void AddRestorationAbilities(AbilityBuilder builder)
    {
        var mend = builder.AddInstantHeal(
            "Mend",
            "Restores a portion of an ally's health.",
            Skill.Restoration,
            1,
            0,
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
                0,
                3,
                2,
                AttackTargetType.Single,
                DamageType.Magic,
                8,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Silenced, 2);
        var regenerate = builder
            .AddHealOverTime(
                "Regenerate",
                "Grants an ally health regeneration over time.",
                Skill.Restoration,
                2,
                0,
                3,
                3,
                TargetType.Single,
                8,
                3
            )
            .Requires(mend);
        var smite = builder.AddAttack(
            "Smite",
            "A bolt of divine judgment.",
            Skill.Restoration,
            3,
            0,
            4,
            1,
            AttackTargetType.Single,
            DamageType.Magic,
            16,
            AmountType.Flat
        );
        var massHeal = builder
            .AddInstantHeal(
                "Mass Heal",
                "Restores health to all nearby allies.",
                Skill.Restoration,
                5,
                0,
                5,
                4,
                TargetType.Aoe,
                12
            )
            .Requires(regenerate);

        var greaterMend = builder
            .AddInstantHeal(
                "Greater Mend",
                "A stronger healing touch that restores more vitality.",
                Skill.Restoration,
                20,
                0,
                6,
                4,
                TargetType.Single,
                28
            )
            .Requires(mend);

        var turnUndead = builder
            .AddAttack(
                "Turn Undead",
                "Divine power that sears the undead and staggers all else.",
                Skill.Restoration,
                20,
                0,
                6,
                4,
                AttackTargetType.Single,
                DamageType.Magic,
                15,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Stunned, 1)
            .AddCreatureTypeBonus(CreatureType.Undead, 3f)
            .Requires(smite);

        var radiantBurst = builder
            .AddAttack(
                "Radiant Burst",
                "An expanding sphere of holy light.",
                Skill.Restoration,
                25,
                0,
                7,
                5,
                AttackTargetType.Aoe,
                DamageType.Magic,
                20,
                AmountType.Flat
            )
            .Requires(turnUndead);

        var sacredGround = builder
            .AddHealOverTime(
                "Sacred Ground",
                "Consecrate an area that heals allies who stand within.",
                Skill.Restoration,
                40,
                0,
                10,
                7,
                TargetType.Aoe,
                12,
                3
            )
            .Requires(massHeal);

        builder
            .AddHealOverTime(
                "Resurrection Pulse",
                "A wave of healing energy that restores all nearby allies.",
                Skill.Restoration,
                60,
                0,
                15,
                10,
                TargetType.Aoe,
                15,
                4
            )
            .Requires(sacredGround);

        var destroyUndead = builder
            .AddAttack(
                "Destroy Undead",
                "Divine annihilation that unmakes the undead and shakes all else.",
                Skill.Restoration,
                60,
                0,
                14,
                9,
                AttackTargetType.Single,
                DamageType.Magic,
                28,
                AmountType.Flat
            )
            .AddStatus(ConditionType.Stunned, 2)
            .AddCreatureTypeBonus(CreatureType.Undead, 3f)
            .Requires(radiantBurst);

        builder
            .AddAttack(
                "Wrath of the Divine",
                "An unrelenting torrent of divine judgment.",
                Skill.Restoration,
                65,
                0,
                15,
                10,
                AttackTargetType.Single,
                DamageType.Magic,
                45,
                AmountType.Flat
            )
            .Requires(destroyUndead);

        builder
            .AddInstantHeal(
                "Divine Intervention",
                "Call upon divine power to massively restore a single ally.",
                Skill.Restoration,
                90,
                0,
                22,
                15,
                TargetType.Single,
                70
            )
            .Requires(greaterMend);
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
                0,
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
                0,
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
                0,
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
                0,
                20,
                TargetType.Self,
                18
            )
            .AddModifier(AttributeName.Intelligence, 15)
            .AddModifier(AttributeName.Mana, 10)
            .Requires(arcaneInfusion);
    }

    private class AbilityBuilder
    {
        public Dictionary<string, Ability> ByName { get; } = [];

        public AttackAbilityEntry AddAttack(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int apCost,
            int mpCost,
            int cooldown,
            AttackTargetType targetType,
            DamageType damageType,
            float damageAmount,
            AmountType damageAmountType,
            GearRequirement gearRequirement = GearRequirement.None
        )
        {
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
                DamageAmountType = damageAmountType,
                DamageAmount = damageAmount,
                GearRequirement = gearRequirement,
            };
            ByName[name] = attack;
            return new AttackAbilityEntry(attack);
        }

        public InstantHealAbilityEntry AddInstantHeal(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int apCost,
            int mpCost,
            int cooldown,
            TargetType targetType,
            int amount
        )
        {
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
            return new InstantHealAbilityEntry(heal);
        }

        public HealOverTimeAbilityEntry AddHealOverTime(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int apCost,
            int mpCost,
            int cooldown,
            TargetType targetType,
            int amountPerTurn,
            int duration
        )
        {
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
            return new HealOverTimeAbilityEntry(heal);
        }

        public BuffAbilityEntry AddBuff(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int apCost,
            int mpCost,
            int cooldown,
            TargetType targetType,
            int duration
        )
        {
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
            return new BuffAbilityEntry(buff);
        }

        public GuardStanceAbilityEntry AddGuardStance(
            string name,
            string description,
            Skill skill,
            int requiredSkillLevel,
            int apCost,
            int mpCost,
            int cooldown,
            TargetType targetType,
            int duration
        )
        {
            var buff = new GuardStanceAbility
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
            return new GuardStanceAbilityEntry(buff);
        }
    }

    private abstract class AbilityEntry(Ability ability)
    {
        internal Ability Ability { get; } = ability;
    }

    private class AttackAbilityEntry(AttackAbility attack) : AbilityEntry(attack)
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
            Ability.Prerequisites.Add(prereq.Ability.Name);
            return this;
        }
    }

    private class InstantHealAbilityEntry(InstantHealAbility heal) : AbilityEntry(heal)
    {
        public InstantHealAbilityEntry Requires(AbilityEntry prereq)
        {
            Ability.Prerequisites.Add(prereq.Ability.Name);
            return this;
        }
    }

    private class HealOverTimeAbilityEntry(HealOverTimeAbility heal) : AbilityEntry(heal)
    {
        public HealOverTimeAbilityEntry Requires(AbilityEntry prereq)
        {
            Ability.Prerequisites.Add(prereq.Ability.Name);
            return this;
        }
    }

    private class BuffAbilityEntry(BuffAbility buff) : AbilityEntry(buff)
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

        public BuffAbilityEntry Requires(AbilityEntry prereq)
        {
            Ability.Prerequisites.Add(prereq.Ability.Name);
            return this;
        }
    }

    private class GuardStanceAbilityEntry(GuardStanceAbility buff) : AbilityEntry(buff)
    {
        public GuardStanceAbilityEntry AddModifier(
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

        public GuardStanceAbilityEntry AddParryModifier(
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
    }
}
