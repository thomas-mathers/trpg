using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Commands.Bootstrap;
using TRPG.Extensions;
using TRPG.Models;

namespace TRPG.Commands;

internal class GenerateSkillsCommand {
    public int AttackCount { get; init; } = 10;
    public required string Description { get; init; }
    public int SupportCount { get; init; } = 5;
    public required Guid WorldId { get; init; }
}

internal class GenerateSkillsCommandResult {
    public required IReadOnlyList<Attack> Attacks { get; init; }
    public required IReadOnlyList<SkillPrerequisite> Prerequisites { get; init; }
    public required IReadOnlyList<Support> Supports { get; init; }
}

internal class GenerateSkillsCommandHandler(AiClient client, ILogger<GenerateSkillsCommandHandler> logger) {
    public async Task<GenerateSkillsCommandResult> Handle(
        GenerateSkillsCommand command,
        CancellationToken cancellationToken
    ) {
        var sw = Stopwatch.StartNew();

        var conditionTypes = string.Join(", ", Enum.GetNames<ConditionType>());
        var damageTypes = string.Join(", ", Enum.GetNames<DamageType>());
        var attributeNames = string.Join(", ", Enum.GetNames<AttributeName>());

        var attackChat = client.CreateChat<GenerateSkillsCommandHandler>(
            $$"""
             You are a creative world-building assistant for a TRPG game generating content for: {{command.Description}}.
             When asked to generate an attack skill, respond with a single JSON object with these fields:
             - Name (string, unique)
             - Description (string)
             - Category: a skill archetype such as Warrior, Ranger, Mage, Rogue — group thematically related skills under the same category
             - Cost: AP cost (integer 0-10)
             - Cooldown: turns (integer, 0 = no cooldown)
             - TargetType: Single (one enemy) or Aoe (all enemies in radius)
             - AoeRadius: metres (float), only when TargetType is Aoe, omit otherwise
             - DamageType: one of {{damageTypes}}
             - DamageAmountType: Flat or Percent
             - DamageAmount: number (use 0.15 for 15% when Percent)
             - Conditions: optional array of rider conditions applied to the enemy, each with:
               - Condition: one of {{conditionTypes}}
               - Duration: turns (integer)
               - Amount: number (for Bleeding/Poisoned DoT damage per turn), omit for others
               - AmountType: Flat or Percent (for Bleeding/Poisoned), omit for others
             Vary categories, damage types, target types, and conditions. Each attack must be unique. Do not use markdown.
             Example: {"Name":"Blizzard","Description":"Summons a freezing storm that damages and chills all nearby enemies.","Category":"Mage","Cost":6,"Cooldown":4,"TargetType":"Aoe","AoeRadius":8.0,"DamageType":"Ice","DamageAmountType":"Flat","DamageAmount":12,"Conditions":[{"Condition":"Frozen","Duration":1}]}
             """);

        var supportChat = client.CreateChat<GenerateSkillsCommandHandler>(
            $$"""
             You are a creative world-building assistant for a TRPG game generating content for: {{command.Description}}.
             When asked to generate a support skill, respond with a single JSON object with these fields:
             - Name (string, unique)
             - Description (string)
             - Category: a skill archetype such as Warrior, Ranger, Mage, Rogue — group thematically related skills under the same category
             - Cost: AP cost (integer 0-10)
             - Cooldown: turns (integer, 0 = no cooldown)
             - TargetType: Single (one ally), Aoe (all allies in radius), or Self
             - AoeRadius: metres (float), only when TargetType is Aoe, omit otherwise
             - Duration: turns the effect lasts (integer), or null if permanent
             - Modifiers: array of stat changes, each with:
               - Attribute: one of {{attributeNames}}
               - Type: Flat or Percent
               - Amount: number (negative for penalties, use 0.15 for 15% when Percent)
             Vary categories, target types, stats, and durations. Each support must be unique. Do not use markdown.
             Example: {"Name":"War Cry","Description":"A rallying shout that bolsters nearby allies' strength.","Category":"Warrior","Cost":2,"Cooldown":3,"TargetType":"Aoe","AoeRadius":6.0,"Duration":3,"Modifiers":[{"Attribute":"Strength","Type":"Flat","Amount":5}]}
             """);

        var attackSchemas = new List<AttackSchema>();
        for (var i = 0; i < command.AttackCount; i++) {
            var schema = await attackChat.GetJson<AttackSchema>(
                $"Generate attack skill {i + 1} of {command.AttackCount}.",
                cancellationToken);
            attackSchemas.Add(schema);
        }

        var supportSchemas = new List<SupportSchema>();
        for (var i = 0; i < command.SupportCount; i++) {
            var schema = await supportChat.GetJson<SupportSchema>(
                $"Generate support skill {i + 1} of {command.SupportCount}.",
                cancellationToken);
            supportSchemas.Add(schema);
        }

        var allSkillNames = attackSchemas.Select(a => a.Name)
            .Concat(supportSchemas.Select(s => s.Name))
            .ToList();
        var allCategories = attackSchemas.Select(a => a.Category)
            .Concat(supportSchemas.Select(s => s.Category))
            .ToList();

        var prereqSchemas = new List<SkillPrerequisitesSchema>();
        for (var i = 0; i < allSkillNames.Count; i++) {
            var myCategory = allCategories[i];
            var sameCategoryPreceding = allSkillNames
                .Take(i)
                .Where((_, j) => allCategories[j].Equals(myCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (sameCategoryPreceding.Count == 0) {
                prereqSchemas.Add(new SkillPrerequisitesSchema());
            } else {
                var prereqs = await attackChat.GetJson<SkillPrerequisitesSchema>(
                    $"Choose 0-2 prerequisites for '{allSkillNames[i]}' ({myCategory}) from this same-category list (or none if it is a starting skill): {string.Join(", ", sameCategoryPreceding)}. Respond with PrerequisiteSkillNames using only names from that list. Example: {{\"PrerequisiteSkillNames\":[\"{sameCategoryPreceding[0]}\"]}}",
                    cancellationToken);
                prereqSchemas.Add(prereqs);
            }
        }

        var attacks = attackSchemas.Select(a => new Attack {
            WorldId = command.WorldId,
            Name = a.Name,
            Description = a.Description,
            Cost = a.Cost,
            Cooldown = a.Cooldown,
            TargetType = TryParseEnum<TargetType>(a.TargetType) is TargetType.Aoe ? TargetType.Aoe : TargetType.Single,
            AoeRadius = a.TargetType.Equals("Aoe", StringComparison.OrdinalIgnoreCase) ? a.AoeRadius : null,
            DamageType = TryParseEnum<DamageType>(a.DamageType) ?? DamageType.Physical,
            DamageAmountType = TryParseEnum<AmountType>(a.DamageAmountType) ?? AmountType.Flat,
            DamageAmount = a.DamageAmount,
            Conditions = MapConditions(a.Name, a.Conditions)
        }).ToList();

        var supports = supportSchemas.Select(s => new Support {
            WorldId = command.WorldId,
            Name = s.Name,
            Description = s.Description,
            Cost = s.Cost,
            Cooldown = s.Cooldown,
            TargetType = TryParseEnum<TargetType>(s.TargetType) ?? TargetType.Self,
            AoeRadius = s.TargetType.Equals("Aoe", StringComparison.OrdinalIgnoreCase) ? s.AoeRadius : null,
            Duration = s.Duration,
            Modifiers = MapModifiers(s.Name, s.Modifiers)
        }).ToList();

        var allSkills = attacks.Concat(supports.Cast<Skill>()).ToList();
        var prerequisites = new List<SkillPrerequisite>();

        for (var i = 0; i < allSkillNames.Count; i++) {
            foreach (var prereqName in prereqSchemas[i].PrerequisiteSkillNames) {
                var prereq = allSkills.Take(i).FirstOrDefault(s =>
                    s.Name.Equals(prereqName, StringComparison.OrdinalIgnoreCase));
                if (prereq is not null)
                    prerequisites.Add(new SkillPrerequisite {
                        SkillId = allSkills[i].Id, PrerequisiteSkillId = prereq.Id
                    });
                else
                    logger.LogWarning("Dropped prerequisite '{Prereq}' for '{Skill}': not found in skill list", prereqName, allSkillNames[i]);
            }
        }

        logger.LogDebug("GenerateSkills completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);
        return new GenerateSkillsCommandResult { Attacks = attacks, Supports = supports, Prerequisites = prerequisites };
    }

    private List<ConditionEffect> MapConditions(string skillName, List<ConditionEffectSchema> schemas) =>
        schemas
            .Select(c => {
                var condType = TryParseEnum<ConditionType>(c.Condition);
                if (condType is null) {
                    logger.LogWarning("Dropped condition '{Condition}' on '{Skill}': unknown ConditionType", c.Condition, skillName);
                    return null;
                }
                return new ConditionEffect {
                    Condition = condType.Value,
                    Duration = c.Duration,
                    Amount = c.Amount,
                    Type = TryParseEnum<AmountType>(c.AmountType)
                };
            })
            .OfType<ConditionEffect>()
            .ToList();

    private List<AttributeModifier> MapModifiers(string skillName, List<AttributeModifierSchema> schemas) =>
        schemas
            .Select(m => {
                var attr = TryParseEnum<AttributeName>(m.Attribute);
                if (attr is null) {
                    logger.LogWarning("Dropped modifier '{Attribute}' on '{Skill}': unknown AttributeName", m.Attribute, skillName);
                    return null;
                }
                return new AttributeModifier {
                    Attribute = attr.Value,
                    Type = TryParseEnum<AmountType>(m.Type) ?? AmountType.Flat,
                    Amount = m.Amount
                };
            })
            .OfType<AttributeModifier>()
            .ToList();

    private static T? TryParseEnum<T>(string? value) where T : struct, Enum {
        if (string.IsNullOrEmpty(value)) return null;
        return Enum.TryParse<T>(value, true, out var result) ? result : null;
    }
}
