import { describe, expect, it } from 'vitest';

import type { GreenSpaceSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { residentialLawns } from './green-spaces';

function space(id: string, x: number, y: number, width: number, depth: number): GreenSpaceSnapshot {
  return { id, placement: { x, y, angle: 0 }, footprint: { width, depth } };
}

describe('residentialLawns', () => {
  it('renders the backend supplied footprint and placement', () => {
    expect(residentialLawns([space('court', 10, 15, 13, 28)])).toEqual([
      { id: 'court', x: 10, y: 15, width: 13, depth: 28 },
    ]);
  });
});
