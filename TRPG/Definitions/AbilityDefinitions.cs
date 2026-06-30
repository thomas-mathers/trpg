using TRPG.Models;

namespace TRPG.Definitions;

internal class AbilityDefinitions(
    Dictionary<string, Ability> byName,
    Dictionary<string, HashSet<string>> prerequisites) {
    public IReadOnlyCollection<Ability> Abilities => byName.Values;

    public static AbilityDefinitions Create() {
        var builder = new AbilityBuilder();
        AddSwordsmanshipAbilities(builder);
        AddStealthAbilities(builder);
        AddSpellcastingAbilities(builder);
        AddArcheryAbilities(builder);
        AddDevotionAbilities(builder);
        AddWarfareAbilities(builder);
        return new AbilityDefinitions(builder.ByName, builder.Prerequisites);
    }

    public string RandomAttackAbility() {
        var attacks = Abilities.OfType<AttackAbility>().ToList();
        return attacks[Random.Shared.Next(attacks.Count)].Name;
    }

    private static void AddSwordsmanshipAbilities(AbilityBuilder builder) {
        var slash = builder.AddAttack("Slash", "A basic melee strike.", Skill.Swordsmanship, 1, 2, 0, TargetType.Single,
            null, DamageType.Physical, 8);
        var cleave = builder.AddAttack("Cleave", "A wide swing that hits nearby enemies.", Skill.Swordsmanship, 2, 3, 0,
                TargetType.Aoe, 2f, DamageType.Physical, 12)
            .Requires(slash);
        var shieldBash = builder.AddAttack("Shield Bash", "A stunning blow with the shield.", Skill.Swordsmanship, 2, 2,
                2, TargetType.Single, null, DamageType.Physical, 5)
            .AddCondition(ConditionType.Stunned, 1)
            .Requires(slash);
        builder.AddAttack("Devastate", "A crippling strike that leaves the target bleeding.", Skill.Swordsmanship, 4, 4,
                3, TargetType.Single, null, DamageType.Physical, 14)
            .AddCondition(ConditionType.Bleeding, 2, 3f)
            .Requires(shieldBash);
        builder.AddAttack("Whirlwind", "A spinning strike that damages all surrounding enemies.", Skill.Swordsmanship,
                5, 5, 3, TargetType.Aoe, 4f, DamageType.Physical, 18)
            .Requires(cleave);
    }

    private static void AddStealthAbilities(AbilityBuilder builder) {
        var stab = builder.AddAttack("Stab", "A quick jab at a vital point.", Skill.Stealth, 1, 1, 0, TargetType.Single,
            null, DamageType.Physical, 5);
        var backstab = builder.AddAttack("Backstab", "A precision strike from an unexpected angle.", Skill.Stealth, 2,
                3, 0, TargetType.Single, null, DamageType.Physical, 16)
            .Requires(stab);
        var hamstring = builder.AddAttack("Hamstring", "A cut to the legs that slows the target.", Skill.Stealth, 2, 2,
                2, TargetType.Single, null, DamageType.Physical, 6)
            .AddCondition(ConditionType.Snared, 2)
            .Requires(stab);
        builder.AddAttack("Hemorrhage", "A deep wound that causes severe bleeding.", Skill.Stealth, 4, 3, 2,
                TargetType.Single, null, DamageType.Physical, 8)
            .AddCondition(ConditionType.Bleeding, 3, 4f)
            .Requires(hamstring);
        builder.AddAttack("Shadowstep Strike", "A gap-closing attack that blinds the target.", Skill.Stealth, 5, 4, 3,
                TargetType.Single, null, DamageType.Physical, 20)
            .AddCondition(ConditionType.Blinded, 1)
            .Requires(backstab);
    }

    private static void AddSpellcastingAbilities(AbilityBuilder builder) {
        var frostBolt = builder.AddAttack("Frost Bolt", "A bolt of ice that damages and snares the target.",
                Skill.Spellcasting, 1, 3, 0, TargetType.Single, null, DamageType.Ice, 10)
            .AddCondition(ConditionType.Snared, 1);
        var fireball = builder.AddAttack("Fireball", "A ball of fire hurled at a single target.", Skill.Spellcasting, 1,
                3, 0, TargetType.Single, null, DamageType.Fire, 12)
            .AddCondition(ConditionType.Burning, 1, 3f);
        var chainLightning = builder.AddAttack("Chain Lightning", "Lightning that arcs between nearby enemies.",
            Skill.Spellcasting, 2, 4, 2, TargetType.Aoe, 3f, DamageType.Lightning, 15);
        builder.AddAttack("Poison Cloud", "A cloud of toxic vapour that poisons all within.", Skill.Spellcasting, 2, 3,
                2, TargetType.Aoe, 4f, DamageType.Poison, 8)
            .AddCondition(ConditionType.Poisoned, 3, 3f);
        builder.AddAttack("Arcane Blast", "A concussive burst of raw magic that silences the target.",
                Skill.Spellcasting, 3, 4, 1, TargetType.Single, null, DamageType.Magic, 18)
            .AddCondition(ConditionType.Silenced, 1);
        builder.AddAttack("Blizzard", "A freezing storm that chills all nearby enemies.", Skill.Spellcasting, 4, 5, 3,
                TargetType.Aoe, 5f, DamageType.Ice, 14)
            .AddCondition(ConditionType.Frozen, 1)
            .Requires(frostBolt);
        builder.AddAttack("Inferno", "A massive eruption of flame that engulfs a wide area.", Skill.Spellcasting, 4, 6,
                4, TargetType.Aoe, 6f, DamageType.Fire, 20)
            .AddCondition(ConditionType.Burning, 2, 4f)
            .Requires(fireball);
        builder.AddAttack("Thunderstorm", "A violent storm that electrocutes all enemies in the area.",
                Skill.Spellcasting, 6, 7, 5, TargetType.Aoe, 8f, DamageType.Lightning, 25)
            .AddCondition(ConditionType.Stunned, 1)
            .Requires(chainLightning);
    }

    private static void AddArcheryAbilities(AbilityBuilder builder) {
        var arrowShot = builder.AddAttack("Arrow Shot", "A standard ranged attack.", Skill.Archery, 1, 2, 0,
            TargetType.Single, null, DamageType.Physical, 8);
        builder.AddAttack("Piercing Shot", "An arrow that drives deep and causes bleeding.", Skill.Archery, 2, 3, 0,
                TargetType.Single, null, DamageType.Physical, 14)
            .AddCondition(ConditionType.Bleeding, 1, 3f)
            .Requires(arrowShot);
        builder.AddAttack("Poison Arrow", "A barbed arrow coated with toxin.", Skill.Archery, 3, 3, 2,
                TargetType.Single, null, DamageType.Poison, 6)
            .AddCondition(ConditionType.Poisoned, 3, 3f)
            .Requires(arrowShot);
        builder.AddAttack("Volley", "A spread of arrows targeting a wide area.", Skill.Archery, 4, 5, 3, TargetType.Aoe,
                4f, DamageType.Physical, 10)
            .Requires(arrowShot);
    }

    private static void AddDevotionAbilities(AbilityBuilder builder) {
        var mend = builder.AddSupport("Mend", "Restores a portion of an ally's health.", Skill.Devotion, 1, 2, 0,
                TargetType.Single, null, null)
            .AddModifier(AttributeName.Hp, 15);
        var divineShield = builder.AddSupport("Divine Shield", "Fortifies an ally with a magical barrier.",
                Skill.Devotion, 2, 3, 3, TargetType.Single, null, 2)
            .AddModifier(AttributeName.PhysicalResistance, 20)
            .Requires(mend);
        var regenerate = builder.AddSupport("Regenerate", "Grants an ally health regeneration over time.",
                Skill.Devotion, 2, 3, 3, TargetType.Single, null, 3)
            .AddModifier(AttributeName.Hp, 8)
            .Requires(mend);
        builder.AddSupport("Aura of Protection", "Grants nearby allies increased physical resistance.", Skill.Devotion,
                4, 5, 5, TargetType.Aoe, 5f, 3)
            .AddModifier(AttributeName.PhysicalResistance, 15)
            .Requires(divineShield);
        builder.AddSupport("Mass Heal", "Restores health to all nearby allies.", Skill.Devotion, 5, 5, 4,
                TargetType.Aoe, 6f, null)
            .AddModifier(AttributeName.Hp, 12)
            .Requires(regenerate);
    }

    private static void AddWarfareAbilities(AbilityBuilder builder) {
        var battleStance = builder.AddSupport("Battle Stance", "Braces the caster for combat, increasing strength.",
                Skill.Warfare, 1, 1, 0, TargetType.Self, null, 3)
            .AddModifier(AttributeName.Strength, 5);
        var arcaneInfusion = builder.AddSupport("Arcane Infusion",
                "Infuses an ally with arcane energy, boosting intelligence.", Skill.Warfare, 1, 2, 2, TargetType.Single,
                null, 3)
            .AddModifier(AttributeName.Intelligence, 10);
        builder.AddSupport("Iron Will", "Hardens the caster's resolve against physical harm.", Skill.Warfare, 2, 2, 3,
                TargetType.Self, null, 2)
            .AddModifier(AttributeName.Defense, 10)
            .AddModifier(AttributeName.Endurance, 10)
            .Requires(battleStance);
        builder.AddSupport("Haste", "Accelerates an ally's movements and reflexes.", Skill.Warfare, 2, 2, 3,
                TargetType.Single, null, 2)
            .AddModifier(AttributeName.Dexterity, 5)
            .AddModifier(AttributeName.MovementSpeed, 3);
        builder.AddSupport("Rally", "A battle cry that strengthens nearby allies.", Skill.Warfare, 3, 3, 4,
                TargetType.Aoe, 5f, 2)
            .AddModifier(AttributeName.Strength, 8)
            .Requires(battleStance);
        builder.AddSupport("Spell Ward", "Erects a magical barrier against incoming spells.", Skill.Warfare, 3, 3, 4,
                TargetType.Self, null, 3)
            .AddModifier(AttributeName.MagicResistance, 20)
            .Requires(arcaneInfusion);
    }

    public Ability? GetAbility(string name) {
        return byName.GetValueOrDefault(name);
    }

    public IReadOnlyCollection<string> GetPrerequisites(string abilityName) {
        return prerequisites.TryGetValue(abilityName, out var prereqs) ? prereqs : [];
    }

    private class AbilityBuilder {
        public Dictionary<string, Ability> ByName { get; } = [];
        public Dictionary<string, HashSet<string>> Prerequisites { get; } = [];

        public AttackAbilityEntry AddAttack(string name, string description, Skill skill, int requiredSkillLevel,
            int cost, int cooldown, TargetType targetType, float? aoeRadius, DamageType damageType,
            float damageAmount) {
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
                DamageAmount = damageAmount
            };
            ByName[name] = attack;
            return new AttackAbilityEntry(attack, this);
        }

        public SupportAbilityEntry AddSupport(string name, string description, Skill skill, int requiredSkillLevel,
            int cost, int cooldown, TargetType targetType, float? aoeRadius, int? duration) {
            var support = new SupportAbility {
                Name = name,
                Description = description,
                Skill = skill,
                RequiredSkillLevel = requiredSkillLevel,
                Cost = cost,
                Cooldown = cooldown,
                TargetType = targetType,
                AoeRadius = aoeRadius,
                Duration = duration
            };
            ByName[name] = support;
            return new SupportAbilityEntry(support, this);
        }

        internal void AddPrerequisite(Ability ability, Ability prereq) {
            if (!Prerequisites.TryGetValue(ability.Name, out var set)) {
                set = [];
                Prerequisites[ability.Name] = set;
            }

            set.Add(prereq.Name);
        }
    }

    private abstract class AbilityEntry(Ability ability, AbilityBuilder owner) {
        protected AbilityBuilder Owner { get; } = owner;
        internal Ability Ability { get; } = ability;
    }

    private class AttackAbilityEntry(AttackAbility attack, AbilityBuilder owner) : AbilityEntry(attack, owner) {
        public AttackAbilityEntry AddCondition(ConditionType condition, int duration, float? amount = null) {
            attack.Conditions.Add(new ConditionEffect {
                Condition = condition,
                Duration = duration,
                Amount = amount,
                Type = amount.HasValue ? AmountType.Flat : null
            });
            return this;
        }

        public AttackAbilityEntry Requires(AbilityEntry prereq) {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }

    private class SupportAbilityEntry(SupportAbility support, AbilityBuilder owner) : AbilityEntry(support, owner) {
        public SupportAbilityEntry AddModifier(AttributeName attribute, float amount) {
            support.Modifiers.Add(new AttributeModifier {
                Attribute = attribute,
                Type = AmountType.Flat,
                Amount = amount
            });
            return this;
        }

        public SupportAbilityEntry Requires(AbilityEntry prereq) {
            Owner.AddPrerequisite(Ability, prereq.Ability);
            return this;
        }
    }
}