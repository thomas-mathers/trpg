export const DISTRICTS = [
  'Residential',
  'CityCenter',
  'Encampment',
  'Scientific',
  'Governmental',
  'HolySite',
  'CityEntrance',
] as const;
export type DistrictKind = (typeof DISTRICTS)[number];
export const DISTRICT_NAMES: Record<DistrictKind, string> = {
  Residential: 'Residential',
  CityCenter: 'City center',
  Encampment: 'Encampment',
  Scientific: 'Scientific',
  Governmental: 'Governmental',
  HolySite: 'Holy site',
  CityEntrance: 'City entrance',
};
export const DISTRICT_DESCRIPTIONS: Record<DistrictKind, string> = {
  Residential: 'Households clustered around shared courts, connected by streets and narrow alleys.',
  CityCenter:
    'Seven businesses around the market, with an optional guild hall anchoring its northern edge.',
  Encampment: 'Barracks, forge and stables face a broad muster yard with a clear approach.',
  Scientific: 'The library anchors a quiet court shared with the arcane shop and apothecary.',
  Governmental: 'A castle forecourt, with the jail along a side approach.',
  HolySite: 'One large temple, a gathering court and gardens on either side.',
  CityEntrance: 'An open arrival square and through route. No buildings are generated here today.',
};

type BuildingSpec = {
  name: string;
  width: number;
  depth: number;
  color: string;
  floors: string[][];
};
export const BUILDING_SPECS = {
  House: {
    name: 'House',
    width: 10,
    depth: 12,
    color: '#d4b990',
    floors: [['Living Room'], ['Bedroom 1', 'Bedroom 2', 'Bedroom 3']],
  },
  GeneralGoods: {
    name: 'General goods',
    width: 11,
    depth: 15,
    color: '#c9aaa0',
    floors: [['Shop'], ['Living Quarters']],
  },
  Bakery: {
    name: 'Bakery',
    width: 12,
    depth: 14,
    color: '#e0be83',
    floors: [['Bakery'], ['Living Quarters']],
  },
  Tavern: {
    name: 'Tavern',
    width: 20,
    depth: 18,
    color: '#c6a276',
    floors: [['Common Room'], ["Owner's Quarters"]],
  },
  Inn: {
    name: 'Inn',
    width: 20,
    depth: 24,
    color: '#b7bb91',
    floors: [
      ['Lobby'],
      ['North Guest Room', 'South Guest Room', 'East Guest Room', 'West Guest Room'],
      ["Owner's Quarters"],
    ],
  },
  Tailor: {
    name: 'Tailor',
    width: 9,
    depth: 14,
    color: '#c3aec9',
    floors: [['Shop'], ['Living Quarters']],
  },
  Carpenter: {
    name: 'Carpenter',
    width: 16,
    depth: 20,
    color: '#bfa284',
    floors: [['Workshop'], ['Living Quarters']],
  },
  Jeweler: {
    name: 'Jeweler',
    width: 9,
    depth: 12,
    color: '#abc3c6',
    floors: [['Shop'], ['Living Quarters']],
  },
  GuildHall: {
    name: 'Guild hall',
    width: 25,
    depth: 22,
    color: '#a9b9c2',
    floors: [
      ['Hall'],
      ["Guild Master's Chamber", 'Member Room 1', 'Member Room 2', 'Member Room 3'],
    ],
  },
  Library: {
    name: 'Library',
    width: 24,
    depth: 26,
    color: '#c3b49b',
    floors: [['Reading Room'], ['Study']],
  },
  ArcaneShop: {
    name: 'Arcane shop',
    width: 12,
    depth: 16,
    color: '#b7a6c7',
    floors: [['Shop'], ['Living Quarters']],
  },
  Apothecary: {
    name: 'Apothecary',
    width: 12,
    depth: 15,
    color: '#b4c69a',
    floors: [['Shop'], ['Living Quarters']],
  },
  Castle: {
    name: 'Castle',
    width: 38,
    depth: 32,
    color: '#b8bec5',
    floors: [['Great Hall'], ['Royal Chambers']],
  },
  Jail: {
    name: 'Jail',
    width: 17,
    depth: 22,
    color: '#a6afa8',
    floors: [['Guard Station'], ['Cells']],
  },
  Temple: {
    name: 'Temple',
    width: 26,
    depth: 36,
    color: '#e1d6b4',
    floors: [['Sanctuary'], ['Quarters']],
  },
  Barracks: {
    name: 'Barracks',
    width: 30,
    depth: 22,
    color: '#b8b5a4',
    floors: [['Drill Hall'], ["Officer's Quarters", 'Barracks Dormitory']],
  },
  Blacksmith: {
    name: 'Blacksmith',
    width: 17,
    depth: 20,
    color: '#b29b92',
    floors: [['Workshop'], ['Living Quarters']],
  },
  Stable: {
    name: 'Stable',
    width: 28,
    depth: 13,
    color: '#c3ae83',
    floors: [['Stable'], ['Living Quarters']],
  },
} satisfies Record<string, BuildingSpec>;
export type PreviewBuildingType = keyof typeof BUILDING_SPECS;
export const ROSTERS: Record<Exclude<DistrictKind, 'Residential'>, PreviewBuildingType[]> = {
  CityCenter: [
    'GuildHall',
    'Inn',
    'Tavern',
    'GeneralGoods',
    'Bakery',
    'Tailor',
    'Carpenter',
    'Jeweler',
  ],
  Encampment: ['Barracks', 'Blacksmith', 'Stable'],
  Scientific: ['Library', 'ArcaneShop', 'Apothecary'],
  Governmental: ['Castle', 'Jail'],
  HolySite: ['Temple'],
  CityEntrance: [],
};

const SUITE_AREA = 24;
const BEDROOM_AREA = 16;
const DORMITORY_AREA = 40;
const ROYAL_AREA = 40;

export function upperRoomArea(name: string): number | undefined {
  if (/Dormitory/.test(name)) return DORMITORY_AREA;
  if (/Royal/.test(name)) return ROYAL_AREA;
  if (/Owner|Master|Officer|Living/.test(name)) return SUITE_AREA;
  if (/Bedroom|Guest Room|Member Room|^Quarters$/.test(name)) return BEDROOM_AREA;
  return undefined;
}
