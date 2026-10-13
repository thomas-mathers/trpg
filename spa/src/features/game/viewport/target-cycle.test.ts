import { describe, expect, it } from 'vitest';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { cycleTarget } from './target-cycle';

function creature(id: string, x: number, condition: CreatureStatusSnapshot['condition'] = 'Awake') {
  return { id, condition, placement: { x, y: 0, angle: 0 } };
}

const player = { x: 0, y: 0 };
const creatures = [creature('far', 9), creature('near', 1), creature('mid', 4)];

describe('cycleTarget', () => {
  it('starts with the nearest creature', () => {
    expect(cycleTarget(creatures, player, undefined, 1)).toBe('near');
  });

  it('moves outward by distance and wraps around', () => {
    expect(cycleTarget(creatures, player, 'near', 1)).toBe('mid');
    expect(cycleTarget(creatures, player, 'mid', 1)).toBe('far');
    expect(cycleTarget(creatures, player, 'far', 1)).toBe('near');
  });

  it('starts with the farthest creature when stepping backwards', () => {
    expect(cycleTarget(creatures, player, undefined, -1)).toBe('far');
  });

  it('steps backwards and wraps around', () => {
    expect(cycleTarget(creatures, player, 'mid', -1)).toBe('near');
    expect(cycleTarget(creatures, player, 'near', -1)).toBe('far');
  });

  it('skips dead creatures', () => {
    const withCorpse = [creature('corpse', 0.5, 'Dead'), ...creatures];

    expect(cycleTarget(withCorpse, player, undefined, 1)).toBe('near');
  });

  it('restarts from the nearest creature when the current target is gone', () => {
    expect(cycleTarget(creatures, player, 'missing', 1)).toBe('near');
  });

  it('returns nothing when no living creature is nearby', () => {
    expect(cycleTarget([creature('corpse', 1, 'Dead')], player, undefined, 1)).toBeUndefined();
  });
});
