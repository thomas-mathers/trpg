namespace TRPG.Models;

public enum EquipmentSlot { Helm, Chest, LeftHand, RightHand, Boots, Necklace, Gloves, LeftRing, RightRing, Belt }

public enum ItemCategory { Helm, Chest, Sword, Spear, Bow, Staff, Shield, Boots, Necklace, Gloves, Ring, Belt, Arrows, Consumable, Quest, CraftingMaterial }

public enum EffectStat { CurrentHp, MaximumHp, CurrentAp, MaximumAp, Strength, Defense, Dexterity, Endurance, Intelligence }

public enum EffectApplicationMode { Origin, Target }

public enum EffectType { Flat, Percent }

public enum FactionRole { Leader, Member }

public enum QuestStatus { Accepted, Completed, Failed, Abandoned }

public enum QuestObjectiveType { Kill, Collect, Explore, Speak }

public enum QuestTargetType { Person, Item, City, Building, Race }

public enum JobAction { Sleep, Work, Idle, Patrol, Socialize }
