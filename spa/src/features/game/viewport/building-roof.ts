import type { BuildingType } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { stringHash, varyColor } from './color-variation';

const ROOFLESS: ReadonlySet<BuildingType> = new Set([
  'Castle',
  'Tower',
  'Cave',
  'Mine',
  'Crypt',
  'Ruins',
]);

const ROOF_COLORS = ['#8c4a3a', '#6e4a3a', '#4f5b66', '#7a6a52', '#5e6b4a'];

const OVERHANG = 0.3;
const MIN_RISE = 1;
const MAX_RISE = 2.5;
const RISE_RATIO = 0.3;

export interface RoofShape {
  span: number;
  length: number;
  rise: number;
  yaw: number;
  color: string;
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
  };
}
