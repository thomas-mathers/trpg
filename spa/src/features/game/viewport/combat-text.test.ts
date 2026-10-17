import { describe, expect, it } from 'vitest';

import type { CombatActionResult } from '@/api/signalr-client/TRPG.Combat.Responses';

import { toCombatTexts, yawToward } from './combat-text';

function action(overrides: Partial<CombatActionResult>): CombatActionResult {
  return {
    outcome: 'Hit',
    attackerId: 'a',
    attackerName: 'A',
    abilityName: 'Strike',
    targetId: 't',
    targetName: 'T',
    narration: '',
    ...overrides,
  } as CombatActionResult;
}

describe('toCombatTexts', () => {
  it('shows plain damage for a hit', () => {
    expect(toCombatTexts([action({ damage: 7 })])).toEqual([
      { attackerId: 'a', targetId: 't', kind: 'hit', text: '7' },
    ]);
  });

  it('marks a critical hit', () => {
    expect(toCombatTexts([action({ damage: 14, isCritical: true })])[0]).toMatchObject({
      kind: 'crit',
      text: '14!',
    });
  });

  it('shows misses and blocks', () => {
    const kinds = toCombatTexts([action({ outcome: 'Miss' }), action({ outcome: 'Block' })]).map(
      (text) => text.text,
    );
    expect(kinds).toEqual(['Miss', 'Blocked']);
  });

  it('ignores outcomes without a number to show', () => {
    expect(toCombatTexts([action({ outcome: 'Buff' })])).toEqual([]);
  });
});

describe('yawToward', () => {
  it('faces north-bound targets with zero yaw', () => {
    expect(yawToward({ x: 0, z: 0 }, { x: 0, z: -5 })).toBeCloseTo(0);
  });

  it('turns toward an eastern target', () => {
    expect(yawToward({ x: 0, z: 0 }, { x: 5, z: 0 })).toBeCloseTo(-Math.PI / 2);
  });
});
