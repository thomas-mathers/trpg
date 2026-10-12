import type { BuildingType } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { buildingFacade } from './building-facade';
import { stringHash, varyColor } from './color-variation';

const ROOFLESS: ReadonlySet<BuildingType> = new Set([
  'Castle',
  'Tower',
  'Cave',
  'Mine',
  'Crypt',
  'Ruins',
]);

const ROOF_COLORS = ['#a0523c', '#8c4a3a', '#6e4a3a', '#4f5b66', '#9c8450'];

const OVERHANG = 0.4;
const MIN_RISE = 1.4;
const MAX_RISE = 4.2;
const RISE_RATIO = 0.5;
const CHIMNEY_SPAN_OFFSET = 0.18;
const CHIMNEY_LENGTH_OFFSET = 0.3;

export interface ChimneyPlacement {
  x: number;
  z: number;
}

export interface RoofShape {
  span: number;
  length: number;
  rise: number;
  yaw: number;
  color: string;
  chimney?: ChimneyPlacement;
}

function chimneyPlacement(id: string, span: number, length: number): ChimneyPlacement {
  const hash = stringHash(id);
  const side = hash % 2 === 0 ? 1 : -1;
  const end = Math.floor(hash / 2) % 2 === 0 ? 1 : -1;
  return {
    x: side * span * CHIMNEY_SPAN_OFFSET,
    z: end * length * CHIMNEY_LENGTH_OFFSET,
  };
}

export function buildingWallColor(color: string, id: string) {
  return varyColor(color, id, 1);
}

export function roofShape(
  id: string,
  type: BuildingType,
  { width, depth }: { width: number; depth: number },
): RoofShape | undefined {
  if (ROOFLESS.has(type)) return undefined;
  const alongDepth = width <= depth;
  const span = (alongDepth ? width : depth) + OVERHANG * 2;
  const length = (alongDepth ? depth : width) + OVERHANG * 2;
  return {
    span,
    length,
    rise: Math.min(MAX_RISE, Math.max(MIN_RISE, span * RISE_RATIO)),
    yaw: alongDepth ? 0 : Math.PI / 2,
    color: varyColor(ROOF_COLORS[stringHash(id) % ROOF_COLORS.length], id, 0.8),
    chimney: buildingFacade(type).chimney ? chimneyPlacement(id, span, length) : undefined,
  };
}
