import type { EquippedGearSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export type Hand = 'left' | 'right';
export type WeaponStyle = {
  grip: number;
  head: [number, number, number];
  headColor: string;
};
export type ArmorPiece = 'Helm' | 'Chest' | 'Boots' | 'Gloves';
export type Gear = {
  armor: Partial<Record<ArmorPiece, string>>;
  weapons: { hand: Hand; style: WeaponStyle }[];
  shields: Hand[];
};

const ARMOR_COLORS: Record<string, string> = {
  Cloth: '#8d7a5c',
  Leather: '#6b4a30',
  Mail: '#8c949c',
  Plate: '#b4bcc6',
};

const STEEL = '#c3cad1';
const IRON = '#7b838b';
const WOOD = '#7a5a3a';

export const WEAPON_STYLES: Record<string, WeaponStyle> = {
  Dagger: { grip: 0.12, head: [0.04, 0.25, 0.015], headColor: STEEL },
  Sword: { grip: 0.15, head: [0.06, 0.7, 0.015], headColor: STEEL },
  GreatSword: { grip: 0.25, head: [0.09, 1.05, 0.02], headColor: STEEL },
  Axe: { grip: 0.7, head: [0.22, 0.18, 0.03], headColor: IRON },
  GreatAxe: { grip: 1.1, head: [0.34, 0.3, 0.03], headColor: IRON },
  Mace: { grip: 0.6, head: [0.14, 0.14, 0.14], headColor: IRON },
  Hammer: { grip: 0.7, head: [0.2, 0.14, 0.14], headColor: IRON },
  GreatHammer: { grip: 1.1, head: [0.32, 0.22, 0.22], headColor: IRON },
  Staff: { grip: 1.5, head: [0.08, 0.08, 0.08], headColor: '#8e6fb5' },
  Wand: { grip: 0.3, head: [0.03, 0.03, 0.03], headColor: '#8e6fb5' },
  Bow: { grip: 0.05, head: [0.03, 0.9, 0.05], headColor: WOOD },
  Crossbow: { grip: 0.1, head: [0.5, 0.05, 0.05], headColor: WOOD },
  Javelin: { grip: 1.4, head: [0.04, 0.16, 0.04], headColor: STEEL },
};

const ARMOR_MODEL = /^(Cloth|Leather|Mail|Plate)(Helm|Chest|Boots|Gloves)$/;

function handOf(slot: string): Hand | undefined {
  if (slot === 'LeftHand') return 'left';
  if (slot === 'RightHand') return 'right';
  return undefined;
}

export function resolveGear(equipment: readonly EquippedGearSnapshot[]): Gear {
  const gear: Gear = { armor: {}, weapons: [], shields: [] };
  for (const { slot, modelClass } of equipment) {
    const armor = ARMOR_MODEL.exec(modelClass);
    const hand = handOf(slot);
    if (armor) {
      gear.armor[armor[2] as ArmorPiece] = ARMOR_COLORS[armor[1]];
    } else if (hand && modelClass === 'Shield') {
      gear.shields.push(hand);
    } else if (hand && modelClass in WEAPON_STYLES) {
      gear.weapons.push({ hand, style: WEAPON_STYLES[modelClass] });
    }
  }
  return gear;
}
