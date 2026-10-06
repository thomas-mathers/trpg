import { Color, MathUtils } from 'three';

import type {
  FootprintWire,
  NearbyPropSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { WALL_HEIGHT } from './layout-math';
import { skyStateAt } from './sky-state';

export const LIGHT_SLOTS = 4;

const HEARTH_COLOR = '#ffb56a';
const INDOOR_FLAME_DAMPING = 0.85;
const LANTERN_DAMPING = 1;
const NIGHT_FLOOR = 0.2;
const DAY_PEAK = 0.8;
const MIN_INFLUENCE_DISTANCE = 0.5;
const INDOOR_GROUND = new Color('#6b5a48');

export interface LightSource {
  id: string;
  x: number;
  y: number;
  z: number;
  color: string;
  intensity: number;
  daylightDamping: number;
}

export interface LightSlotState {
  x: number;
  y: number;
  z: number;
  color: string;
  intensity: number;
}

export interface Focus {
  x: number;
  z: number;
}

const CANDLE_COLOR = '#ffc27a';
const CHANDELIER_DROP = 0.6;
const SCONCE_HEIGHT = 1.65;
const SCONCE_OFFSET = 0.3;
const STREET_LANTERN_HEIGHT = 2.8;
const WALL_LANTERN_HEIGHT = 1.5;
const WALL_LANTERN_OFFSET = 0.3;
const LANTERN_COLOR = '#ffca76';

function hearthSource({ id, model, placement, footprint }: NearbyPropSnapshot): LightSource {
  const isFireplace = model === 'FurnitureFireplace';
  const front = isFireplace ? footprint.depth / 2 + 0.15 : 0;
  return {
    id,
    x: placement.x + Math.sin(placement.angle) * front,
    y: 1,
    z: placement.y - Math.cos(placement.angle) * front,
    color: HEARTH_COLOR,
    intensity: 10,
    daylightDamping: 0,
  };
}

function chandelierSource({ id, placement }: NearbyPropSnapshot): LightSource {
  return {
    id,
    x: placement.x,
    y: WALL_HEIGHT - CHANDELIER_DROP,
    z: placement.y,
    color: CANDLE_COLOR,
    intensity: 7,
    daylightDamping: INDOOR_FLAME_DAMPING,
  };
}

function sconceSource({ id, placement }: NearbyPropSnapshot): LightSource {
  return {
    id,
    x: placement.x + Math.sin(placement.angle) * SCONCE_OFFSET,
    y: SCONCE_HEIGHT,
    z: placement.y - Math.cos(placement.angle) * SCONCE_OFFSET,
    color: CANDLE_COLOR,
    intensity: 3,
    daylightDamping: INDOOR_FLAME_DAMPING,
  };
}

function streetLanternSource({ id, placement }: NearbyPropSnapshot): LightSource {
  return {
    id,
    x: placement.x,
    y: STREET_LANTERN_HEIGHT,
    z: placement.y,
    color: LANTERN_COLOR,
    intensity: 3,
    daylightDamping: LANTERN_DAMPING,
  };
}

function wallLanternSource({ id, placement }: NearbyPropSnapshot): LightSource {
  return {
    id,
    x: placement.x + Math.sin(placement.angle) * WALL_LANTERN_OFFSET,
    y: WALL_LANTERN_HEIGHT,
    z: placement.y - Math.cos(placement.angle) * WALL_LANTERN_OFFSET,
    color: LANTERN_COLOR,
    intensity: 0.8,
    daylightDamping: LANTERN_DAMPING,
  };
}

function sourceFor(prop: NearbyPropSnapshot): LightSource | undefined {
  switch (prop.model) {
    case 'FurnitureFireplace':
    case 'FurnitureFirePit':
      return hearthSource(prop);
    case 'FurnitureChandelier':
      return chandelierSource(prop);
    case 'FurnitureWallSconce':
      return sconceSource(prop);
    case 'FurnitureStreetLantern':
      return streetLanternSource(prop);
    case 'FurnitureWallLantern':
      return wallLanternSource(prop);
    default:
      return undefined;
  }
}

export function lightSourcesFor(props: NearbyPropSnapshot[]): LightSource[] {
  return props.flatMap((prop) => sourceFor(prop) ?? []);
}

export function shadowReach({ width, depth }: FootprintWire) {
  return Math.hypot(width, depth, WALL_HEIGHT) + 1;
}

export function lightRig(sources: LightSource[], hour: number, focus: Focus): LightSlotState[] {
  const { daylight } = skyStateAt(hour);
  const strongest = sources
    .map((source) => {
      const intensity = source.intensity * (1 - daylight * source.daylightDamping);
      const distance = Math.hypot(source.x - focus.x, source.z - focus.z);
      return {
        source,
        intensity,
        influence: intensity / Math.max(distance, MIN_INFLUENCE_DISTANCE),
      };
    })
    .sort((a, b) => b.influence - a.influence || a.source.id.localeCompare(b.source.id))
    .slice(0, LIGHT_SLOTS)
    .sort((a, b) => b.source.y - a.source.y || a.source.id.localeCompare(b.source.id));

  return Array.from({ length: LIGHT_SLOTS }, (_, slot) => {
    const entry = strongest[slot];
    if (!entry) {
      return { x: 0, y: WALL_HEIGHT, z: 0, color: HEARTH_COLOR, intensity: 0 };
    }
    const { source, intensity } = entry;
    return {
      x: source.x,
      y: source.y,
      z: source.z,
      color: source.color,
      intensity,
    };
  });
}

export function indoorAmbientAt(hour: number) {
  const { daylight, ambient } = skyStateAt(hour);
  return {
    sky: ambient.sky,
    ground: INDOOR_GROUND.clone().lerp(ambient.ground, 0.3),
    intensity: MathUtils.lerp(NIGHT_FLOOR, DAY_PEAK, daylight),
  };
}
