import type {
  CompassDirection,
  FootprintWire,
  PointWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { PlanarPoint } from './layout-math';

export interface Ribbon {
  positions: number[];
  indices: number[];
}

const EDGE_SNAP = 0.8;
const ROAD_LIFT = 0.03;

export function outwardNormal(point: PlanarPoint, size: FootprintWire): PlanarPoint | undefined {
  const { width, depth } = size;
  if (Math.abs(point.y) <= EDGE_SNAP) return { x: 0, y: -1 };
  if (Math.abs(point.y - depth) <= EDGE_SNAP) return { x: 0, y: 1 };
  if (Math.abs(point.x) <= EDGE_SNAP) return { x: -1, y: 0 };
  if (Math.abs(point.x - width) <= EDGE_SNAP) return { x: 1, y: 0 };
  return undefined;
}

export function isOnOpenEdge(
  point: PlanarPoint,
  size: FootprintWire,
  openEdges: CompassDirection[],
): boolean {
  const normal = outwardNormal(point, size);
  if (!normal) return false;
  const direction: CompassDirection =
    normal.y < 0 ? 'North' : normal.y > 0 ? 'South' : normal.x < 0 ? 'West' : 'East';
  return openEdges.includes(direction);
}

export function openEdgeSegment(
  direction: CompassDirection,
  { width, depth }: FootprintWire,
): [PlanarPoint, PlanarPoint] | undefined {
  switch (direction) {
    case 'North':
      return [
        { x: 0, y: 0 },
        { x: width, y: 0 },
      ];
    case 'South':
      return [
        { x: 0, y: depth },
        { x: width, y: depth },
      ];
    case 'West':
      return [
        { x: 0, y: 0 },
        { x: 0, y: depth },
      ];
    case 'East':
      return [
        { x: width, y: 0 },
        { x: width, y: depth },
      ];
    default:
      return undefined;
  }
}

export function extrudedQuad(
  from: PlanarPoint,
  to: PlanarPoint,
  normal: PlanarPoint,
  depth: number,
): PlanarPoint[] {
  const dx = normal.x * depth;
  const dy = normal.y * depth;
  return [from, to, { x: to.x + dx, y: to.y + dy }, { x: from.x + dx, y: from.y + dy }];
}

export function roadRibbon(points: PointWire[], width: number): Ribbon {
  const ribbon: Ribbon = { positions: [], indices: [] };
  const half = width / 2;

  for (let index = 1; index < points.length; index++) {
    const from = points[index - 1];
    const to = points[index];
    const length = Math.hypot(to.x - from.x, to.y - from.y);
    if (length === 0) continue;

    const along = { x: (to.x - from.x) / length, y: (to.y - from.y) / length };
    const across = { x: -along.y * half, y: along.x * half };
    const start = { x: from.x - along.x * half, y: from.y - along.y * half };
    const end = { x: to.x + along.x * half, y: to.y + along.y * half };
    const base = ribbon.positions.length / 3;

    for (const corner of [
      { x: start.x - across.x, y: start.y - across.y },
      { x: start.x + across.x, y: start.y + across.y },
      { x: end.x + across.x, y: end.y + across.y },
      { x: end.x - across.x, y: end.y - across.y },
    ]) {
      ribbon.positions.push(corner.x, ROAD_LIFT, corner.y);
    }
    ribbon.indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
  }

  return ribbon;
}
