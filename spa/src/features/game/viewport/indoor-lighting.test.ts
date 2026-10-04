import { Vector3 } from 'three/webgpu';
import { expect, it } from 'vitest';

import type { NearbyPropSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { createFireLight } from './indoor-fire-light';
import { WALL_HEIGHT } from './layout-math';

it('places a shadow-casting light in front of the fireplace and reaches the room', () => {
  const size = { width: 20, depth: 16 };
  const fireplace = {
    model: 'FurnitureFireplace',
    placement: { x: 1, y: 8, angle: Math.PI / 2 },
    footprint: { width: 1.5, depth: 0.6 },
  } as NearbyPropSnapshot;
  const light = createFireLight(fireplace, size);

  expect(light.castShadow).toBe(true);
  expect(light.position.x).toBeGreaterThan(fireplace.placement.x);
  expect(light.position.y).toBeLessThan(WALL_HEIGHT);
  for (const x of [0, size.width]) {
    for (const z of [0, size.depth]) {
      expect(light.shadow.camera.far).toBeGreaterThan(
        light.position.distanceTo(new Vector3(x, WALL_HEIGHT, z)),
      );
    }
  }
});
