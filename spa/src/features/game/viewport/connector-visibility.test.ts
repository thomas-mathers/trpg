import { describe, expect, it } from 'vitest';

import type {
  LocationBoundarySnapshot,
  NearbyExitSnapshot,
} from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { buildingNameBoards, hasDoor } from './connector-visibility';

const SIZE = { width: 60, depth: 60 };

function exit(
  kind: 'District' | 'Building' | 'Room' | 'Wilderness',
  x: number,
  y: number,
  overrides: Partial<NearbyExitSnapshot> = {},
): NearbyExitSnapshot {
  return {
    connectorId: `${kind}:${x}:${y}`,
    destination: { $type: kind, name: `${kind} name` },
    placement: { x, y, angle: 0 },
    ...overrides,
  } as NearbyExitSnapshot;
}

function boundary(overrides: Partial<LocationBoundarySnapshot> = {}): LocationBoundarySnapshot {
  return { segments: [], gates: [], openEdges: [], ...overrides };
}

describe('hasDoor', () => {
  it('keeps the door on a building facade', () => {
    const door = exit('Building', 30, 30);

    expect(hasDoor(door, boundary({ openEdges: ['South'] }), SIZE)).toBe(true);
  });

  it('drops the door on a district connection along an open edge', () => {
    const connection = exit('District', 30, 60);

    expect(hasDoor(connection, boundary({ openEdges: ['South'] }), SIZE)).toBe(false);
  });

  it('keeps the door on a district connection along a walled edge', () => {
    const connection = exit('District', 30, 60);

    expect(hasDoor(connection, boundary({ openEdges: ['North'] }), SIZE)).toBe(true);
  });

  it('drops the door on a wall gate', () => {
    const gate = exit('Wilderness', 0, 30);
    const walled = boundary({
      gates: [{ connectorId: gate.connectorId, placement: gate.placement, width: 3 }],
    });

    expect(hasDoor(gate, walled, SIZE)).toBe(false);
  });

  it('keeps every door when the location has no boundary', () => {
    const connection = exit('District', 30, 60);

    expect(hasDoor(connection, undefined, SIZE)).toBe(true);
  });
});

describe('buildingNameBoards', () => {
  it('lists a board for each building door, titled with the building name', () => {
    const door = exit('Building', 10, 10, {
      destination: { $type: 'Building', name: 'The Inn' },
    } as never);

    const boards = buildingNameBoards([door, exit('District', 30, 60)]);

    expect(boards.map(({ text }) => text)).toEqual(['The Inn']);
  });

  it('keeps long names within a bounded board width', () => {
    const door = exit('Building', 10, 10, {
      destination: { $type: 'Building', name: 'The Guild Of Wandering Cartographers' },
    } as never);

    const [board] = buildingNameBoards([door]);

    expect(board.width).toBeLessThanOrEqual(2.4);
  });
});
