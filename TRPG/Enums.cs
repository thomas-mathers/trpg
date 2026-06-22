namespace TRPG;

internal enum EquipmentSlot { Helm, Chest, LeftHand, RightHand, Boots, Necklace, Gloves, LeftRing, RightRing, Belt }

internal enum ItemCategory { Helm, Chest, Sword, Spear, Bow, Staff, Shield, Boots, Necklace, Gloves, Ring, Belt, Arrows, Consumable, Quest, CraftingMaterial }

internal enum EffectStat { CurrentHp, MaximumHp, CurrentAp, MaximumAp, Strength, Defense, Dexterity, Endurance, Intelligence }

internal enum EffectApplicationMode { Origin, Target }

internal enum EffectType { Flat, Percent }

internal enum FactionRole { Leader, Member }

internal enum QuestStatus { Accepted, Completed, Failed, Abandoned }

internal enum QuestObjectiveType { Kill, Collect, Explore, Speak }

internal enum QuestTargetType { Person, Item, City, Building, Race }

internal enum JobAction { Sleep, Work, Idle, Patrol, Socialize }
