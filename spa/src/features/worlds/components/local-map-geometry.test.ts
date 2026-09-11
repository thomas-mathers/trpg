import { describe, expect, it } from 'vitest';

import {
  corridorGate,
  doorwayJambPath,
  roomWallPath,
  roundedCorridorPath,
} from './local-map-geometry';

describe('corridor geometry', () => {
  it('keeps an aligned corridor straight, without a smoothing jog', () => {
    expect(
      roundedCorridorPath([
        { x: 100, y: 42 },
        { x: 230, y: 42 },
      ]),
    ).toBe('M 100 42 L 230 42');
  });

  it('rounds both sides of a corner by the same radius', () => {
    expect(
      roundedCorridorPath([
        { x: 0, y: 0 },
        { x: 100, y: 0 },
        { x: 100, y: 20 },
      ]),
    ).toBe('M 0 0 L 90 0 Q 100 0 100 10 L 100 20');
  });

  it('does not divide by zero for repeated points or empty paths', () => {
    expect(roundedCorridorPath([])).toBe('');
    expect(
      roundedCorridorPath([
        { x: 0, y: 0 },
        { x: 0, y: 0 },
        { x: 100, y: 0 },
      ]),
    ).toBe('M 0 0 L 100 0');
    expect(
      corridorGate([
        { x: 0, y: 0 },
        { x: 0, y: 0 },
      ]),
    ).toEqual({ x: 0, y: 0, angle: 0 });
  });

  it('places a lock across a straight stretch instead of on a bend', () => {
    expect(
      corridorGate([
        { x: 0, y: 0 },
        { x: 30, y: 0 },
        { x: 30, y: 100 },
      ]),
    ).toEqual({ x: 30, y: 50, angle: 90 });
  });
});

describe('room architecture', () => {
  it('leaves a floor-width opening at the exact doorway coordinate', () => {
    const path = roomWallPath({
      width: 120,
      height: 80,
      doorways: [{ side: 'top', offset: 0.25 }],
    });
    expect(path).toContain('M 0 0 L 13 0');
    expect(path).toContain('M 47 0 L 120 0');
    expect(path).not.toContain('M 0 0 L 120 0');
  });

  it('merges overlapping openings without redrawing a wall through them', () => {
    const path = roomWallPath({
      width: 120,
      height: 80,
      doorways: [
        { side: 'left', offset: 0.5 },
        { side: 'left', offset: 0.55 },
      ],
    });
    expect(path).toContain('M 0 0 L 0 23');
    expect(path).toContain('M 0 61 L 0 80');
    expect(path).not.toContain('M 0 48.5');
  });

  it('puts jambs on the wall rather than adding a cream tab outside the room', () => {
    expect(
      doorwayJambPath({ width: 120, height: 80, doorways: [{ side: 'right', offset: 0.5 }] }),
    ).toBe('M 115 23 L 122 23 M 115 57 L 122 57');
  });
});
