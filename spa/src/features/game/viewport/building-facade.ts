import { Color } from 'three';

import type { BuildingType } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { varyColor } from './color-variation';

export interface BuildingFacade {
  timbered: boolean;
  plinth: number;
  chimney: boolean;
}

const STONE: BuildingFacade = { timbered: false, plinth: Infinity, chimney: false };
const TIMBERED: BuildingFacade = { timbered: true, plinth: 0.9, chimney: false };
const HEARTH: BuildingFacade = { ...TIMBERED, chimney: true };

const FACADES: Record<BuildingType, BuildingFacade> = {
  ArcaneShop: TIMBERED,
  Apothecary: HEARTH,
  Bakery: HEARTH,
  Barracks: STONE,
  Blacksmith: { timbered: true, plinth: 1.1, chimney: true },
  Carpenter: TIMBERED,
  Castle: STONE,
  Cave: STONE,
  Crypt: STONE,
  GeneralGoods: TIMBERED,
  GuildHall: TIMBERED,
  House: HEARTH,
  Inn: HEARTH,
  Jail: STONE,
  Jeweler: TIMBERED,
  Library: TIMBERED,
  Mine: STONE,
  Ruins: STONE,
  Stable: TIMBERED,
  Tailor: TIMBERED,
  Tavern: HEARTH,
  Temple: STONE,
  Tower: STONE,
};

export function buildingFacade(type: BuildingType): BuildingFacade {
  return FACADES[type];
}

const WHITEWASH = '#ece3cc';
const WHITEWASH_WEIGHT = 0.6;
const PLINTH_STONE = '#8b867c';
const OAK = '#5a4129';

export interface FacadePalette {
  plaster: string;
  stone: string;
  timber: string;
}

export function facadePalette(type: BuildingType, wallColor: string, id: string): FacadePalette {
  if (!buildingFacade(type).timbered) {
    return { plaster: wallColor, stone: wallColor, timber: OAK };
  }
  const plaster = new Color(wallColor).lerp(new Color(WHITEWASH), WHITEWASH_WEIGHT);
  return {
    plaster: `#${plaster.getHexString()}`,
    stone: varyColor(PLINTH_STONE, id, 0.6),
    timber: varyColor(OAK, id, 0.5),
  };
}
