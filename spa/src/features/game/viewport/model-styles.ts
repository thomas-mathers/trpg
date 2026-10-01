import type { BuildingType, PropModel } from '@/api/signalr-client/TRPG.GameSessions.Responses';

export interface BoxStyle {
  color: string;
  height: number;
}

const WOOD = '#8a6b43';
const STONE = '#9a948a';
const METAL = '#6f7782';

export const PROP_STYLES: Record<PropModel, BoxStyle> = {
  Bed: { color: '#a85f5f', height: 0.6 },
  Cell: { color: METAL, height: 2.5 },
  Sign: { color: WOOD, height: 1.6 },
  ContainerBasic: { color: WOOD, height: 0.8 },
  ContainerBarrel: { color: '#7a5532', height: 1 },
  ContainerChest: { color: '#9c7a3c', height: 0.7 },
  ContainerCrate: { color: '#a58355', height: 0.8 },
  ContainerFootlocker: { color: '#7d6a4a', height: 0.5 },
  ContainerStrongbox: { color: METAL, height: 0.5 },
  ContainerWeaponRack: { color: '#6b5236', height: 1.5 },
  SeatBasic: { color: WOOD, height: 0.5 },
  SeatChair: { color: WOOD, height: 0.9 },
  SeatPew: { color: '#74502f', height: 0.9 },
  SeatThrone: { color: '#c9a227', height: 1.6 },
  SeatBench: { color: WOOD, height: 0.5 },
  SeatStoneBench: { color: STONE, height: 0.5 },
  SeatLowWall: { color: STONE, height: 0.6 },
  TrapMechanical: { color: '#b04a3a', height: 0.15 },
  TrapCollapse: { color: '#8c7b66', height: 0.15 },
  TrapSlope: { color: '#8c7b66', height: 0.15 },
  TrapWater: { color: '#4a7fb0', height: 0.15 },
  TriggerBasic: { color: METAL, height: 0.4 },
  TriggerLever: { color: METAL, height: 1.1 },
  WorkstationAlchemy: { color: '#5f8a6b', height: 1 },
  WorkstationArmorsmithing: { color: METAL, height: 1 },
  WorkstationCarpentry: { color: WOOD, height: 1 },
  WorkstationCooking: { color: '#b0763a', height: 1 },
  WorkstationEnchanting: { color: '#7a5fa8', height: 1 },
  WorkstationJewelcrafting: { color: '#3fa6a0', height: 1 },
  WorkstationPrayer: { color: '#d8d2b8', height: 1 },
  WorkstationReading: { color: '#6b5236', height: 1.1 },
  WorkstationTailoring: { color: '#a85f8a', height: 1 },
  WorkstationTrade: { color: '#b89a4a', height: 1 },
  WorkstationWeaponsmithing: { color: METAL, height: 1 },
};

export const BUILDING_STYLES: Record<BuildingType, BoxStyle> = {
  ArcaneShop: { color: '#6f5fa0', height: 4 },
  Apothecary: { color: '#5f8a6b', height: 4 },
  Bakery: { color: '#c19a5a', height: 4 },
  Barracks: { color: '#7c7468', height: 4.5 },
  Blacksmith: { color: '#5c5f66', height: 4 },
  Carpenter: { color: '#9a7a4a', height: 4 },
  Castle: { color: '#8a8a92', height: 12 },
  Cave: { color: '#5a5048', height: 3 },
  Crypt: { color: '#6a6a6a', height: 3 },
  GeneralGoods: { color: '#a58a62', height: 4 },
  GuildHall: { color: '#8a6a4a', height: 5 },
  House: { color: '#b4a58c', height: 4 },
  Inn: { color: '#a8794a', height: 5 },
  Jail: { color: '#66676e', height: 4 },
  Jeweler: { color: '#4f9aa0', height: 4 },
  Library: { color: '#7a5a4a', height: 5 },
  Mine: { color: '#5a5048', height: 3 },
  Ruins: { color: '#7a766c', height: 2.5 },
  Stable: { color: '#8a7048', height: 3.5 },
  Tailor: { color: '#a0608a', height: 4 },
  Tavern: { color: '#9a6a3a', height: 5 },
  Temple: { color: '#d0cab4', height: 7 },
  Tower: { color: '#8a8a92', height: 10 },
};

export const PROP_MODEL_URLS: Partial<Record<PropModel, string>> = {};

export const BUILDING_MODEL_URLS: Partial<Record<BuildingType, string>> = {};
