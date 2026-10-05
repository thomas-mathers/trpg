import { describe, expect, it } from 'vitest';

import { terrainGeometry } from './floor-geometry';
import { outdoorFogRange } from './outdoor-fog';

describe('outdoorFogRange', () => {
  it('starts the fog sooner for a small district than a large one', () => {
    // Act
    const small = outdoorFogRange({ width: 10, depth: 10 });
    const large = outdoorFogRange({ width: 200, depth: 200 });

    // Assert
    expect(small.near).toBeLessThan(large.near);
  });

  it('ends the fog after it starts', () => {
    // Act
    const { near, far } = outdoorFogRange({ width: 30, depth: 40 });

    // Assert
    expect(far).toBeGreaterThan(near);
  });
});

describe('terrainGeometry', () => {
  it('surrounds the district out to the extent', () => {
    // Act
    const geometry = terrainGeometry({ width: 30, depth: 40 }, 500, 0.3);
    geometry.computeBoundingBox();

    // Assert
    expect(geometry.boundingBox?.min.x).toBe(-500);
    expect(geometry.boundingBox?.max.x).toBe(530);
  });
});
