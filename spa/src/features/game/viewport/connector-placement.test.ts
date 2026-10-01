import { describe, expect, it } from 'vitest';

import type {
  BuildingLayoutWire,
  ConnectorLayoutWire,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { connectorYaw } from './connector-placement';
const size = { width: 20, depth: 20 };
const connector = (exitX: number, exitY: number): ConnectorLayoutWire => ({
  connectorId: 'door',
  destinationLocationId: 'room',
  exitX,
  exitY,
});
describe('connector orientation', () => {
  it.each([
    [10, 0, 0],
    [10, 20, Math.PI],
    [0, 10, Math.PI / 2],
    [20, 10, -Math.PI / 2],
  ])('faces into the room from (%s, %s)', (x, y, yaw) => {
    expect(connectorYaw(connector(x, y), size, [])).toBe(yaw);
  });
  it('faces out of a nearby building instead of towards the room centre', () => {
    const building = {
      placement: { x: 10, y: 10, angle: 0 },
      footprint: { width: 4, depth: 6 },
    } as BuildingLayoutWire;
    expect(connectorYaw(connector(12, 10), size, [building])).toBe(Math.PI / 2);
    expect(connectorYaw(connector(10, 7), size, [building])).toBe(Math.PI);
  });
  it('follows a rotated building facade', () => {
    const building = {
      placement: { x: 10, y: 10, angle: Math.PI / 4 },
      footprint: { width: 4, depth: 6 },
    } as BuildingLayoutWire;
    expect(connectorYaw(connector(10 + Math.SQRT2, 10 + Math.SQRT2), size, [building])).toBeCloseTo(
      Math.PI / 4,
    );
  });
});
