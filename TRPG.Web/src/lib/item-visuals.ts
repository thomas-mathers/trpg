import {
  BowArrow,
  Coins,
  FlaskConical,
  Footprints,
  Gem,
  Hand,
  HardHat,
  Shield,
  Shirt,
  Sword,
  type LucideIcon,
} from 'lucide-react';

import type { ItemRarity, ItemType } from '@/api/client';

export const TYPE_ICON: Record<ItemType, LucideIcon> = {
  Dagger: Sword,
  Sword: Sword,
  Axe: Sword,
  Mace: Sword,
  Hammer: Sword,
  Staff: Sword,
  Wand: Sword,
  Bow: BowArrow,
  Crossbow: BowArrow,
  Javelin: Sword,
  GreatSword: Sword,
  GreatAxe: Sword,
  GreatHammer: Sword,
  Helm: HardHat,
  Chest: Shirt,
  Boots: Footprints,
  Gloves: Hand,
  Arrow: BowArrow,
  Bolt: BowArrow,
  Ring: Gem,
  Necklace: Gem,
  Belt: Gem,
  Shield: Shield,
  Consumable: FlaskConical,
  Gold: Coins,
};

export const RARITY_COLOR: Partial<Record<ItemRarity, string>> = {
  Magic: 'var(--rarity-magic)',
  Rare: 'var(--rarity-rare)',
  Unique: 'var(--rarity-unique)',
};
