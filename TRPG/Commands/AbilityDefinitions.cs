using TRPG.Models;

namespace TRPG.Commands;

internal static class AbilityDefinitions {
    private static readonly List<AttackAbility> _attacks = [];
    private static readonly List<SupportAbility> _supports = [];
    private static readonly List<(string Ability, string Prerequisite)> _prerequisites = [];
    private static readonly Dictionary<string, Ability> _byName = [];

    static AbilityDefinitions() {
        AttackAbility AddAttack(string name, string description, Skill skill, int requiredSkillLevel, int cost,
            int cooldown, TargetType targetType, float? aoeRadius, DamageType damageType, float damageAmount,
            params (ConditionType Condition, int Duration, float? Amount)[] conditions) {
            var attack = new AttackAbility {
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
                Conditions = conditions.Select(c => new ConditionEffect {
                    Condition = c.Condition,
                    Duration = c.Duration,
                    Amount = c.Amount,
                    Type = c.Amount.HasValue ? AmountType.Flat : null
                }).ToList()
            };
            _attacks.Add(attack);
            _byName[name] = attack;
            return attack;
        }

        SupportAbility AddSupport(string name, string description, Skill skill, int requiredSkillLevel, int cost,
            int cooldown, TargetType targetType, float? aoeRadius, int? duration,
            params (AttributeName Attribute, float Amount)[] modifiers) {
            var support = new SupportAbility {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                Cost = cost,
                Cooldown = cooldown,
                TargetType = targetType,
                AoeRadius = aoeRadius,
                Duration = duration,
                Modifiers = modifiers.Select(m => new AttributeModifier {
                    Attribute = m.Attribute,
                    Type = AmountType.Flat,
                    Amount = m.Amount
                }).ToList()
            };
            _supports.Add(support);
            _byName[name] = support;
            return support;
        }

        void Requires(Ability ability, Ability prereq) => _prerequisites.Add((ability.Name, prereq.Name));

        // Swordsmanship
        var slash      = AddAttack("Slash",      "A basic melee strike.",                                    Skill.Swordsmanship, 1, 2, 0, TargetType.Single, null, DamageType.Physical, 8);
        var cleave     = AddAttack("Cleave",     "A wide swing that hits nearby enemies.",                   Skill.Swordsmanship, 2, 3, 0, TargetType.Aoe,    2f,   DamageType.Physical, 12);
        var shieldBash = AddAttack("Shield Bash","A stunning blow with the shield.",                         Skill.Swordsmanship, 2, 2, 2, TargetType.Single, null, DamageType.Physical, 5,  (ConditionType.Stunned,  1, null));
        var devastate  = AddAttack("Devastate",  "A crippling strike that leaves the target bleeding.",      Skill.Swordsmanship, 4, 4, 3, TargetType.Single, null, DamageType.Physical, 14, (ConditionType.Bleeding, 2, 3f));
        var whirlwind  = AddAttack("Whirlwind",  "A spinning strike that damages all surrounding enemies.",  Skill.Swordsmanship, 5, 5, 3, TargetType.Aoe,    4f,   DamageType.Physical, 18);
        Requires(cleave,     slash);
        Requires(shieldBash, slash);
        Requires(devastate,  shieldBash);
        Requires(whirlwind,  cleave);

        // Stealth
        var stab        = AddAttack("Stab",             "A quick jab at a vital point.",                              Skill.Stealth, 1, 1, 0, TargetType.Single, null, DamageType.Physical, 5);
        var backstab    = AddAttack("Backstab",          "A precision strike from an unexpected angle.",               Skill.Stealth, 2, 3, 0, TargetType.Single, null, DamageType.Physical, 16);
        var hamstring   = AddAttack("Hamstring",         "A cut to the legs that slows the target.",                   Skill.Stealth, 2, 2, 2, TargetType.Single, null, DamageType.Physical, 6,  (ConditionType.Snared,   2, null));
        var hemorrhage  = AddAttack("Hemorrhage",        "A deep wound that causes severe bleeding.",                  Skill.Stealth, 4, 3, 2, TargetType.Single, null, DamageType.Physical, 8,  (ConditionType.Bleeding, 3, 4f));
        var shadowstep  = AddAttack("Shadowstep Strike", "A gap-closing attack that blinds the target.",               Skill.Stealth, 5, 4, 3, TargetType.Single, null, DamageType.Physical, 20, (ConditionType.Blinded,  1, null));
        Requires(backstab,   stab);
        Requires(hamstring,  stab);
        Requires(hemorrhage, hamstring);
        Requires(shadowstep, backstab);

        // Spellcasting
        var frostBolt     = AddAttack("Frost Bolt",     "A bolt of ice that damages and snares the target.",                Skill.Spellcasting, 1, 3, 0, TargetType.Single, null, DamageType.Ice,       10, (ConditionType.Snared,   1, null));
        var fireball      = AddAttack("Fireball",        "A ball of fire hurled at a single target.",                       Skill.Spellcasting, 1, 3, 0, TargetType.Single, null, DamageType.Fire,      12, (ConditionType.Burning,  1, 3f));
        var chainLightning = AddAttack("Chain Lightning","Lightning that arcs between nearby enemies.",                     Skill.Spellcasting, 2, 4, 2, TargetType.Aoe,    3f,   DamageType.Lightning, 15);
        var poisonCloud   = AddAttack("Poison Cloud",   "A cloud of toxic vapour that poisons all within.",                Skill.Spellcasting, 2, 3, 2, TargetType.Aoe,    4f,   DamageType.Poison,    8,  (ConditionType.Poisoned, 3, 3f));
        var arcaneBlast   = AddAttack("Arcane Blast",   "A concussive burst of raw magic that silences the target.",       Skill.Spellcasting, 3, 4, 1, TargetType.Single, null, DamageType.Magic,     18, (ConditionType.Silenced, 1, null));
        var blizzard      = AddAttack("Blizzard",        "A freezing storm that chills all nearby enemies.",                Skill.Spellcasting, 4, 5, 3, TargetType.Aoe,    5f,   DamageType.Ice,       14, (ConditionType.Frozen,   1, null));
        var inferno       = AddAttack("Inferno",         "A massive eruption of flame that engulfs a wide area.",           Skill.Spellcasting, 4, 6, 4, TargetType.Aoe,    6f,   DamageType.Fire,      20, (ConditionType.Burning,  2, 4f));
        var thunderstorm  = AddAttack("Thunderstorm",   "A violent storm that electrocutes all enemies in the area.",      Skill.Spellcasting, 6, 7, 5, TargetType.Aoe,    8f,   DamageType.Lightning, 25, (ConditionType.Stunned,  1, null));
        Requires(blizzard,      frostBolt);
        Requires(inferno,       fireball);
        Requires(thunderstorm,  chainLightning);

        // Archery
        var arrowShot    = AddAttack("Arrow Shot",    "A standard ranged attack.",                            Skill.Archery, 1, 2, 0, TargetType.Single, null, DamageType.Physical, 8);
        var piercingShot = AddAttack("Piercing Shot", "An arrow that drives deep and causes bleeding.",       Skill.Archery, 2, 3, 0, TargetType.Single, null, DamageType.Physical, 14, (ConditionType.Bleeding, 1, 3f));
        var poisonArrow  = AddAttack("Poison Arrow",  "A barbed arrow coated with toxin.",                   Skill.Archery, 3, 3, 2, TargetType.Single, null, DamageType.Poison,   6,  (ConditionType.Poisoned, 3, 3f));
        var volley       = AddAttack("Volley",        "A spread of arrows targeting a wide area.",            Skill.Archery, 4, 5, 3, TargetType.Aoe,    4f,   DamageType.Physical, 10);
        Requires(piercingShot, arrowShot);
        Requires(poisonArrow,  arrowShot);
        Requires(volley,       arrowShot);

        // Devotion
        var mend            = AddSupport("Mend",               "Restores a portion of an ally's health.",                       Skill.Devotion, 1, 2, 0, TargetType.Single, null, null, (AttributeName.CurrentHp,          15));
        var divineShield    = AddSupport("Divine Shield",       "Fortifies an ally with a magical barrier.",                     Skill.Devotion, 2, 3, 3, TargetType.Single, null, 2,    (AttributeName.PhysicalResistance,  20));
        var regenerate      = AddSupport("Regenerate",          "Grants an ally health regeneration over time.",                 Skill.Devotion, 2, 3, 3, TargetType.Single, null, 3,    (AttributeName.CurrentHp,           8));
        var auraProtection  = AddSupport("Aura of Protection",  "Grants nearby allies increased physical resistance.",           Skill.Devotion, 4, 5, 5, TargetType.Aoe,    5f,   3,    (AttributeName.PhysicalResistance,  15));
        var massHeal        = AddSupport("Mass Heal",           "Restores health to all nearby allies.",                        Skill.Devotion, 5, 5, 4, TargetType.Aoe,    6f,   null, (AttributeName.CurrentHp,           12));
        Requires(divineShield,   mend);
        Requires(regenerate,     mend);
        Requires(auraProtection, divineShield);
        Requires(massHeal,       regenerate);

        // Warfare
        var battleStance  = AddSupport("Battle Stance",   "Braces the caster for combat, increasing strength.",          Skill.Warfare, 1, 1, 0, TargetType.Self,   null, 3, (AttributeName.Strength,          5));
        var arcaneInfusion = AddSupport("Arcane Infusion","Infuses an ally with arcane energy, boosting intelligence.",  Skill.Warfare, 1, 2, 2, TargetType.Single, null, 3, (AttributeName.Intelligence,      10));
        var ironWill      = AddSupport("Iron Will",       "Hardens the caster's resolve against physical harm.",         Skill.Warfare, 2, 2, 3, TargetType.Self,   null, 2, (AttributeName.Defense,           10), (AttributeName.Endurance, 10));
        var haste         = AddSupport("Haste",           "Accelerates an ally's movements and reflexes.",              Skill.Warfare, 2, 2, 3, TargetType.Single, null, 2, (AttributeName.Dexterity,         5),  (AttributeName.MovementSpeed, 3));
        var rally         = AddSupport("Rally",           "A battle cry that strengthens nearby allies.",               Skill.Warfare, 3, 3, 4, TargetType.Aoe,    5f,   2, (AttributeName.Strength,          8));
        var spellWard     = AddSupport("Spell Ward",      "Erects a magical barrier against incoming spells.",          Skill.Warfare, 3, 3, 4, TargetType.Self,   null, 3, (AttributeName.MagicResistance,   20));
        Requires(ironWill,  battleStance);
        Requires(rally,     battleStance);
        Requires(spellWard, arcaneInfusion);
    }

    public static IReadOnlyList<AttackAbility> Attacks => _attacks;
    public static IReadOnlyList<SupportAbility> Supports => _supports;

    public static Ability? GetAbility(string name) => _byName.GetValueOrDefault(name);

    public static IReadOnlyList<string> GetPrerequisites(string abilityName) =>
        _prerequisites.Where(p => p.Ability == abilityName).Select(p => p.Prerequisite).ToList().AsReadOnly();
}
