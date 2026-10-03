import { describe, expect, it } from 'vitest';

import type { NearbyExitSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { connectorYaw } from './connector-placement';
describe('connector orientation', () => {
  it.each([0, Math.PI / 2, Math.PI, -Math.PI / 4])(
    'faces the visible front of the door into the location at angle %s',
    (angle) => {
      const connector = { placement: { x: 10, y: 10, angle } } as NearbyExitSnapshot;
      const yaw = connectorYaw(connector);

      expect(Math.sin(yaw)).toBeCloseTo(Math.sin(angle));
      expect(Math.cos(yaw)).toBeCloseTo(-Math.cos(angle));
    },
  );
});
