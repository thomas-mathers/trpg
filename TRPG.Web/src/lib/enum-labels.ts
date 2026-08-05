import type {
  AmountType,
  AttributeName,
  CombatSpeedType,
  ItemType,
  ProcTrigger,
  ResourceType,
  SpecialHitType,
} from '@/api/client';

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

export const RESOURCE_LABEL: Record<ResourceType, string> = {
  Hp: 'HP',
  Ap: 'AP',
  Mp: 'MP',
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
};

export const COMBAT_SPEED_LABEL: Record<CombatSpeedType, string> = {
  IncreasedAttackSpeed: 'Increased Attack Speed',
  FasterCastRate: 'Faster Cast Rate',
  FasterHitRecovery: 'Faster Hit Recovery',
};

export const SPECIAL_HIT_LABEL: Record<SpecialHitType, string> = {
  CrushingBlow: 'Crushing Blow',
  DeadlyStrike: 'Deadly Strike',
  OpenWounds: 'Open Wounds',
};

export const PROC_TRIGGER_LABEL: Record<ProcTrigger, string> = {
  OnStriking: 'on Striking',
  WhenStruck: 'when Struck',
  OnKill: 'on Kill',
};

export function formatAmount(amount: number, amountType: AmountType): string {
  const sign = amount > 0 ? '+' : '';
  const unit = amountType === 'Percent' ? '%' : '';
  return `${sign}${amount}${unit}`;
}
