namespace TRPG.Commands.Bootstrap;

internal class GeographyEntitySchema {
    public string Description { get; set; } = "";
    public string Name { get; set; } = "";
}

internal class AttackSchema {
    public float? AoeRadius { get; set; }
    public string Category { get; set; } = "";
    public List<ConditionEffectSchema> Conditions { get; set; } = [];
    public int Cooldown { get; set; }
    public int Cost { get; set; }
    public float DamageAmount { get; set; }
    public string DamageAmountType { get; set; } = "";
    public string DamageType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetType { get; set; } = "";
}

internal class ConditionEffectSchema {
    public float? Amount { get; set; }
    public string? AmountType { get; set; }
    public string Condition { get; set; } = "";
    public int Duration { get; set; }
}

internal class SupportSchema {
    public float? AoeRadius { get; set; }
    public string Category { get; set; } = "";
    public int Cooldown { get; set; }
    public int Cost { get; set; }
    public string Description { get; set; } = "";
    public int? Duration { get; set; }
    public List<AttributeModifierSchema> Modifiers { get; set; } = [];
    public string Name { get; set; } = "";
    public string TargetType { get; set; } = "";
}

internal class AttributeModifierSchema {
    public float Amount { get; set; }
    public string Attribute { get; set; } = "";
    public string Type { get; set; } = "";
}

internal class SkillPrerequisitesSchema {
    public List<string> PrerequisiteSkillNames { get; set; } = [];
}

internal class ItemSchema {
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public int GoldValue { get; set; }
    public bool IsStackable { get; set; }
    public List<AttributeModifierSchema> Modifiers { get; set; } = [];
    public string Name { get; set; } = "";
    public int Weight { get; set; }
}

internal class PersonSchema {
    public string Biography { get; set; } = "";
    public string BirthCityName { get; set; } = "";
    public int BirthYear { get; set; }
    public string Name { get; set; } = "";
    public string ProfessionName { get; set; } = "";
    public string RaceName { get; set; } = "";
}

internal class QuestSchema {
    public string Description { get; set; } = "";
    public int ExperienceReward { get; set; }
    public string GiverName { get; set; } = "";
    public int GoldReward { get; set; }
    public List<string> ItemRewardNames { get; set; } = [];
    public string Name { get; set; } = "";
    public List<string> PrerequisiteQuestNames { get; set; } = [];
}