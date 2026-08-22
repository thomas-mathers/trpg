import type { IconType } from 'react-icons';
import {
  GiArrowed,
  GiArrowhead,
  GiBattleAxe,
  GiBelt,
  GiBoots,
  GiBowArrow,
  GiBroadsword,
  GiBubblingFlask,
  GiChestArmor,
  GiClosedBarbute,
  GiCoinsPile,
  GiCrossbow,
  GiCrystalWand,
  GiDiamondRing,
  GiFlangedMace,
  GiFlatHammer,
  GiGauntlet,
  GiIntricateNecklace,
  GiKey,
  GiPlainDagger,
  GiShield,
  GiThrownSpear,
  GiTwoHandedSword,
  GiWarAxe,
  GiWarhammer,
  GiWizardStaff,
} from 'react-icons/gi';

import type { ItemDetail, ItemRarity, ItemType } from '@/api/client';

export const TYPE_ICON: Record<ItemType, IconType> = {
  Dagger: GiPlainDagger,
  Sword: GiBroadsword,
  Axe: GiWarAxe,
  Mace: GiFlangedMace,
  Hammer: GiFlatHammer,
  Staff: GiWizardStaff,
  Wand: GiCrystalWand,
  Bow: GiBowArrow,
  Crossbow: GiCrossbow,
  Javelin: GiThrownSpear,
  GreatSword: GiTwoHandedSword,
  GreatAxe: GiBattleAxe,
  GreatHammer: GiWarhammer,
  Helm: GiClosedBarbute,
  Chest: GiChestArmor,
  Boots: GiBoots,
  Gloves: GiGauntlet,
  Arrow: GiArrowhead,
  Bolt: GiArrowed,
  Ring: GiDiamondRing,
  Necklace: GiIntricateNecklace,
  Belt: GiBelt,
  Shield: GiShield,
  Consumable: GiBubblingFlask,
  Gold: GiCoinsPile,
  Key: GiKey,
};

export const RARITY_COLOR: Partial<Record<ItemRarity, string>> = {
  Magic: 'var(--rarity-magic)',
  Rare: 'var(--rarity-rare)',
  Unique: 'var(--rarity-unique)',
};

export type ItemCategory = ItemDetail['$type'];

export const CATEGORY_ORDER: ItemCategory[] = [
  'Weapon',
  'Shield',
  'Armor',
  'Accessory',
  'Ammunition',
  'Consumable',
  'Gold',
  'Key',
];
