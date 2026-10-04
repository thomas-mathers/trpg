import type { PreviewBuildingType } from './district-catalog';
import type { ScaleProp } from './preview-population';

export type Furnishing = Omit<ScaleProp, 'x' | 'y'>;
export const FURNITURE = {
  bed: { name: 'Bed', width: 1, depth: 2, shape: 'bed' },
  chest: { name: 'Chest', width: 0.9, depth: 0.6, shape: 'box' },
  barrel: { name: 'Barrel', width: 0.6, depth: 0.6, shape: 'barrel' },
  crate: { name: 'Crate', width: 0.7, depth: 0.7, shape: 'box' },
  bench: { name: 'Bench', width: 1.5, depth: 0.5, shape: 'seat' },
  chair: { name: 'Chair', width: 0.5, depth: 0.5, shape: 'seat' },
  pew: { name: 'Pew', width: 2, depth: 0.6, shape: 'seat' },
  counter: { name: 'Counter', width: 1.8, depth: 0.8, shape: 'table' },
  table: { name: 'Dining table', width: 1.8, depth: 1, shape: 'table' },
  bookcase: { name: 'Bookcase', width: 1, depth: 0.7, shape: 'shelf' },
  altar: { name: 'Altar', width: 1.2, depth: 0.8, shape: 'altar' },
  hearth: { name: 'Fireplace', width: 1.4, depth: 0.8, shape: 'hearth' },
  rack: { name: 'Weapon rack', width: 1.2, depth: 0.4, shape: 'shelf' },
  cell: { name: 'Cell', width: 2, depth: 2, shape: 'cell' },
  stall: { name: 'Stall', width: 2.4, depth: 2.4, shape: 'cell' },
  board: { name: 'Notice board', width: 2, depth: 0.25, shape: 'shelf' },
  shelf: { name: 'Display shelf', width: 1.2, depth: 0.5, shape: 'shelf' },
  dummy: { name: 'Training dummy', width: 0.7, depth: 0.7, shape: 'barrel' },
  workTable: { name: 'Work table', width: 2.4, depth: 1.2, shape: 'table' },
  throne: { name: 'Throne', width: 0.9, depth: 0.9, shape: 'seat' },
  rug: { name: 'Rug', width: 1.6, depth: 2.4, shape: 'rug' },
  cauldron: { name: 'Cauldron', width: 0.9, depth: 0.9, shape: 'barrel' },
  herbRack: { name: 'Herb rack', width: 1.4, depth: 0.4, shape: 'shelf' },
  lectern: { name: 'Spellbook lectern', width: 0.7, depth: 0.6, shape: 'altar' },
  staffRack: { name: 'Staff rack', width: 1.2, depth: 0.4, shape: 'shelf' },
  mannequin: { name: 'Mannequin', width: 0.5, depth: 0.5, shape: 'barrel' },
  clothShelf: { name: 'Cloth shelf', width: 1.4, depth: 0.5, shape: 'shelf' },
  displayCase: { name: 'Display case', width: 1.4, depth: 0.6, shape: 'table' },
  flour: { name: 'Flour sacks', width: 0.8, depth: 0.6, shape: 'box' },
  breadRack: { name: 'Bread rack', width: 1.4, depth: 0.5, shape: 'shelf' },
  lumber: { name: 'Lumber stack', width: 1.8, depth: 0.7, shape: 'box' },
  timberRack: { name: 'Timber rack', width: 1.4, depth: 0.4, shape: 'shelf' },
} satisfies Record<string, Furnishing>;

export const SQUARE_FEATURES: Record<string, Furnishing> = {
  'Market square': { name: 'Fountain', width: 3.2, depth: 3.2, shape: 'fountain' },
  'Shared green': { name: 'Well', width: 1.4, depth: 1.4, shape: 'fountain' },
  'Muster yard': { name: 'Fire pit', width: 1.6, depth: 1.6, shape: 'fire' },
  'Scholars’ court': { name: 'Statue', width: 1.2, depth: 1.2, shape: 'pillar' },
  'Castle forecourt': { name: 'Monument', width: 1.8, depth: 1.8, shape: 'pillar' },
  'Temple court': { name: 'Shrine', width: 1.4, depth: 1.4, shape: 'pillar' },
  'Arrival square': { name: 'Waystone', width: 1.2, depth: 1.2, shape: 'pillar' },
};

type StockPair = { left?: Furnishing; right: Furnishing };
export const SOUTH_STOCK: Partial<Record<PreviewBuildingType, StockPair>> = {
  Apothecary: { left: FURNITURE.cauldron, right: FURNITURE.herbRack },
  ArcaneShop: { left: FURNITURE.lectern, right: FURNITURE.staffRack },
  Tailor: { left: FURNITURE.mannequin, right: FURNITURE.clothShelf },
  Jeweler: { right: FURNITURE.displayCase },
  Bakery: { left: FURNITURE.flour, right: FURNITURE.breadRack },
  Carpenter: { left: FURNITURE.lumber, right: FURNITURE.timberRack },
};

export function workshopStation(type: PreviewBuildingType): Furnishing {
  switch (type) {
    case 'Blacksmith':
      return { name: 'Forge', width: 1.4, depth: 1.2, shape: 'hearth' };
    case 'Apothecary':
      return { name: 'Alchemy table', width: 1.2, depth: 0.7, shape: 'alchemy' };
    case 'ArcaneShop':
      return { name: 'Enchanting table', width: 1.2, depth: 1.2, shape: 'altar' };
    case 'Bakery':
      return { ...FURNITURE.hearth, name: 'Oven' };
    case 'Carpenter':
      return { name: 'Workbench', width: 1.6, depth: 0.8, shape: 'table' };
    case 'Jeweler':
      return { name: "Jeweler's bench", width: 1.2, depth: 0.7, shape: 'table' };
    case 'Tailor':
      return { name: 'Cutting table', width: 1.4, depth: 0.8, shape: 'table' };
    default:
      return FURNITURE.counter;
  }
}
