import { expect, it } from 'vitest';

import {
  doorFittingsGeometry,
  doorFittingsMaterial,
  doorTimberGeometry,
  doorTimberMaterial,
} from './door-connector';
import { GRAIN_ATTRIBUTE } from './wood-grain';

it('splits the door into grained timber and plain fittings', () => {
  doorTimberGeometry.computeBoundingBox();

  expect(doorTimberGeometry.getAttribute(GRAIN_ATTRIBUTE).count).toBe(
    doorTimberGeometry.getAttribute('position').count,
  );
  expect(doorFittingsGeometry.getAttribute('color').count).toBe(
    doorFittingsGeometry.getAttribute('position').count,
  );
  expect(doorTimberGeometry.groups).toHaveLength(0);
  expect(doorTimberMaterial.vertexColors).toBe(true);
  expect(doorFittingsMaterial.vertexColors).toBe(true);
  expect(doorTimberGeometry.boundingBox!.min.y).toBeLessThanOrEqual(0.5);
  expect(doorTimberGeometry.boundingBox!.max.y).toBeGreaterThan(2.5);
});
