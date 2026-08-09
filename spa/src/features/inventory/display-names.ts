import type {
  AttributeName,
  CombatSpeedType,
  EquipmentSlot,
  ItemType,
  ProcTrigger,
  ResourceType,
  SpecialHitType,
} from '@/api/client';

import type { ItemCategory } from './item-visuals';

export const ATTRIBUTE_LABEL: Record<AttributeName, string> = {
  MaximumHp: 'Maximum HP',
  MaximumAp: 'Maximum AP',
  MaximumMp: 'Maximum MP',
  Strength: 'Strength',
  Defense: 'Defense',
  Dexterity: 'Dexterity',
  Endurance: 'Endurance',
  Stamina: 'Stamina',
  Mana: 'Mana',
  Intelligence: 'Intelligence',
  PhysicalResistance: 'Physical Resistance',
  FireResistance: 'Fire Resistance',
  IceResistance: 'Ice Resistance',
  LightningResistance: 'Lightning Resistance',
  PoisonResistance: 'Poison Resistance',
  MagicResistance: 'Magic Resistance',
  MovementSpeed: 'Movement Speed',
};

export const COMBAT_SPEED_LABEL: Record<CombatSpeedType, string> = {
  IncreasedAttackSpeed: 'Increased Attack Speed',
  FasterCastRate: 'Faster Cast Rate',
  FasterHitRecovery: 'Faster Hit Recovery',
};

export const EQUIPMENT_SLOT_LABEL: Record<EquipmentSlot, string> = {
  Helm: 'Helm',
  Chest: 'Chest',
  LeftHand: 'Left Hand',
  RightHand: 'Right Hand',
  Boots: 'Boots',
  Necklace: 'Necklace',
  Gloves: 'Gloves',
  LeftRing: 'Left Ring',
  RightRing: 'Right Ring',
  Belt: 'Belt',
};

export const ITEM_TYPE_LABEL: Record<ItemType, string> = {
  Dagger: 'Dagger',
  Sword: 'Sword',
  Axe: 'Axe',
  Mace: 'Mace',
  Hammer: 'Hammer',
  Staff: 'Staff',
  Wand: 'Wand',
  Bow: 'Bow',
  Crossbow: 'Crossbow',
  Javelin: 'Javelin',
  GreatSword: 'Great Sword',
  GreatAxe: 'Great Axe',
  GreatHammer: 'Great Hammer',
  Helm: 'Helm',
  Chest: 'Chest Armor',
  Boots: 'Boots',
  Gloves: 'Gloves',
  Arrow: 'Arrow',
  Bolt: 'Bolt',
  Ring: 'Ring',
  Necklace: 'Necklace',
  Belt: 'Belt',
  Shield: 'Shield',
  Consumable: 'Consumable',
  Gold: 'Gold',
  Key: 'Key',
};

export const PROC_TRIGGER_LABEL: Record<ProcTrigger, string> = {
  OnStriking: 'on Striking',
  WhenStruck: 'when Struck',
  OnKill: 'on Kill',
};

export const RESOURCE_LABEL: Record<ResourceType, string> = {
  Hp: 'HP',
  Ap: 'AP',
  Mp: 'MP',
};

export const SPECIAL_HIT_LABEL: Record<SpecialHitType, string> = {
  CrushingBlow: 'Crushing Blow',
  DeadlyStrike: 'Deadly Strike',
  OpenWounds: 'Open Wounds',
};

export const CATEGORY_LABEL: Record<ItemCategory, string> = {
  Weapon: 'Weapons',
  Shield: 'Shields',
  Armor: 'Armor',
  Accessory: 'Accessories',
  Ammunition: 'Ammo',
  Consumable: 'Consumables',
  Gold: 'Gold',
  Key: 'Keys',
};
