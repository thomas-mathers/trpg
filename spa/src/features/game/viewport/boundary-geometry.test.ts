import { describe, expect, it } from 'vitest';

import {
  extrudedQuad,
  isOnOpenEdge,
  openEdgeSegment,
  outwardNormal,
  roadRibbon,
} from './boundary-geometry';

const size = { width: 40, depth: 30 };

describe('outwardNormal', () => {
  it.each([
    [
      { x: 20, y: 0 },
      { x: 0, y: -1 },
    ],
    [
      { x: 20, y: 30 },
      { x: 0, y: 1 },
    ],
    [
      { x: 0, y: 12 },
      { x: -1, y: 0 },
    ],
    [
      { x: 40, y: 12 },
      { x: 1, y: 0 },
    ],
  ])('points away from the edge that %j sits on', (point, expected) => {
    // Act
    const normal = outwardNormal(point, size);

    // Assert
    expect(normal).toEqual(expected);
  });

  it('is undefined for a point away from every edge', () => {
    // Act
    const normal = outwardNormal({ x: 20, y: 15 }, size);

    // Assert
    expect(normal).toBeUndefined();
  });
});

describe('isOnOpenEdge', () => {
  it('is true for a point on an open edge', () => {
    // Act
    const open = isOnOpenEdge({ x: 20, y: 30 }, size, ['South']);

    // Assert
    expect(open).toBe(true);
  });

  it('is false for a point on a walled edge', () => {
    // Act
    const open = isOnOpenEdge({ x: 20, y: 0 }, size, ['South']);

    // Assert
    expect(open).toBe(false);
  });

  it('is false for a point away from every edge', () => {
    // Act
    const open = isOnOpenEdge({ x: 20, y: 15 }, size, ['North', 'South', 'East', 'West']);

    // Assert
    expect(open).toBe(false);
  });
});

describe('openEdgeSegment', () => {
  it('spans the full width along the north edge', () => {
    // Act
    const segment = openEdgeSegment('North', size);

    // Assert
    expect(segment).toEqual([
      { x: 0, y: 0 },
      { x: 40, y: 0 },
    ]);
  });

  it('spans the full depth along the east edge', () => {
    // Act
    const segment = openEdgeSegment('East', size);

    // Assert
    expect(segment).toEqual([
      { x: 40, y: 0 },
      { x: 40, y: 30 },
    ]);
  });

  it('has no segment for a diagonal direction', () => {
    // Act
    const segment = openEdgeSegment('Northeast', size);

    // Assert
    expect(segment).toBeUndefined();
  });
});

describe('extrudedQuad', () => {
  it('pushes both ends of a segment out along the normal', () => {
    // Act
    const corners = extrudedQuad({ x: 0, y: 0 }, { x: 4, y: 0 }, { x: 0, y: -1 }, 6);

    // Assert
    expect(corners).toEqual([
      { x: 0, y: 0 },
      { x: 4, y: 0 },
      { x: 4, y: -6 },
      { x: 0, y: -6 },
    ]);
  });
});

describe('roadRibbon', () => {
  it('has no geometry for a single point', () => {
    // Act
    const ribbon = roadRibbon([{ x: 1, y: 1 }], 3);

    // Assert
    expect(ribbon.indices).toHaveLength(0);
  });

  it('lays a quad of the road width along each segment', () => {
    // Act
    const ribbon = roadRibbon(
      [
        { x: 0, y: 0 },
        { x: 10, y: 0 },
      ],
      4,
    );

    // Assert
    const zs = ribbon.positions.filter((_, index) => index % 3 === 2);
    expect(Math.min(...zs)).toBeCloseTo(-2);
    expect(Math.max(...zs)).toBeCloseTo(2);
    expect(ribbon.indices).toHaveLength(6);
  });

  it('overlaps the corner of a turn so no gap shows', () => {
    // Act
    const ribbon = roadRibbon(
      [
        { x: 0, y: 0 },
        { x: 10, y: 0 },
        { x: 10, y: 10 },
      ],
      4,
    );

    // Assert
    const xs = ribbon.positions.filter((_, index) => index % 3 === 0);
    expect(Math.max(...xs)).toBeCloseTo(12);
    expect(ribbon.indices).toHaveLength(12);
  });
});
