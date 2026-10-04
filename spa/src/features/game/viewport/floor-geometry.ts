import { BufferGeometry, Float32BufferAttribute } from 'three';

import type {
  FootprintWire,
  NearbyExitSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { stairCorners } from './layout-math';

interface Rect {
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
}

const MIN_PIECE = 1e-4;

export function floorGeometry({ width, depth }: FootprintWire, wells: NearbyExitSnapshot[]) {
  const floor = wells
    .map(wellRect)
    .reduce<Rect[]>(
      (pieces, well) => pieces.flatMap((piece) => subtract(piece, well)),
      [{ minX: 0, maxX: width, minY: 0, maxY: depth }],
    );
  return toGeometry(floor);
}

function wellRect(well: NearbyExitSnapshot): Rect {
  const corners = stairCorners(well);
  const xs = corners.map(({ x }) => x);
  const ys = corners.map(({ y }) => y);
  return {
    minX: Math.min(...xs),
    maxX: Math.max(...xs),
    minY: Math.min(...ys),
    maxY: Math.max(...ys),
  };
}

function subtract(piece: Rect, hole: Rect): Rect[] {
  const overlaps =
    hole.minX < piece.maxX &&
    hole.maxX > piece.minX &&
    hole.minY < piece.maxY &&
    hole.maxY > piece.minY;
  if (!overlaps) {
    return [piece];
  }
  const bandMinY = Math.max(piece.minY, hole.minY);
  const bandMaxY = Math.min(piece.maxY, hole.maxY);
  return [
    { ...piece, maxY: bandMinY },
    { ...piece, minY: bandMaxY },
    { ...piece, maxX: hole.minX, minY: bandMinY, maxY: bandMaxY },
    { ...piece, minX: hole.maxX, minY: bandMinY, maxY: bandMaxY },
  ].filter(({ minX, maxX, minY, maxY }) => maxX - minX > MIN_PIECE && maxY - minY > MIN_PIECE);
}

function toGeometry(pieces: Rect[]) {
  const positions: number[] = [];
  const indices: number[] = [];
  pieces.forEach(({ minX, maxX, minY, maxY }, piece) => {
    positions.push(minX, -maxY, 0, maxX, -maxY, 0, maxX, -minY, 0, minX, -minY, 0);
    const first = piece * 4;
    indices.push(first, first + 1, first + 2, first, first + 2, first + 3);
  });
  const geometry = new BufferGeometry();
  geometry.setAttribute('position', new Float32BufferAttribute(positions, 3));
  geometry.setAttribute(
    'normal',
    new Float32BufferAttribute(
      positions.map((_, i) => (i % 3 === 2 ? 1 : 0)),
      3,
    ),
  );
  geometry.setIndex(indices);
  return geometry;
}
