import type { CreatureType } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { stringHash } from './color-variation';

type Feature = 'none' | 'pointed-ears' | 'beard' | 'horns';

interface SpeciesStyle {
  height: number;
  skin: string;
  feature: Feature;
  elderAge: number;
}

const SPECIES: Record<CreatureType, SpeciesStyle> = {
  Human: { height: 1, skin: '#c39474', feature: 'none', elderAge: 60 },
  Elf: { height: 1.08, skin: '#d7b092', feature: 'pointed-ears', elderAge: 120 },
  Dwarf: { height: 0.79, skin: '#aa795f', feature: 'beard', elderAge: 100 },
  Orc: { height: 1.12, skin: '#748b62', feature: 'none', elderAge: 55 },
  Halfling: { height: 0.72, skin: '#c39a77', feature: 'pointed-ears', elderAge: 75 },
  Gnome: { height: 0.68, skin: '#c8a184', feature: 'pointed-ears', elderAge: 100 },
  Undead: { height: 1, skin: '#9d9c8e', feature: 'none', elderAge: 200 },
  Demon: { height: 1.12, skin: '#9b5950', feature: 'horns', elderAge: 200 },
  Beast: { height: 0.9, skin: '#7a6750', feature: 'none', elderAge: 30 },
  Construct: { height: 1.13, skin: '#8a9294', feature: 'none', elderAge: 200 },
  Elemental: { height: 1.06, skin: '#789eb7', feature: 'none', elderAge: 200 },
  Goblin: { height: 0.72, skin: '#778b55', feature: 'pointed-ears', elderAge: 45 },
  Wraith: { height: 1.02, skin: '#899da6', feature: 'none', elderAge: 200 },
  Giant: { height: 1.6, skin: '#ad8b73', feature: 'none', elderAge: 100 },
  Dragon: { height: 1.45, skin: '#8f5f4e', feature: 'horns', elderAge: 200 },
};

const GARMENTS = ['#765f4d', '#637078', '#745954', '#6a6850', '#625d72', '#6d5848'];

function garmentColor(id: string) {
  return GARMENTS[stringHash(id) % GARMENTS.length];
}

export function creatureAppearance(id: string, creatureType: CreatureType, age: number) {
  const species = SPECIES[creatureType];
  const ageScale = age < 12 ? 0.72 : age < 18 ? 0.88 : age >= species.elderAge ? 0.95 : 1;
  return {
    height: species.height * ageScale,
    skin: species.skin,
    garment: garmentColor(id),
    feature: species.feature,
    elderly: age >= species.elderAge,
  };
}

export type CreatureAppearance = ReturnType<typeof creatureAppearance>;
