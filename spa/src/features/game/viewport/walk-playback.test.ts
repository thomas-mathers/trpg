import { describe, expect, it } from 'vitest';

import type { CreatureWalkSnapshot } from '@/api/signalr-client/TRPG.GameSessions.Responses';

import { blendOffset, walkedDistance, walkPoseAt, walkSignature } from './walk-playback';

function walkOf(overrides: Partial<CreatureWalkSnapshot> = {}): CreatureWalkSnapshot {
  return {
    points: [
      { x: 0, y: 0 },
      { x: 10, y: 0 },
    ],
    startedAtGameTimeMilliseconds: 1000,
    metersPerGameSecond: 2,
    leavesAtEnd: false,
    ...overrides,
  };
}

describe('walkedDistance', () => {
  it('is zero before the walk starts', () => {
    expect(walkedDistance(walkOf(), 0)).toBe(0);
  });

  it('advances at the walk pace in game seconds', () => {
    expect(walkedDistance(walkOf(), 4000)).toBe(6);
  });

  it('holds still at the pause instant while the walk is paused', () => {
    expect(walkedDistance(walkOf({ pausedAtGameTimeMilliseconds: 3000 }), 60_000)).toBe(4);
  });

  it('keeps walking up to the pause instant', () => {
    expect(walkedDistance(walkOf({ pausedAtGameTimeMilliseconds: 9000 }), 4000)).toBe(6);
  });
});

describe('walkPoseAt', () => {
  const corner = [
    { x: 0, y: 0 },
    { x: 10, y: 0 },
    { x: 10, y: 10 },
  ];

  it('interpolates along the first segment facing east', () => {
    expect(walkPoseAt(corner, 4)).toEqual({ x: 4, y: 0, angle: Math.PI / 2, finished: false });
  });

  it('continues onto the next segment facing south', () => {
    expect(walkPoseAt(corner, 15)).toEqual({ x: 10, y: 5, angle: Math.PI, finished: false });
  });

  it('faces north when walking toward negative y', () => {
    const pose = walkPoseAt(
      [
        { x: 0, y: 10 },
        { x: 0, y: 0 },
      ],
      5,
    );

    expect(pose.angle).toBe(0);
  });

  it('normalizes a westward heading into the positive range', () => {
    const pose = walkPoseAt(
      [
        { x: 10, y: 0 },
        { x: 0, y: 0 },
      ],
      5,
    );

    expect(pose.angle).toBeCloseTo((Math.PI * 3) / 2);
  });

  it('rests on the last point, facing the last segment, once the path is walked', () => {
    expect(walkPoseAt(corner, 99)).toEqual({ x: 10, y: 10, angle: Math.PI, finished: true });
  });

  it('skips zero-length segments', () => {
    const pose = walkPoseAt(
      [
        { x: 0, y: 0 },
        { x: 0, y: 0 },
        { x: 10, y: 0 },
      ],
      5,
    );

    expect(pose).toEqual({ x: 5, y: 0, angle: Math.PI / 2, finished: false });
  });
});

describe('walkSignature', () => {
  it('matches for the same walk delivered in a new array', () => {
    expect(walkSignature(walkOf())).toBe(walkSignature(walkOf({ points: [...walkOf().points] })));
  });

  it('differs once the walk is paused', () => {
    expect(walkSignature(walkOf())).not.toBe(
      walkSignature(walkOf({ pausedAtGameTimeMilliseconds: 3000 })),
    );
  });

  it('differs when the walk starts at another time', () => {
    expect(walkSignature(walkOf())).not.toBe(
      walkSignature(walkOf({ startedAtGameTimeMilliseconds: 2000 })),
    );
  });
});

describe('blendOffset', () => {
  it('carries a small gap so the creature eases onto its target', () => {
    expect(blendOffset({ x: 1, y: 0 }, { x: 0, y: 0 })).toEqual({ x: 1, y: 0 });
  });

  it('drops a large gap so the creature snaps instead of sliding', () => {
    expect(blendOffset({ x: 20, y: 0 }, { x: 0, y: 0 })).toEqual({ x: 0, y: 0 });
  });

  it('has no gap before the first position is known', () => {
    expect(blendOffset(null, { x: 5, y: 5 })).toEqual({ x: 0, y: 0 });
  });
});
