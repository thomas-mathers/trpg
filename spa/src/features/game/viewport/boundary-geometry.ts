import type {
  CompassDirection,
  FootprintWire,
  PointWire,
  RoadClassSnapshot,
  RoadSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import type { PlanarPoint } from './layout-math';

export interface Ribbon {
  positions: number[];
  indices: number[];
}

const EDGE_SNAP = 0.8;
const ROAD_CLASS_RANK: Record<RoadClassSnapshot, number> = { Lane: 0, Street: 1, Avenue: 2 };
const APRON_LENGTH = 150;
const APRON_OVERLAP = 0.3;

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

export interface ApronQuad {
  key: string;
  corners: PlanarPoint[];
}

export function apronQuads(
  roads: RoadSnapshot[],
  size: FootprintWire,
  openEdges: CompassDirection[],
): ApronQuad[] {
  return roads.flatMap(({ points, width }) =>
    [points[0], points[points.length - 1]].flatMap((end) => {
      const normal = end && outwardNormal(end, size);
      if (!end || !normal || isOnOpenEdge(end, size, openEdges)) return [];
      const half = width / 2;
      const origin = { x: end.x - normal.x * APRON_OVERLAP, y: end.y - normal.y * APRON_OVERLAP };
      const from = { x: origin.x - normal.y * half, y: origin.y + normal.x * half };
      const to = { x: origin.x + normal.y * half, y: origin.y - normal.x * half };
      return [
        {
          key: `${end.x}:${end.y}`,
          corners: extrudedQuad(from, to, normal, APRON_LENGTH + APRON_OVERLAP),
        },
      ];
    }),
  );
}

export function roadRenderOrder(roadClass: RoadClassSnapshot): number {
  return -9 + ROAD_CLASS_RANK[roadClass];
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
    const startReach = index > 1 ? half : 0;
    const start = { x: from.x - along.x * startReach, y: from.y - along.y * startReach };
    const end = { x: to.x + along.x * half, y: to.y + along.y * half };
    const base = ribbon.positions.length / 3;

    for (const corner of [
      { x: start.x - across.x, y: start.y - across.y },
      { x: start.x + across.x, y: start.y + across.y },
      { x: end.x + across.x, y: end.y + across.y },
      { x: end.x - across.x, y: end.y - across.y },
    ]) {
      ribbon.positions.push(corner.x, 0, corner.y);
    }
    ribbon.indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
  }

  return ribbon;
}
