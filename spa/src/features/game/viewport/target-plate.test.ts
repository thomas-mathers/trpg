import { describe, expect, it } from 'vitest';

import type { CreatureStatusSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { standingColor, targetPlate } from './target-plate';

function creature(overrides: Partial<CreatureStatusSnapshot> = {}): CreatureStatusSnapshot {
  return {
    name: 'Tessa',
    level: 3,
    condition: 'Awake',
    currentHp: 5,
    maximumHp: 10,
    currentAp: 10,
    maximumAp: 10,
    currentMp: 0,
    maximumMp: 0,
    ...overrides,
  } as CreatureStatusSnapshot;
}

describe('targetPlate', () => {
  it('titles an awake creature with its name and level', () => {
    expect(targetPlate(creature()).title).toBe('Tessa · Lv 3');
  });

  it('adds the condition to the title when the creature is not awake', () => {
    expect(targetPlate(creature({ condition: 'Sleeping' })).title).toBe('Tessa · Lv 3 · Sleeping');
  });

  it('fills each bar by its current share of the maximum', () => {
    const { bars } = targetPlate(creature());

    expect(bars.map(({ fraction }) => fraction)).toEqual([0.5, 1]);
  });

  it('omits the bar for a resource the creature does not have', () => {
    expect(targetPlate(creature()).bars).toHaveLength(2);
  });

  it('clamps overfull and negative resources into the bar', () => {
    const { bars } = targetPlate(creature({ currentHp: 30, currentAp: -4 }));

    expect(bars.map(({ fraction }) => fraction)).toEqual([1, 0]);
  });

  it('shows no bars for a dead creature', () => {
    const plate = targetPlate(creature({ condition: 'Dead' }));

    expect(plate.bars).toEqual([]);
    expect(plate.title).toBe('Tessa · Lv 3 · Dead');
  });
});

describe('standingColor', () => {
  it('distinguishes hostile, neutral and friendly standing', () => {
    expect(new Set([standingColor(-5), standingColor(0), standingColor(5)]).size).toBe(3);
  });

  it('treats unknown reputation as neutral', () => {
    expect(standingColor(undefined)).toBe(standingColor(0));
  });
});
