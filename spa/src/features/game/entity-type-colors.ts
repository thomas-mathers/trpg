import type { EntityType } from '@/api/client';

export const ENTITY_TYPE_COLORS: Record<EntityType, string> = {
  Creature: 'oklch(0.5 0.15 55)',
  Building: 'oklch(0.42 0.08 75)',
  District: 'oklch(0.48 0.15 140)',
  World: 'oklch(0.5 0.13 190)',
  Country: 'oklch(0.48 0.15 250)',
  State: 'oklch(0.48 0.17 305)',
  City: 'oklch(0.52 0.17 350)',
  Item: 'oklch(0.48 0.18 290)',
};
