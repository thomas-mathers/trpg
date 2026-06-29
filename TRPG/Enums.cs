namespace TRPG;

internal enum EquipmentSlot {
    Helm,
    Chest,
    LeftHand,
    RightHand,
    Boots,
    Necklace,
    Gloves,
    LeftRing,
    RightRing,
    Belt
}

internal enum ItemCategory {
    Helm,
    Chest,
    Sword,
    Spear,
    Bow,
    Staff,
    Shield,
    Boots,
    Necklace,
    Gloves,
    Ring,
    Belt,
    Arrows,
    Consumable,
    Quest,
    CraftingMaterial
}

internal enum AmountType {
    Flat,
    Percent
}

internal enum AttributeName {
    CurrentHp,
    MaximumHp,
    CurrentAp,
    MaximumAp,
    Strength,
    Defense,
    Dexterity,
    Endurance,
    Intelligence,
    PhysicalResistance,
    FireResistance,
    IceResistance,
    LightningResistance,
    PoisonResistance,
    MagicResistance,
    MovementSpeed
}

internal enum ConditionType {
    Blinded,
    Bleeding,
    Burning,
    Frozen,
    Poisoned,
    Silenced,
    Snared,
    Stunned
}

internal enum DamageType {
    Physical,
    Fire,
    Ice,
    Lightning,
    Poison,
    Magic
}

internal enum TargetType {
    Single,
    Aoe,
    Self
}

internal enum Skill {
    Swordsmanship,
    Stealth,
    Spellcasting,
    Archery,
    Devotion,
    Warfare
}

internal enum Profession {
    Knight,
    Rogue,
    Ranger,
    Mage,
    Cleric,
    Mercenary,
    Alchemist,
    Blacksmith,
    Scholar,
    Merchant
}

internal enum FactionRole {
    Leader,
    Member
}

internal enum QuestStatus {
    Accepted,
    Completed,
    Failed,
    Abandoned
}

internal enum QuestObjectiveType {
    Kill,
    Collect,
    Explore,
    Speak
}

internal enum QuestTargetType {
    Person,
    Item,
    City,
    Building,
    Race
}

internal enum WorkstationType {
    Alchemy,
    Armorsmithing,
    Cooking,
    Enchanting,
    Trade,
    Prayer,
    Reading,
    Weaponsmithing
}

internal enum BuildingType {
    ArcaneShop,
    Apothecary,
    Bakery,
    Blacksmith,
    Castle,
    GeneralGoods,
    GuildHall,
    House,
    Jail,
    Library,
    Stable,
    Tavern,
    Temple
}

internal enum JobAction {
    Sleep,
    Work,
    Idle,
    Patrol,
    Socialize
}