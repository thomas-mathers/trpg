namespace TRPG.Data.Models;

public class ActiveDotSnapshot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public string DamageType { get; init; } = "";
    public int RemainingTurns { get; init; }
}

public class ActiveHotSnapshot
{
    public string AbilityName { get; init; } = "";
    public int Amount { get; init; }
    public int RemainingTurns { get; init; }
}

public class ActiveBuffSnapshot
{
    public float Amount { get; init; }
    public string Attribute { get; init; } = "";
    public int RemainingTurns { get; init; }
    public string AmountType { get; init; } = "";
}

public class CombatantSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public Guid CreatureId { get; init; }
    public bool IsPlayer { get; init; }
    public int CurrentHp { get; set; }
    public int CurrentAp { get; set; }
    public int CurrentMp { get; set; }
    public Dictionary<string, int> WeaponSwingCounts { get; set; } = [];
    public Dictionary<string, int> ActiveConditions { get; set; } = [];
    public Dictionary<string, int> CooldownRemainingByAbility { get; set; } = [];
    public List<ActiveDotSnapshot> ActiveDots { get; set; } = [];
    public List<ActiveHotSnapshot> ActiveHots { get; set; } = [];
    public List<ActiveBuffSnapshot> ActiveBuffs { get; set; } = [];
}
