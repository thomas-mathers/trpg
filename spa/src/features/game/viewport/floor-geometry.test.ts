import type { BufferGeometry } from 'three';
import { describe, expect, it } from 'vitest';

import type { NearbyExitSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { floorGeometry } from './floor-geometry';
import { STAIR_DEPTH, STAIR_WIDTH } from './layout-math';

function surfaceArea(geometry: BufferGeometry) {
  const position = geometry.getAttribute('position');
  const index = geometry.getIndex();
  let area = 0;
  for (let i = 0; i < (index?.count ?? 0); i += 3) {
    const [a, b, c] = [0, 1, 2].map((offset) => {
      const vertex = index!.getX(i + offset);
      return { x: position.getX(vertex), y: position.getY(vertex) };
    });
    area += Math.abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) / 2;
  }
  return area;
}

const well = (x: number, y: number, angle: number) =>
  ({ placement: { x, y, angle } }) as NearbyExitSnapshot;

describe('floorGeometry', () => {
  const size = { width: 10, depth: 8 };

  it('covers the whole room when there are no wells', () => {
    // Act
    const floor = floorGeometry(size, []);

    // Assert
    expect(surfaceArea(floor)).toBeCloseTo(80);
  });

  it.each<[string, NearbyExitSnapshot, { width: number; depth: number }?]>([
    ['against the north wall', well(5, 0, Math.PI)],
    ['against the south wall', well(5, 8, 0)],
    ['against the west wall', well(0, 4, Math.PI / 2)],
    ['in the middle of the room', well(5, 3, Math.PI)],
    ['in a narrow room', well(1.125, 0, Math.PI), { width: 2.25, depth: 10 }],
    ['against the east wall', well(10, 4, (3 * Math.PI) / 2)],
  ])('removes exactly the stair footprint for a well %s', (_, stairs, room = size) => {
    // Act
    const floor = floorGeometry(room, [stairs]);

    // Assert
    expect(surfaceArea(floor)).toBeCloseTo(room.width * room.depth - STAIR_WIDTH * STAIR_DEPTH, 1);
  });
});

describe('floorGeometry well placement', () => {
  const size = { width: 10, depth: 8 };

  it('leaves no floor inside the well', () => {
    // Arrange
    const floor = floorGeometry(size, [well(5, 3, Math.PI)]);

    // Act
    const covered = coversPoint(floor, 5, -4);

    // Assert
    expect(covered).toBe(false);
  });

  it('keeps the floor right beside the well', () => {
    // Arrange
    const floor = floorGeometry(size, [well(5, 3, Math.PI)]);

    // Act
    const covered = coversPoint(floor, 5 + STAIR_WIDTH / 2 + 0.1, -4);

    // Assert
    expect(covered).toBe(true);
  });

  it('removes overlapping wells only once', () => {
    // Act
    const floor = floorGeometry(size, [well(5, 0, Math.PI), well(5, 0, Math.PI)]);

    // Assert
    expect(surfaceArea(floor)).toBeCloseTo(80 - STAIR_WIDTH * STAIR_DEPTH, 1);
  });
});

function coversPoint(geometry: BufferGeometry, x: number, y: number) {
  const position = geometry.getAttribute('position');
  const index = geometry.getIndex()!;
  const sign = (ax: number, ay: number, bx: number, by: number) =>
    (x - bx) * (ay - by) - (ax - bx) * (y - by);
  for (let i = 0; i < index.count; i += 3) {
    const [a, b, c] = [0, 1, 2].map((o) => [
      position.getX(index.getX(i + o)),
      position.getY(index.getX(i + o)),
    ]);
    const signs = [
      sign(a[0], a[1], b[0], b[1]),
      sign(b[0], b[1], c[0], c[1]),
      sign(c[0], c[1], a[0], a[1]),
    ];
    if (signs.every((s) => s >= 0) || signs.every((s) => s <= 0)) {
      return true;
    }
  }
  return false;
}
